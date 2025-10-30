using System.Text.Json.Serialization;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Output;

[JsonPolymorphic]
[JsonDerivedType(typeof(FileOutput), typeDiscriminator: "file")]
[JsonDerivedType(typeof(BinaryPrintOutput), typeDiscriminator: "binary")]
[JsonDerivedType(typeof(ConcatOutput), typeDiscriminator: "concat")]
[JsonDerivedType(typeof(LengthPrependOutput), typeDiscriminator: "prepend_length")]
[JsonDerivedType(typeof(PassThroughOutput), typeDiscriminator: "pass_through")]
public interface IOutputComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger);
}
