namespace MuThr.DataModels;

public class TypedProperty
{
    public required DataType Type { get; set; }
    public required string Value { get; set; }

    public bool Verify(string assetRoot)
    {
        switch (Type)
        {
            case DataType.Byte:
                return byte.TryParse(Value, out _);
            case DataType.Int32:
                return int.TryParse(Value, out _);
            case DataType.Int64:
                return long.TryParse(Value, out _);
            case DataType.Uint32:
                return uint.TryParse(Value, out _);
            case DataType.Uint64:
                return ulong.TryParse(Value, out _);
            case DataType.Float:
                return float.TryParse(Value, out _);
            case DataType.Vec2:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 2)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Vec3:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 3)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Vec4:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 4)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Mat2:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 4)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Mat3:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 9)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Mat4:
                {
                    string[] segments = [.. Value.Split(" ").Where(x => x.Length > 0)];
                    if (segments.Length != 16)
                        return false;
                    if (segments.Any(x => !float.TryParse(x, out _)))
                        return false;

                    return true;
                }
            case DataType.Path:
                {
                    string fullPath = Path.Combine(assetRoot, Value);
                    return Path.Exists(fullPath);
                }
        }

        return false;
    }
}
