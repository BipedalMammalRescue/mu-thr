using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using MuThr.DataModels;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.Sdk;

public record TaskReference(Guid? Parent, Guid Id, BuildAction Action);

public class Coordinator
{
    private class InvalidBuildAction : BuildAction
    {
        public override Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
        {
            throw new NotImplementedException();
        }
    }

    private class InvalidDataPoint : IDataPoint;

    private readonly static InvalidDataPoint _invalidData = new();
    private readonly static InvalidBuildAction _invalidAction = new();

    private record BuildTask
    (
        string Key, // only useful for roots
        BuildTask? Parent,
        Guid Id,
        int ParentalRelation,
        IDataPoint Source,
        BuildAction Action,
        string PathPrefix
    );

    private record Continuation
    (
        BuildTask Task,
        ImmutableArray<BuildResult> Children,
        long TargetMap,
        long CurrentMap
    );

    private record RequestResponse;
    private record DenyRequest(string Key) : RequestResponse;
    private record AcceptRequest(string Key, BuildTask Task) : RequestResponse;
    
    private record TaskResult(BuildTask Task, BuildResult Result);

    private static long BuildMask(int length) => (1 << length) - 1;
    private static long BuildFlag(int index) => 1 << index;

    private readonly Subject<string> _requestQueue = new();
    private readonly Subject<BuildTask> _scheduleQueue = new();
    private readonly Subject<(BuildTask Task, ImmutableArray<BuildResult> Children)> _readyQueue = new();
    private readonly ConcurrentDictionary<Guid, Continuation> _continuations = [];
    private readonly IObservable<TaskResult> _results;

    private int _rootCount = 0;

    public Task<ImmutableDictionary<string, BuildResult>> OutputTask { get; }

    private static IMuThrLogger CreateTaskLogger(IMuThrLogger logger, BuildTask task) => logger.WithChannel(task.Action.GetType().Name).ForTask(task.Id);

    public void ScheduleTask(string key)
    {
        _requestQueue.OnNext(key);
    }

    public Coordinator(ITaskProvider taskProvider, IMuThrLogger logger)
    {
        // these are just logging
        _readyQueue.Subscribe(ready => logger.Verbose("Task ready: {id}", ready.Task.Id));

        // transform requests into scheduling
        IObservable<RequestResponse> responses = _requestQueue
            .Distinct()
            .Select<string, RequestResponse>(req =>
            {
                Interlocked.Increment(ref _rootCount);

                try
                {
                    logger.Verbose("Handling request `{req}`", req);
                    (BuildAction action, IDataPoint data) = taskProvider.CreateTask(req);
                    BuildTask newTask = new(req, null, Guid.NewGuid(), -1, data, action, string.Empty);
                    return new AcceptRequest(req, newTask);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to create task for request `{req}`", req);
                    return new DenyRequest(req);
                }
            }).Publish().AutoConnect();

        responses.OfType<AcceptRequest>()
            .Select(res => res.Task)
            .Subscribe(_scheduleQueue);

        // make a reusable source of schedule requests
        IObservable<(BuildTask Task, BuildAction[] Children)> taskAndChildren = _scheduleQueue
            .Do(s =>
            {
                logger.Verbose("Task scheduled: Key = {key}, Type = {name}, ID = {id}, Parent = {parent}", s.Key, s.Action.GetType().Name, s.Id, s.Parent?.Id);
                logger.Verbose("Task detail: {id}, {detail}", s.Id, JsonSerializer.Serialize(s.Action));
            })
            // use an empty build environment here since this is before any execution, all there is in the env is source data
            .Select(t => (t, t.Action.CollectChildren(new BuildEnvironment(t.Source, [], t.PathPrefix), CreateTaskLogger(logger, t)).ToArray()))
            .Publish()
            .AutoConnect();

        // separate out tasks immediately schedulable
        taskAndChildren
            .Where(pair => pair.Children.Length <= 0)
            .Select(pair => (pair.Task, ImmutableArray<BuildResult>.Empty))
            .Subscribe(_readyQueue);

        // schedule children and create continuations
        taskAndChildren
            .Where(pair => pair.Children.Length > 0)
            .SelectMany(pair =>
            {
                // register a continuation
                _continuations.AddOrUpdate(
                    pair.Task.Id,
                    _ => new Continuation(pair.Task, [.. new BuildResult[pair.Children.Length]], BuildMask(pair.Children.Length), 0),
                    (oldId, oldCont) => oldCont with { Task = pair.Task }
                );

                // send the children back to scheduler (children inherit keys since why not)
                return pair.Children
                    .Select((child, index) => new BuildTask(pair.Task.Key, pair.Task, Guid.NewGuid(), index, pair.Task.Source, child, pair.Task.PathPrefix));
            })
            .Subscribe(_scheduleQueue);

        // construct the queue of results, where individual tasks are constucted asynchronously
        _results = Observable.Merge(responses.OfType<DenyRequest>()
            .Select(res => new TaskResult(new BuildTask(res.Key, null, Guid.Empty, -1, _invalidData, _invalidAction, string.Empty), new BuildResult() { Errors = [new BuildErrorMessage("Failed to create task.")] })),
            Observable.Merge(_readyQueue.Select(ready => Task.Run(async () =>
        {
            // early termination: if there are errored children, bubble up the error
            IEnumerable<BuildError> childErrors = ready.Children.SelectMany(c => c.Errors);
            if (childErrors.Any())
            {
                logger.Error("Task {parent}/{id} canceled due to child task failures.", ready.Task.Parent?.Id, ready.Task.Id);
                return new TaskResult(ready.Task, new BuildResult() { Errors = [.. childErrors] });
            }

            // normal code path
            try
            {
                logger.Verbose("Task started: {id}", ready.Task.Id);

                BuildEnvironment env = new(ready.Task.Source, ready.Children, string.Empty);
                BuildResult result = await ready.Task.Action.ExecuteAsync(env, CreateTaskLogger(logger, ready.Task)).ConfigureAwait(false);

                // clean up
                logger.Verbose("Cleaning up for task {id}.", ready.Task.Id);
                foreach (BuildResult usedResult in ready.Children)
                {
                    if (File.Exists(usedResult.OutputPath))
                    {
                        File.Delete(usedResult.OutputPath);
                    }
                }
                logger.Verbose("Task {id} cleaned up.", ready.Task.Id);

                return new TaskResult(ready.Task, result);
            }
            catch (Exception ex)
            {
                return new TaskResult(ready.Task, new BuildResult() { Errors = [new BuildException(ex)] });
            }
        }).ToObservable()))).Publish().AutoConnect();

        // funnel results back into continuation
        _results.Where(result => result.Task.Parent != null).SelectMany<TaskResult, Continuation>(result =>
        {
            // report
            if (result.Result.Errors.Length > 0)
            {
                logger.Information("Task failed: {id}, errors: {errors}", result.Task.Id, result.Result.Errors);
            }
            else
            {
                logger.Information("Task succeeded: {id}", result.Task.Id);
            }

            // update continuations
            Continuation candidate = _continuations.AddOrUpdate(
                result.Task.Parent!.Id,
                _ => new Continuation(
                    default!,
                    ImmutableArray<BuildResult>.Empty.AddRange(new BuildResult[result.Task.Parent.Action.ChildTasks.Length]).SetItem(result.Task.ParentalRelation, result.Result),
                    BuildMask(result.Task.Parent.Action.ChildTasks.Length),
                    BuildFlag(result.Task.ParentalRelation)),
                (oldId, oldCont) =>
                {
                    return oldCont with
                    {
                        Children = oldCont.Children.SetItem(result.Task.ParentalRelation, result.Result),
                        CurrentMap = oldCont.CurrentMap | BuildFlag(result.Task.ParentalRelation)
                    };
                });

            // check if a new task is ready
            if ((candidate.CurrentMap & candidate.TargetMap) == candidate.TargetMap)
            {
                _continuations.Remove(candidate.Task.Id, out _);
                return [candidate];
            }
            else
            {
                return [];
            }
        }).Select(cont => (cont.Task, cont.Children)).Subscribe(_readyQueue);

        // spawn new root tasks from derived tasks
        _results
            .Where(result => result.Result.DerivedTasks.Length > 0)
            .SelectMany(result => result.Result.DerivedTasks)
            .Subscribe(_requestQueue);

        // builds a dictinary from key to results for root tasks only
        OutputTask = _results
            .Where(result => result.Task.Parent == null)
            .TakeUntil(_ => Interlocked.Decrement(ref _rootCount) == 0)
            .Scan(ImmutableDictionary<string, BuildResult>.Empty, (oldArr, newResult) => oldArr.Add(newResult.Task.Key, newResult.Result))
            .ToTask();
    }
}
