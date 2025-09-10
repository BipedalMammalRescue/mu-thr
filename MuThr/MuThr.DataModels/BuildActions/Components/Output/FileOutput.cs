namespace MuThr.DataModels.BuildActions.Components.Output;

public class FileOutput : IOutputComponent
{
    public required string Path { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next)
    {
        await using FileStream source = File.OpenRead(environment.ExpandValues(Path));
        await source.CopyToAsync(next).ConfigureAwait(false);
    }
}
