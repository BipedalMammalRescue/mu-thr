namespace MuThr.DataModels.BuildActions.Components.Input;

public class ConcatInput : IInputComponent
{
    public required IInputComponent[] Sources { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination)
    {
        foreach (IInputComponent src in Sources)
        {
            await src.TransformAsync(environment, source, destination);
        }
    }
}
