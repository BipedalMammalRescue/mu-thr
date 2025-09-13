using System.Collections.Immutable;

namespace MuThr.DataModels;

public class BuildResult
{
    public ImmutableDictionary<string, string> Tags { get; set; } = ImmutableDictionary<string, string>.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public BuildError[] Errors { get; set; } = [];
    public string[] DerivedTasks { get; set; } = [];
}
