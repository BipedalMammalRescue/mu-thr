using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Schema;

namespace MuThr.Sdk;

public interface ITaskProvider
{
    (BuildAction Action, IDataPoint SourceData) CreateTask(string key);
}
