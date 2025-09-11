using System.Collections.Immutable;

namespace MuThr.DataModels.BuildActions;

public record BuildError;

public record BuildErrorMessage(string Message) : BuildError
{
    public override string ToString() => Message;
}

public record BuildException(Exception Exception) : BuildError
{
    public override string ToString() => $"{Exception.GetType().Name}: {Exception.Message}";
}

public class BuildResult
{
    public ImmutableDictionary<string, string> Tags { get; set; } = ImmutableDictionary<string, string>.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public BuildError[] Errors { get; set; } = [];
}
