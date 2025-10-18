using System.Text.Json.Serialization;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Input;

[JsonPolymorphic]
[JsonDerivedType(typeof(ConcatInput), typeDiscriminator: "concat")]
[JsonDerivedType(typeof(EnumerateConcatInput), typeDiscriminator: "enum_concat")]
[JsonDerivedType(typeof(FileInput), typeDiscriminator: "file")]
[JsonDerivedType(typeof(BinaryPrintInput), typeDiscriminator: "binary")]
public interface IInputComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger);
}
