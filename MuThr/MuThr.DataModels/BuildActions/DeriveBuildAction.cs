using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

public class DeriveBuildAction : BuildAction
{
    public required string SourcePath { get; set; }
    public required BuildAction DerivedAction { get; set; }
    protected override Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
    {
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        IDataPoint? sourceData = environment.GetDataPoint<IDataPoint>(sourcePath);
        if (sourceData == null)
            return Task.FromResult(Error($"Can't find array at data path {sourcePathFull}"));

        logger.Information("Creating derived task of type {type} from data at {path}", DerivedAction.GetType().Name, sourcePathFull);
        return Task.FromResult(Derive(new DerivedTask(DerivedAction, sourceData, sourcePathFull)));
    }
}
