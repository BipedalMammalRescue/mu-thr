namespace MuThr.DataModels.BuildActions.Components.Input;

/// <summary>
/// Dump input data into a file and drop the original input.
/// </summary>
public class FileInput : IInputComponent
{
    public required string Path { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination)
    {
        await using FileStream file = File.Create(environment.ExpandValues(Path));
        await source.CopyToAsync(file).ConfigureAwait(false);
    }
}
