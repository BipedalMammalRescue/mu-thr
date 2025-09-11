using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using MuThr.DataModels.BuildActions;
using Serilog;

namespace MuThr.Sdk;

public record TaskReference(Guid? Parent, Guid Id, BuildAction Action);

public class Coordinator
{
    private record BuildTask
    (
        BuildTask? Parent,
        Guid Id,
        int ParentalRelation,
        ImmutableDictionary<string, string> Source,
        BuildAction Action
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

    public Coordinator(BuildAction rootAction, ImmutableDictionary<string, string> source, ILogger logger)
    {
        _readyQueue.Subscribe(ready =>
        {
            logger.Information("Task ready: {id}", ready.Task.Id);
        });

        _scheduleQueue.Subscribe(s =>
            {
                logger.Information("Task scheduled: {id}, parent is {parent}", s.Id, s.Parent?.Id);
                logger.Debug("Task detail: {id}, {detail}", JsonSerializer.Serialize(s.Action));
            });

        // funnel leaf tasks into the ready queue immediately 
        _scheduleQueue
            .Where(task => task.Action.ChildTasks.Length <= 0)
            .Select(task => (task, ImmutableArray<BuildResult>.Empty))
            .Subscribe(_readyQueue);

        // funnel child tasks back into the schedule pool
        _scheduleQueue
            .Where(task => task.Action.ChildTasks.Length > 0)
            .SelectMany(task =>
            {
                // register a continuation
                _continuations.AddOrUpdate(
                    task.Id,
                    _ => new Continuation(task, [.. new BuildResult[task.Action.ChildTasks.Length]], BuildMask(task.Action.ChildTasks.Length), 0),
                    (oldId, oldCont) => oldCont with { Task = task }
                );

                // send the children back to scheduler
                return task.Action.ChildTasks
                    .Select((child, index) => new BuildTask(task, Guid.NewGuid(), index, task.Source, child));
            })
            .Subscribe(_scheduleQueue);

        // construct the queue of results, where individual tasks are constucted asynchronously
        _results = Observable.Merge(_readyQueue.Select(ready => Task.Run(async () =>
        {
            logger.Information("Task started: {id}", ready.Task.Id);

            // early termination: if there are errored children, bubble up the error
            IEnumerable<BuildError> childErrors = ready.Children.SelectMany(c => c.Errors);
            if (childErrors.Any())
                return new TaskResult(ready.Task, new BuildResult() { Errors = [.. childErrors] });

            // normal code path
            try
            {
                BuildEnvironment env = new(ready.Task.Source, ready.Children);
                BuildResult result = await ready.Task.Action.ExecuteAsync(env).ConfigureAwait(false);

                // clean up
                foreach (BuildResult usedResult in ready.Children)
                {
                    if (File.Exists(usedResult.OutputPath))
                    {
                        File.Delete(usedResult.OutputPath);
                    }
                }

                return new TaskResult(ready.Task, result);
            }
            catch (Exception ex)
            {
                return new TaskResult(ready.Task, new BuildResult() { Errors = [new BuildException(ex)] });
            }
        }).ToObservable())).Publish().AutoConnect().TakeUntil(x => x.Task.Parent == null);

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

        // register the first task
        _scheduleQueue.OnNext(new BuildTask(null, Guid.NewGuid(), -1, source, rootAction));
    }

    public async Task<BuildResult> WaitAsync()
    {
        var result = await _results.ToTask().ConfigureAwait(false);
        return result.Result;
    }
}