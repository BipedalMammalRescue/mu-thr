using Serilog.Core;
using Serilog.Events;

namespace MuThr.Sdk;

public class MuThrLogEnricher : ILogEventEnricher
{
    private static string? GetChannelName(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("__channels__", out LogEventPropertyValue? propValue)
            && (propValue is SequenceValue seqValue)
            && (seqValue.Elements.Count > 0)
            && (seqValue.Elements[^1] is ScalarValue scalarValue))
        {
            return scalarValue.Value as string;
        }
        else
        {
            return null;
        }
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        string? channel = GetChannelName(logEvent);
        if (channel == null)
            return;
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("PrimaryChannel", channel));
    }
}


