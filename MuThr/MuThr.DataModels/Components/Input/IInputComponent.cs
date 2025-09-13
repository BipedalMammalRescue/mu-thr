using System.Text.Json.Serialization;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.BuildActions.Components.Input;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.Components.Input;

[JsonPolymorphic]
[JsonDerivedType(typeof(ConcatInput), typeDiscriminator: "concat")]
[JsonDerivedType(typeof(FileInput), typeDiscriminator: "file")]
public interface IInputComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination, IMuThrLogger logger);
}
