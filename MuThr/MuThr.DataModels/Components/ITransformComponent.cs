using System.Text.Json.Serialization;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components;

[JsonPolymorphic]
[JsonDerivedType(typeof(FileComponent), typeDiscriminator: "file")]
[JsonDerivedType(typeof(BinaryPrintComponent), typeDiscriminator: "binary")]
[JsonDerivedType(typeof(ConcatComponent), typeDiscriminator: "concat")]
[JsonDerivedType(typeof(LengthPrependComponent), typeDiscriminator: "prepend_length")]
[JsonDerivedType(typeof(PassThroughOutput), typeDiscriminator: "pass_through")]
[JsonDerivedType(typeof(CountComponent), typeDiscriminator: "count")]
[JsonDerivedType(typeof(CollectChildrenComponent), typeDiscriminator: "collect_children")]
public interface ITransformComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger);
}
