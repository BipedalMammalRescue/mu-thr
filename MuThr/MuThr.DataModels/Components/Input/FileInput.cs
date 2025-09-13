using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Input;

/// <summary>
/// Dump input data into a file and drop the original input.
/// </summary>
public class FileInput : IInputComponent
{
    public required string Path { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger)
    {
        string path = environment.ExpandValues(Path);
        logger.Information("Creating input stream from file: {path}", path);
        
        await using FileStream file = File.Create(path);
        await source.CopyToAsync(file).ConfigureAwait(false);
    }
}
