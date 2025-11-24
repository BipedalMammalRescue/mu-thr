using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

public class PassThroughOutput : ITransformComponent
{
    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        await prev.CopyToAsync(next).ConfigureAwait(false);
    }
}
