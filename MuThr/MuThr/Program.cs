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
        public IDataPoint? Get(string path) => data.TryGetValue(path, out IDataPoint? result) ? null : result;
    }

    private static async Task Main(string[] _)
    {
        BuildAction rootAction = new CommandBuildAction()
        {
            ChildTasks = [
                new CommandBuildAction()
                {
                    Process = "echo",
                    Arguments = ["hello"]
                },
                new CommandBuildAction()
                {
                    Process = "echo",
                    Arguments = ["world"]
                },
                new CommandBuildAction()
                {
                    Process = "echo",
                    Arguments = ["111"]
                }
            ],
            Process = "cat",
            Arguments = [
                "#0",
                "#1"
            ]
        };

        // initialize logging
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        IMuThrLogger logger = new MuThrLogger(["AppRoot"], new LoggerConfiguration()
            .Enrich.With(new MuThrLogEnricher())
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}][{PrimaryChannel}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger());
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

        Coordinator coord = new(rootAction,
            new ExampleObjData(new Dictionary<string, IDataPoint>() {
                ["foo"] = new ExampleData("hello"),
                ["bar"] = new ExampleData("world")
            }), logger.WithChannel("Coordinator"));

        IEnumerable<BuildResult> result = await coord.WaitAsync().ConfigureAwait(false);
        string outputPath = result.First().OutputPath;

        try
        {
            using StreamReader fs = new(outputPath);
            string content = await fs.ReadToEndAsync().ConfigureAwait(false);
            logger.Information("Job result: {result}", content);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
