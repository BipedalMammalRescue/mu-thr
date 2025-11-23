using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.BuildActions;

public class BypassBuildAction : BuildAction
{
    public override Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger) => Task.FromResult(new ProtoBuildResult(System.Collections.Immutable.ImmutableDictionary<string, string>.Empty, [], []));
}
