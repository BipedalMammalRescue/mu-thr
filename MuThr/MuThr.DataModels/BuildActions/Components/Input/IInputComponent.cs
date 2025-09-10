using System.Text.Json.Serialization;

namespace MuThr.DataModels.BuildActions.Components.Input;

[JsonPolymorphic]
[JsonDerivedType(typeof(ConcatInput), typeDiscriminator: "concat")]
[JsonDerivedType(typeof(FileInput), typeDiscriminator: "file")]
public interface IInputComponent
{
    Task TransformAsync(BuildEnvironment environment, Stream source, Stream destination);
}
