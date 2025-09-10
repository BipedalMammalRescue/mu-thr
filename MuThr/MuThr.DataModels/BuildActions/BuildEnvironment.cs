using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MuThr.DataModels.BuildActions;

public partial record BuildEnvironment(IDictionary<string, string> SourceData, ImmutableArray<BuildResult> ChildrenResults)
{
    [GeneratedRegex("#\\((?<fieldref>\\w+)\\)")]
    private partial Regex GetSourceReference();

    [GeneratedRegex("#(?<id>[0-9]+):(?<tag>\\w+)")]
    private partial Regex GetTagReference();

    [GeneratedRegex("#(?<id>[0-9]+)")]
    private partial Regex GetOutputReference();

    public string ExpandValues(string source)
    {
        // expand env var
        string result = Environment.ExpandEnvironmentVariables(source);

        // expand source fields from the asset file
        result = GetSourceReference().Replace(result, match =>
        {
            if (!match.Success)
                return result;

            string field = match.Groups["fieldref"].Value;
            if (!SourceData.TryGetValue(field, out string? foundValue))
                return result;

            return foundValue;
        });

        // expand child tags
        result = GetTagReference().Replace(result, match =>
        {
            int childIndex = int.Parse(match.Groups["id"].Value);
            string tag = match.Groups["tag"].Value;
            return ChildrenResults[childIndex].Tags[tag];
        });

        // expand child results (tags have been taken out already so there should be safe to use a subpattern)
        result = GetOutputReference().Replace(result, match =>
        {
            int childIndex = int.Parse(match.Groups["id"].Value);
            return ChildrenResults[childIndex].OutputPath;
        });

        return result;
    }
}
