using System.Collections.Immutable;
using System.Text.Json.Serialization;
using MuThr.DataModels.Components.Input;
using MuThr.DataModels.Components.Output;
using MuThr.DataModels.Diagnostic;
using MuThr.DataModels.Schema;

namespace MuThr.DataModels.BuildActions;

[JsonPolymorphic]
[JsonDerivedType(typeof(CommandBuildAction), typeDiscriminator: "command")]
public abstract partial class BuildAction
{
    protected record ProtoBuildResult(ImmutableDictionary<string, string> Tags, BuildError[] Errors);
    protected static ProtoBuildResult Result(Dictionary<string, string> tags) => new(tags.ToImmutableDictionary(), []);
    protected static ProtoBuildResult Error(params string[] errors) => new(ImmutableDictionary<string, string>.Empty, [.. errors.Select(e => new BuildErrorMessage(e))]);
    protected static ProtoBuildResult Error(params Exception[] errors) => new(ImmutableDictionary<string, string>.Empty, [.. errors.Select(e => new BuildException(e))]);

    public BuildAction[] ChildTasks { get; set; } = [];

    // transforms the input
    public IInputComponent[] InputComponents { get; set; } = [];
    public IOutputComponent[] OutputComponents { get; set; } = [];

    // used to generate extra tags
    public Dictionary<string, string> Tags { get; set; } = [];

    public IEnumerable<BuildAction> CollectChildren(IDataPoint sourceData, IMuThrLogger logger) => [.. ChildTasks, .. GetDynamicChildren(sourceData, logger)];

    public async Task<BuildResult> ExecuteAsync(BuildEnvironment environment, IMuThrLogger logger)
    {
        HashSet<string> tempFilesUsed = [];

        // transform the input
        logger.Verbose("Transforming input.");
        Stream input = Stream.Null;
        foreach (IInputComponent inputComponent in InputComponents)
        {
            // make sure every file used for input is automatically collected
            string tempFile = Path.GetTempFileName();
            tempFilesUsed.Add(tempFile);

            // transform data
            Stream nextInput = File.Create(tempFile);
            await inputComponent.TransformAsync(environment, input, nextInput, logger).ConfigureAwait(false);

            // swap
            input.Dispose();
            input = nextInput;

            // seek back to beginning for the resulting new input
            input.Seek(0, SeekOrigin.Begin);
        }
        logger.Verbose("Input transformed.");

        // dump output into a temp file
        logger.Verbose("Executing core action.");
        string outputPath = Path.GetTempFileName();
        FileStream output = File.Create(outputPath);
        ProtoBuildResult protoResult = await ExecuteCoreAsync(environment, input, output, logger).ConfigureAwait(false);
        logger.Verbose("Core action exit.");

        // close the files used just now
        await input.DisposeAsync().ConfigureAwait(false);
        await output.DisposeAsync().ConfigureAwait(false);

        // transform the output
        logger.Verbose("Transforming output.");
        foreach (IOutputComponent outputComponent in OutputComponents)
        {
            // get a new file
            string nextOutputPath = Path.GetTempFileName();

            // funnel the output to the next file
            await using FileStream prevOutput = File.OpenRead(outputPath);
            await using FileStream nextOutput = File.Create(outputPath);
            await outputComponent.TransformAsync(environment, prevOutput, nextOutput, logger).ConfigureAwait(false);

            // keep tap and swap
            tempFilesUsed.Add(outputPath);
            outputPath = nextOutputPath;
        }
        logger.Verbose("Output transformed.");

        // clean up
        logger.Verbose("Cleaning up temp files.");
        foreach (string tempFile in tempFilesUsed)
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        logger.Verbose("Temp files cleaned.");

        // add in the extra tags
        var tags = protoResult.Tags;
        foreach (KeyValuePair<string, string> extraTag in Tags)
        {
            tags = protoResult.Tags.Add(environment.ExpandValues(extraTag.Key), environment.ExpandValues(extraTag.Value));
        }

        return new BuildResult()
        {
            Errors = protoResult.Errors,
            OutputPath = outputPath,
            Tags = tags
        };
    }

    /// <summary>
    /// Implement this method (and <see cref="CountDynamicChildren"/>) to generate extra children based on input and environment.
    /// </summary>
    /// <param name="environment">The environment of this task.</param>
    /// <param name="logger"></param>
    /// <returns></returns>
    protected virtual BuildAction[] GetDynamicChildren(IDataPoint sourceData, IMuThrLogger logger) => [];

    /// <summary>
    /// Implement this method for a type of build action. 
    /// </summary>
    /// <param name="input">The finalized input to this task.</param>
    /// <param name="output">The initial output of this task.</param>
    /// <param name="environment">Helper functions that's based on part of the input data.</param>
    /// <returns></returns>
    protected abstract Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger);
}
