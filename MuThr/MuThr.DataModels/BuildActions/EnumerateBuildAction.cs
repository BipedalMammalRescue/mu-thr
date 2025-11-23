using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

/// <summary>
/// Used to generate a dynamic amount of build actions (of the same type) from a given data path.
/// </summary>
public class EnumerateBuildAction : BypassBuildAction
{
    private class StubbedBuildAction : BuildAction
    {
        public required BuildEnvironment Stub { get; set; }
        public required BuildAction Implementation { get; set; }

        public override BuildAction[] GetDynamicChildren(BuildEnvironment sourceData, IMuThrLogger logger)
        {
            return [..Implementation.ChildTasks, ..Implementation.GetDynamicChildren(sourceData, logger)];
        }

        public override async Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
        {
            return await Implementation.ExecuteCoreAsync(Stub, input, output, logger).ConfigureAwait(false);
        }
    }

    public required string SourcePath { get; set; }
    public required BuildAction Implementation { get; set; }

    // NOTE: might cause a little memory churn, hopefully the build actions and recipes are light enough that it's not gonna be a problem
    public override BuildAction[] GetDynamicChildren(BuildEnvironment environment, IMuThrLogger logger)
    {
        // extract the enumerable from source path
        string sourcePath = environment.ExpandValues(SourcePath);
        string sourcePathFull = environment.GetFullPath(sourcePath);
        IArrayDataPoint? sourceData = environment.GetDataPoint<IArrayDataPoint>(sourcePath) ?? throw new Exception($"Can't find array at data path {sourcePathFull}");

        var outChildren = new BuildAction[sourceData.Get().Length];

        for (int i = 0; i < outChildren.Length; i++)
        {
            BuildEnvironment derivedEnvironment = environment with { SourceData = sourceData.Get()[i] };
            outChildren[i] = new StubbedBuildAction() { Stub = derivedEnvironment, Implementation = Implementation };
        }

        return outChildren;
    }
}
