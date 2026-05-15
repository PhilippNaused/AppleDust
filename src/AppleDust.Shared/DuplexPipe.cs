using System.IO.Pipes;
using System.Text;

namespace AppleDust.Shared;

internal interface IDuplexPipe : IDisposable
{
    Task WriteLineAsync(string line, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
}

internal abstract class DuplexPipeBase : IDuplexPipe
{
    protected readonly PipeStream _inPipe;
    protected readonly PipeStream _outPipe;
    protected readonly StreamReader _reader;
    protected readonly StreamWriter _writer;

    protected DuplexPipeBase(PipeStream inPipe, PipeStream outPipe)
    {
        _inPipe = inPipe;
        _outPipe = outPipe;
        var encoding = new UTF8Encoding(false);
        _reader = new StreamReader(_inPipe, encoding);
        _writer = new StreamWriter(_outPipe, encoding) { AutoFlush = true };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _inPipe.Dispose();
        _outPipe.Dispose();
    }

    public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
#if NETCOREAPP
        return _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
#else
        return _writer.WriteLineAsync(line);
#endif
    }

    public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
#if NETCOREAPP
        return _reader.ReadLineAsync(cancellationToken).AsTask();
#else
        return _reader.ReadLineAsync();
#endif
    }
}

internal sealed class DuplexClient : DuplexPipeBase
{
    public static DuplexClient FromHandles(string inPipeHandle, string outPipeHandle)
    {
        var inPipe = new AnonymousPipeClientStream(PipeDirection.In, inPipeHandle);
        var outPipe = new AnonymousPipeClientStream(PipeDirection.Out, outPipeHandle);
        return new DuplexClient(inPipe, outPipe);
    }

    private DuplexClient(AnonymousPipeClientStream inPipe, AnonymousPipeClientStream outPipe)
        : base(inPipe, outPipe)
    {
    }
}

internal sealed class DuplexServer : DuplexPipeBase
{
    public static DuplexServer Create(HandleInheritability inheritability)
    {
        var inPipe = new AnonymousPipeServerStream(PipeDirection.In, inheritability);
        var outPipe = new AnonymousPipeServerStream(PipeDirection.Out, inheritability);
        return new DuplexServer(inPipe, outPipe);
    }

    private DuplexServer(AnonymousPipeServerStream inPipe, AnonymousPipeServerStream outPipe)
        : base(inPipe, outPipe)
    {
    }

    public (string InHandle, string OutHandle) GetClientHandles()
    {
        return (InHandle: ((AnonymousPipeServerStream)_inPipe).GetClientHandleAsString(),
                OutHandle: ((AnonymousPipeServerStream)_outPipe).GetClientHandleAsString());
    }

    public void DisposeLocalCopyOfClientHandles()
    {
        ((AnonymousPipeServerStream)_inPipe).DisposeLocalCopyOfClientHandle();
        ((AnonymousPipeServerStream)_outPipe).DisposeLocalCopyOfClientHandle();
    }
}
