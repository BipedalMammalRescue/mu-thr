using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Input;

public class ConcatInput : IInputComponent
{
    public required IInputComponent[] Sources { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger)
    {
        // TODO: need to tag these loggers properly
        foreach (IInputComponent src in Sources)
        {
            await src.TransformAsync(environment, source, destination, logger);
        }
    }
}
