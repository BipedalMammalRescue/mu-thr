namespace MuThr.DataModels.Schema;

public interface ILeafDataPoint : IDataPoint
{
    string GetString();
    byte[] GetBytes();
}
