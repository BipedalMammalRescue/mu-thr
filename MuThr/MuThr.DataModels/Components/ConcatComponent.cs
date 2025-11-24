using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

public class ConcatComponent : ITransformComponent
{
    public required ITransformComponent[] Sources { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        foreach (ITransformComponent src in Sources)
        {
            await src.TransformAsync(environment, prev, next, logger).ConfigureAwait(false);
        }
    }
}