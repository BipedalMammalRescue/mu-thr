using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.Components;

public class BinaryPrintComponent : ITransformComponent
{
    public required string SourcePath { get; set; }

    public async Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        ILeafDataPoint? sourceData = environment.GetDataPoint<ILeafDataPoint>(sourcePath) ?? throw new Exception($"Can't find array at data path {sourcePathFull}");
        await next.WriteAsync(sourceData.GetBytes()).ConfigureAwait(false);
    }
}
