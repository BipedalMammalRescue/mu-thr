using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Output;

public class ConcatOutput : IOutputComponent
{
    public required IOutputComponent[] Sources { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        foreach (IOutputComponent src in Sources)
        {
            await src.TransformAsync(environment, prev, next, logger).ConfigureAwait(false);
        }
    }
}