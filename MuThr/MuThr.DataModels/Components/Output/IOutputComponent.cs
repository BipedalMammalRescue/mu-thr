using System.Text.Json.Serialization;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Output;

[JsonPolymorphic]
[JsonDerivedType(typeof(FileOutput), typeDiscriminator: "file")]
[JsonDerivedType(typeof(BinaryPrintOutput), typeDiscriminator: "binary")]
[JsonDerivedType(typeof(ConcatOutput), typeDiscriminator: "concat")]
public interface IOutputComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger);
}
