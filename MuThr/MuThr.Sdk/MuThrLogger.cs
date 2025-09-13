using System.Collections.Immutable;
using MuThr.DataModels.Diagnostic;
using Serilog;
using Serilog.Events;

namespace MuThr.Sdk;

public class MuThrLogger(ImmutableArray<string> channels, ILogger core) : IMuThrLogger
{
    public IMuThrLogger WithChannel(params string[] newChannel) => new MuThrLogger([..newChannel], core);

    public IMuThrLogger AddChannel(params string[] newChannel) => new MuThrLogger(channels.AddRange(newChannel), core);

    public IMuThrLogger ForTask(Guid id) => new MuThrLogger(channels, core.ForContext("TaskId", id));

    public void Write(LogEvent logEvent) => core.ForContext("__channels__", channels).Write(logEvent);
}
