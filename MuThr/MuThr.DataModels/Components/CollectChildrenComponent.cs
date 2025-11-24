using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

public class CollectChildrenComponent : ITransformComponent
{
    public required ITransformComponent Delegate { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger)
    {
        foreach (BuildResult result in environment.ChildrenResults)
        {
            FileStream resultStream = File.OpenRead(result.OutputPath);
            await using (resultStream.ConfigureAwait(false))
            {
                await Delegate.TransformAsync(environment, resultStream, destination, logger).ConfigureAwait(false);
            }
        }
    }
}
