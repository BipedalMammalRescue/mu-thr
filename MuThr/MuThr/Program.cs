using System.Collections.Immutable;
using MuThr.DataModels;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;
using MuThr.Sdk;
using Serilog;

internal class Program
{
    private class ExampleData(string data) : ILeafDataPoint
    {
        public ReadOnlySpan<byte> GetBytes() => [];
        public string GetString() => data;
    }

    private class ExampleArrData(IDataPoint[] data) : IArrayDataPoint
    {
        public IDataPoint[] Get() => data;
    }

    private class ExampleObjData(Dictionary<string, IDataPoint> data) : IObjDataPoint
    {
        public IDataPoint? Get(string path) => data.TryGetValue(path, out IDataPoint? result) ? result : null;
    }

    private class ExampleTaskProvider : ITaskProvider
    {
        private readonly Dictionary<string, IDataPoint> _sources = new()
        {
            ["init"] = new ExampleObjData(new Dictionary<string, IDataPoint>()
            {
                ["lhs"] = new ExampleData("t1"),
                ["rhs"] = new ExampleData("t2"),
            }),
            ["t1"] = new ExampleObjData(new Dictionary<string, IDataPoint>()
            {
                ["foo"] = new ExampleData("hello"),
                ["bar"] = new ExampleData("world"),
            }),
            ["t2"] = new ExampleObjData(new Dictionary<string, IDataPoint>()
            {
                ["foo"] = new ExampleData("one"),
                ["bar"] = new ExampleData("two"),
            }),
        };
        
        private readonly BuildAction _initAction = new BypassBuildAction()
        {
            ChildTasks = [
                new DeriveBuildAction() {
                    SourcePath = "lhs"
                },
                new DeriveBuildAction() {
                    SourcePath = "rhs"
                }
            ]
        };

        private readonly BuildAction _regAction = new CommandBuildAction()
        {
            Process = "echo",
            Arguments = [
                "#(foo)",
                "#(bar)"
            ]
        };

        public (BuildAction Action, IDataPoint SourceData) CreateTask(string key)
        {
            IDataPoint data = _sources[key];
            BuildAction action = key == "init" ? _initAction : _regAction;
            return (action, data);
        }
    }

    private static async Task Main(string[] _)
    {
        // initialize logging
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        IMuThrLogger logger = new MuThrLogger(["AppRoot"], new LoggerConfiguration()
            .Enrich.With(new MuThrLogEnricher())
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}][{PrimaryChannel}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger());
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

        Coordinator coord = new(new ExampleTaskProvider(), logger.WithChannel("Coordinator"));
        coord.ScheduleTask("init");
        coord.ScheduleTask("t1");
        coord.ScheduleTask("t1");
        coord.ScheduleTask("t1");
        coord.ScheduleTask("t1");

        ImmutableDictionary<string, BuildResult> results = await coord.OutputTask.ConfigureAwait(false);

        try
        {
            foreach (var result in results)
            {
                using StreamReader fs = new(result.Value.OutputPath);
                string content = await fs.ReadToEndAsync().ConfigureAwait(false);
                logger.Information("Job result: <{key}> `{result}`", result.Key, content);
            }
        }
        finally
        {
            foreach (BuildResult result in results.Values)
            {
                if (File.Exists(result.OutputPath))
                {
                    File.Delete(result.OutputPath);
                }
            }
        }
    }
}
