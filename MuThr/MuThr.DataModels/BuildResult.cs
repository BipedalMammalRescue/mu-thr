using System.Collections.Immutable;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels;

public record BuildError;

public record BuildErrorMessage(string Message) : BuildError
{
    public override string ToString() => Message;
}

public record BuildException(Exception Exception) : BuildError
{
    public override string ToString() => $"{Exception.GetType().Name}: {Exception.Message}";
}

public record DerivedTask(BuildAction Action, IDataPoint Data, string PathPrefix);

public class BuildResult
{
    public ImmutableDictionary<string, string> Tags { get; set; } = ImmutableDictionary<string, string>.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public BuildError[] Errors { get; set; } = [];
    public DerivedTask[] DerivedTasks { get; set; } = [];
}
