using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.Components.Output;

public class EnumerateConcatOutput : IOutputComponent
{
    public required string SourcePath { get; set; }

    public required IOutputComponent Delegate { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger)
    {
        // extract the enumerable from source path
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        IArrayDataPoint? sourceData = environment.GetDataPoint<IArrayDataPoint>(sourcePath) ?? throw new Exception($"Can't find array at data path {sourcePathFull}");

        // apply the delegate on each action
        foreach (var env in sourceData.Get().Select(data => environment with { SourceData = data }))
        {
            await Delegate.TransformAsync(env, source, destination, logger).ConfigureAwait(false);
        }
    }
}