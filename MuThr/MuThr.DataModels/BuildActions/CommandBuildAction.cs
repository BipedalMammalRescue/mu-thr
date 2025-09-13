using System.Diagnostics;
using MuThr.DataModels.Diagnostic;

namespace MuThr.DataModels.BuildActions;

/// <summary>
/// Run a shell command as a build action. Input is translated to 
/// </summary>
public class CommandBuildAction : BuildAction
{
    public required string Process { get; set; }
    public required string[] Arguments { get; set; }

    protected override async Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output, IMuThrLogger logger)
    {
        logger.Verbose("Setting up builder process.");
        using Process builder = new();
        builder.StartInfo.FileName = environment.ExpandValues(Process);
        foreach (string arg in Arguments)
        {
            builder.StartInfo.ArgumentList.Add(environment.ExpandValues(arg));
        }

        // redirect both input and output
        builder.StartInfo.RedirectStandardInput = true;
        builder.StartInfo.RedirectStandardOutput = true;
        builder.StartInfo.RedirectStandardError = true;
        builder.ErrorDataReceived += (_, e) => logger.Error("Builder err: {e}", e.Data);

        // run the builder
        builder.Start();
        logger.Information("Builder process started: {name} {args} ({pid})", builder.StartInfo.FileName, builder.StartInfo.Arguments, builder.Id);
        await input.CopyToAsync(builder.StandardInput.BaseStream).ConfigureAwait(false);
        builder.StandardInput.Close();
        await builder.StandardOutput.BaseStream.CopyToAsync(output).ConfigureAwait(false);
        await builder.WaitForExitAsync().ConfigureAwait(false);
        logger.Information("Builder exit.");

        // collect results
        if (builder.ExitCode != 0)
        {
            return Error($"Process {Process} exit with code {builder.ExitCode}; args: `{builder.StartInfo.Arguments}`");
        }
        else
        {
            return Result([]);
        }
    }
}
