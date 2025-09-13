namespace MuThr.DataModels.Schema;

public interface IObjDataPoint : IDataPoint
{
    IDataPoint? Get(string path);
}
