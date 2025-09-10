namespace MuThr.DataModels;

public class TextAsset
{
    public required string Module { get; set; }
    public required string Asset { get; set; }
    public required TypedProperty[] Properties { get; set; }
}
