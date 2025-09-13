using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

public class DeriveBuildAction : BuildAction
{
    public required string SourcePath { get; set; }
    protected override Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
    {
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        ILeafDataPoint? sourceData = environment.GetDataPoint<ILeafDataPoint>(sourcePath);
        if (sourceData == null)
            return Task.FromResult(Error($"Can't find array at data path {sourcePathFull}"));

        string newKey = sourceData.GetString();
        logger.Information("Creating derived task with key {key}", newKey);
        return Task.FromResult(Derive(newKey));
    }
}
