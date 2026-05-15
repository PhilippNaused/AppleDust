namespace AppleDust.Cli;

internal sealed class RpcException(string text, string remoteStackTrace) : Exception(text)
{
    public string RemoteStackTrace => remoteStackTrace;
}
