using System.Collections.Immutable;
using System.Text.RegularExpressions;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

public partial record BuildEnvironment(IDataPoint SourceData, ImmutableArray<BuildResult> ChildrenResults, string PathPrefix)
{
    [GeneratedRegex("#\\((?<fieldref>\\w+)\\)")]
    private partial Regex GetSourceReference();

    [GeneratedRegex("#(?<id>[0-9]+):(?<tag>\\w+)")]
    private partial Regex GetTagReference();

    [GeneratedRegex("#(?<id>[0-9]+)")]
    private partial Regex GetOutputReference();

    public T? GetDataPoint<T>(string path) where T : IDataPoint
    {
        string[] segments = path.Split(':');
        IDataPoint? lastSource = segments.Aggregate<string, IDataPoint?>(SourceData, (old, key) => old switch
        {
            null => null,
            ILeafDataPoint leaf => null,
            IArrayDataPoint arr => (int.TryParse(key, out int intKey) && intKey < arr.Get().Length && intKey >= 0) ? arr.Get()[intKey] : null,
            IObjDataPoint obj => obj.Get(key),
            _ => null
        });

        return lastSource switch
        {
            T castValue => castValue,
            _ => default
        };
    }

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
            ILeafDataPoint fieldData = GetDataPoint<ILeafDataPoint>(field) ?? throw new Exception($"Single data point at path {PathPrefix}:{field} not found.");
            return fieldData.GetString();
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

    public string GetFullPath(string suffix) => $"{PathPrefix}:{suffix}";
}
