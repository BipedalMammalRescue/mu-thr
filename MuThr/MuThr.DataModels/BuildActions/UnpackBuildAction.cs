using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

public class UnpackBuildAction : BuildAction
{
    public required string SourcePath { get; set; }
    public required BuildAction DerivedAction { get; set; }

    protected override Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
    {
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        IArrayDataPoint? sourceDataArray = environment.GetDataPoint<IArrayDataPoint>(sourcePath);
        if (sourceDataArray == null)
            return Task.FromResult(Error($"Can't find array at data path {sourcePathFull}"));

        logger.Information("Creating {count} derived tasks of type {type} from data at {path}", sourceDataArray.Get().Length, DerivedAction.GetType().Name, sourcePathFull);
        DerivedTask[] derivedTasks = [.. sourceDataArray.Get().Select(src => new DerivedTask(DerivedAction, src, sourcePathFull))];
        return Task.FromResult(Derive(derivedTasks));
    }
}
