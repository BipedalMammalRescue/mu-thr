using System.Collections.Immutable;
using MuThr.DataModels.Diagnostic;
using Serilog;
using Serilog.Events;

namespace MuThr.Sdk;

public class MuThrLogger(ImmutableArray<string> channels, ILogger core) : IMuThrLogger
{
    /// <summary>
    /// Create a new logger with additional (more important) channels.
    /// </summary>
    /// <param name="channel"></param>
    /// <remarks>the *last* channel is the one used as primary channel</remarks>
    /// <returns></returns>
    public IMuThrLogger WithChannel(params string[] channel) => new MuThrLogger(channels.AddRange(channel), core);

    public void Write(LogEvent logEvent) => core.ForContext("__channels__", channels).Write(logEvent);
}
