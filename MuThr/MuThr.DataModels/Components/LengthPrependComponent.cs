using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

public class LengthPrependComponent : ITransformComponent
{
    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        await next.WriteAsync(BitConverter.GetBytes(prev.Length)).ConfigureAwait(false);
        await prev.CopyToAsync(next).ConfigureAwait(false);
    }
}
