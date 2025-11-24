using CommunityToolkit.HighPerformance;
using MuThr.DataModels.BuildActions;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.Components;

public class CountComponent : ITransformComponent
{
    public required string SourcePath { get; set; }

    public Task TransformAsync(BuildEnvironment environment, Stream prev, Stream next, IMuThrLogger logger)
    {
        IArrayDataPoint? sourceArray = environment.GetDataPoint<IArrayDataPoint>(SourcePath);
        int length = sourceArray?.Get().Length ?? 0;
        next.Write(length);
        return Task.CompletedTask;
    }
}