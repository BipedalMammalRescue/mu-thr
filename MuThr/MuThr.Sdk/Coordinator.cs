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
    private record BuildTask
    (
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

    private record TaskResult(BuildTask Task, BuildResult Result);

    private static long BuildMask(int length) => (1 << length) - 1;
    private static long BuildFlag(int index) => 1 << index;

    private readonly Subject<BuildTask> _scheduleQueue = new();
    private readonly Subject<(BuildTask Task, ImmutableArray<BuildResult> Children)> _readyQueue = new();
    private readonly ConcurrentDictionary<Guid, Continuation> _continuations = [];
    private readonly IObservable<TaskResult> _results;

    private int _rootCount = 1;

    private static IMuThrLogger CreateTaskLogger(IMuThrLogger logger, BuildTask task) => logger.WithChannel(task.Action.GetType().Name).ForTask(task.Id);

    public Coordinator(BuildAction rootAction, IDataPoint source, IMuThrLogger logger)
    {
        // these are just logging
        _readyQueue.Subscribe(ready => logger.Information("Task ready: {id}", ready.Task.Id));
        _scheduleQueue.Subscribe(s =>
            {
                logger.Information("Task scheduled: Type = {name} ID = {id}, Parent = {parent}", s.Action.GetType().Name, s.Id, s.Parent?.Id);
                logger.Verbose("Task detail: {id}, {detail}", JsonSerializer.Serialize(s.Action));
            });

        // make a reusable source of schedule requests
        IObservable<(BuildTask Task, BuildAction[] Children)> taskAndChildren = _scheduleQueue
            .Select(t => (t, t.Action.CollectChildren(t.Source, CreateTaskLogger(logger, t)).ToArray()))
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

                // send the children back to scheduler
                return pair.Children
                    .Select((child, index) => new BuildTask(pair.Task, Guid.NewGuid(), index, pair.Task.Source, child, pair.Task.PathPrefix));
            })
            .Subscribe(_scheduleQueue);

        // construct the queue of results, where individual tasks are constucted asynchronously
        _results = Observable.Merge(_readyQueue.Select(ready => Task.Run(async () =>
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
                logger.Information("Task started: {id}", ready.Task.Id);

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
        }).ToObservable())).Publish().AutoConnect(); // TODO: need to implement new termination condition

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
            .SelectMany(result =>
            {
                Interlocked.Increment(ref _rootCount);
                return result.Result.DerivedTasks.Select(task => new BuildTask(null, Guid.NewGuid(), -1, task.Data, task.Action, task.PathPrefix));
            })
            .Subscribe(_scheduleQueue);

        // register the first task
        _scheduleQueue.OnNext(new BuildTask(null, Guid.NewGuid(), -1, source, rootAction, string.Empty));
    }

    public async Task<IEnumerable<BuildResult>> WaitAsync()
    {
        return await _results
            .Where(result => result.Task.Parent == null)
            .TakeUntil(_ => Interlocked.Decrement(ref _rootCount) == 0)
            .Scan(ImmutableArray<BuildResult>.Empty, (oldArr, newResult) => oldArr.Add(newResult.Result))
            .ToTask().ConfigureAwait(false);
    }
}
