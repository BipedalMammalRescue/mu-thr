using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using MuThr.DataModels.BuildActions;

namespace MuThr.Sdk;

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

    private readonly Subject<BuildTask> _scheduleInput = new();
    private readonly Subject<(BuildTask Task, ImmutableArray<BuildResult> Children)> _readyQueue = new();
    private readonly ConcurrentDictionary<Guid, Continuation> _continuations = [];
    private readonly IObservable<TaskResult> _results;

    public Coordinator(BuildAction rootAction, ImmutableDictionary<string, string> source)
    {
        _readyQueue.Subscribe(rdy =>
        {
            Console.Write("ready: ");
            Console.WriteLine(rdy.Task.Id);
        });

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
                    .Select((child, index) => new BuildTask(task, Guid.NewGuid(), index, task.Source, child));
            })
            .Subscribe(_scheduleInput);

        // construct the queue of results, where individual tasks are constucted asynchronously
        _results = Observable.Merge(_readyQueue.Distinct(ready => ready.Task.Id).Select(ready => Task.Run(async () =>
        {
            Console.Write("begin: ");
            Console.WriteLine(ready.Task.Id);

            // early termination: if there are errored children, bubble up the error
            IEnumerable<string> childErrors = ready.Children.SelectMany(c => c.Errors);
            if (childErrors.Any())
                return new TaskResult(ready.Task, new BuildResult() { Errors = [.. childErrors] });

            // normal code path
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

            Console.Write("end: ");
            Console.WriteLine(ready.Task.Id);

            return new TaskResult(ready.Task, result);
        }).ToObservable())).Publish().AutoConnect().TakeUntil(x => x.Task.Parent == null);

        // funnel results back into continuation
        _results.Where(result => result.Task.Parent != null).SelectMany<TaskResult, Continuation>(result =>
        {
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

            // emit when a new task is ready
            if (candidate.CurrentMap == candidate.TargetMap)
            {
                Console.WriteLine("triggering parent");
            }

            return (candidate.CurrentMap & candidate.TargetMap) == candidate.TargetMap ? [candidate] : [];
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