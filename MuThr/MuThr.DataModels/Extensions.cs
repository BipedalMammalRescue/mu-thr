using MuThr.DataModels.Schema;

namespace MuThr.DataModels;

public static class Extensions
{
    public static T? GetDataPoint<T>(this IReadOnlyDictionary<string, IDataPoint> source, string key) where T : IDataPoint
    {
        if (!source.TryGetValue(key, out IDataPoint? value))
            return default;

        if (value is not T castValue)
            return default;

        return castValue;
    }
}
