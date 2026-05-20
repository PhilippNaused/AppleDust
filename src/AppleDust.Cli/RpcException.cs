namespace AppleDust.Cli;

internal sealed class RpcException(string message, string remoteErrorMessage) : Exception(message)
{
    public string RemoteErrorMessage { get; } = remoteErrorMessage;
}
