using MuThr.DataModels.BuildActions;
using MuThr.Sdk;

internal class Program
{
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

        Coordinator coord = new(rootAction, System.Collections.Immutable.ImmutableDictionary<string, string>.Empty);
        BuildResult result = await coord.WaitAsync().ConfigureAwait(false);

        string outputPath = result.OutputPath;

        try
        {
            using StreamReader fs = new(outputPath);
            string content = await fs.ReadToEndAsync().ConfigureAwait(false);
            Console.Write('`');
            Console.Write(content);
            Console.WriteLine('`');
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
