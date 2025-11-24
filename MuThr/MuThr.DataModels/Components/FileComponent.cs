using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

public class FileComponent : ITransformComponent
{
    public required string Path { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        string path = environment.ExpandValues(Path);
        logger.Information("Create output stream from file: {path}", path);

        await using FileStream source = File.OpenRead(path);
        await source.CopyToAsync(next).ConfigureAwait(false);
    }
}
