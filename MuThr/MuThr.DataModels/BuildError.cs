namespace MuThr.DataModels;

public record BuildError;

public record BuildErrorMessage(string Message) : BuildError
{
    public override string ToString() => Message;
}

public record BuildException(Exception Exception) : BuildError
{
    public override string ToString() => $"{Exception.GetType().Name}: {Exception.Message}";
}