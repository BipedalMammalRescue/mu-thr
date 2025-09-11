using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using MuThr.DataModels.BuildActions;

namespace Muthr.Behavior;

public class Coordinator
{
    private record BuildTask
    (
        Guid? Parent,
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

    private static long BuildMask(int length) => (2 << length) - 1;
    private static long BuildFlag(int index) => 1 << index;

    private readonly Subject<BuildTask> _scheduleInput = new();
    private readonly Subject<(BuildTask Task, ImmutableArray<BuildResult> Children)> _readyQueue = new();
    private readonly ConcurrentDictionary<Guid, Continuation> _continuations = [];
    private readonly IObservable<TaskResult> _results;

    public Coordinator(BuildAction rootAction, ImmutableDictionary<string, string> source)
    {
        // funnel leaf tasks into the ready queue immediately 
        _scheduleInput
            .Where(task => task.Action.ChildTasks.Length <= 0)
            .Select(task => (task, ImmutableArray<BuildResult>.Empty))
            .Subscribe(_readyQueue);

        // funnel child tasks back into the schedule pool
        _scheduleInput
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
                    .Select((child, index) => new BuildTask(task.Id, Guid.NewGuid(), index, task.Source, child));
            })
            .Subscribe(_scheduleInput);

        // construct the queue of results, where individual tasks are constucted asynchronously
        _results = Observable.Merge(_readyQueue.Select(task => Task.Run(async () =>
        {
            // early termination: if there are errored children, bubble up the error
            IEnumerable<string> childErrors = task.Children.SelectMany(c => c.Errors);
            if (childErrors.Any())
                return new TaskResult(task.Task, new BuildResult() { Errors = [.. childErrors] });

            // normal code path
            BuildEnvironment env = new(task.Task.Source, task.Children);
            BuildResult result = await task.Task.Action.ExecuteAsync(env).ConfigureAwait(false);
            return new TaskResult(task.Task, result);
        }).ToObservable()));

        // funnel results back into continuation
        _results.Where(result => result.Task.Parent != null).SelectMany<TaskResult, Continuation>(result =>
        {
            if (!_continuations.TryRemove(result.Task.Parent!.Value, out Continuation? cont))
                return [];

            // update continuations
            Continuation candidate = _continuations.AddOrUpdate(
                result.Task.Parent!.Value,
                _ => throw new Exception("child returns before cotinuation is created"),
                (oldId, oldCont) =>
                {
                    return oldCont with
                    {
                        Children = oldCont.Children.SetItem(result.Task.ParentalRelation, result.Result),
                        CurrentMap = oldCont.CurrentMap | BuildFlag(result.Task.ParentalRelation)
                    };
                });

            // emit when a new task is ready
            return candidate.CurrentMap != candidate.TargetMap ? [] : [candidate];
        }).Select(cont => (cont.Task, cont.Children)).Subscribe(_readyQueue);

        // register the first task
        _scheduleInput.OnNext(new BuildTask(null, Guid.NewGuid(), -1, source, rootAction));
    }

    public async Task<BuildResult> WaitAsync()
    {
        var result = await _results.ToTask().ConfigureAwait(false);
        return result.Result;
    }
}