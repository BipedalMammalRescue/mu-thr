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

        BuildAction rootAction = new CommandBuildAction()
        {
            ChildTasks = [
                new UnpackBuildAction()
                {
                    SourcePath = "foobar:foo",
                    DerivedAction = new CommandBuildAction()
                    {
                        Process = "echo",
                        Arguments = ["#(foo)", "#(bar)"]
                    }
                },
                new CommandBuildAction()
                {
                    Process = "echo",
                    Arguments = ["#(foo)"]
                },
                new CommandBuildAction()
                {
                    Process = "echo",
                    Arguments = ["#(bar)"]
                }
            ],
            Process = "cat",
            Arguments = [
                "#1",
                "#2"
            ]
        };

        Coordinator coord = new(rootAction,
            new ExampleObjData(new Dictionary<string, IDataPoint>()
            {
                ["foo"] = new ExampleData("hello"),
                ["bar"] = new ExampleData("world"),
                ["foobar"] = new ExampleObjData(new Dictionary<string, IDataPoint>()
                {
                    ["foo"] = new ExampleArrData([
                        new ExampleObjData(new Dictionary<string, IDataPoint>() {
                            ["foo"] = new ExampleData("hello"),
                            ["bar"] = new ExampleData("world"),
                        }),
                        new ExampleObjData(new Dictionary<string, IDataPoint>() {
                            ["foo"] = new ExampleData("foo"),
                            ["bar"] = new ExampleData("bar"),
                        })
                    ])
                })
            }), logger.WithChannel("Coordinator"));

        IEnumerable<BuildResult> results = await coord.WaitAsync().ConfigureAwait(false);

        try
        {
            foreach (BuildResult result in results)
            {
                using StreamReader fs = new(result.OutputPath);
                string content = await fs.ReadToEndAsync().ConfigureAwait(false);
                logger.Information("Job result: `{result}`", content);
            }
        }
        finally
        {
            foreach (BuildResult result in results)
            {
                if (File.Exists(result.OutputPath))
                {
                    File.Delete(result.OutputPath);
                }
            }
        }
    }
}
