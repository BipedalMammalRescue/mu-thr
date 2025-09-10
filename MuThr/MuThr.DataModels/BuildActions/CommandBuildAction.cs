using System.Diagnostics;

namespace MuThr.DataModels.BuildActions;

/// <summary>
/// Run a shell command as a build action. Input is translated to 
/// </summary>
public class CommandBuildAction : BuildAction
{
    public required string Process { get; set; }
    public required string[] Arguments { get; set; }

    protected override async Task<ProtoBuildResult> ExecuteCoreAsync(BuildEnvironment environment, Stream input, Stream output)
    {
        using Process builder = new();

        builder.StartInfo.FileName = environment.ExpandValues(Process);
        foreach (string arg in Arguments)
        {
            builder.StartInfo.ArgumentList.Add(environment.ExpandValues(arg));
        }

        // redirect both input and output // TODO: we need a logger so stderr can be logged
        builder.StartInfo.RedirectStandardInput = true;
        builder.StartInfo.RedirectStandardOutput = true;

        // run the builder
        builder.Start();
        await input.CopyToAsync(builder.StandardInput.BaseStream).ConfigureAwait(false);
        builder.StandardInput.Close();
        await builder.StandardOutput.BaseStream.CopyToAsync(output).ConfigureAwait(false);
        await builder.WaitForExitAsync().ConfigureAwait(false);

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
