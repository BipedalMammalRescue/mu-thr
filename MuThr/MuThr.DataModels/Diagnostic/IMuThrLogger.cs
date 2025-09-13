using Serilog;

namespace MuThr.DataModels.Diagnostic;

public interface IMuThrLogger : ILogger
{
    IMuThrLogger WithChannel(params string[] channel);
    IMuThrLogger ForTask(Guid id) => WithChannel(id.ToString());
}
