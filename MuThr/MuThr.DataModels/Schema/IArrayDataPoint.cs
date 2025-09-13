namespace MuThr.DataModels.Schema;

public interface IArrayDataPoint : IDataPoint
{
    IDataPoint[] Get();
}