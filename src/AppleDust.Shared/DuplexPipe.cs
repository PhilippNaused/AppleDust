using System.Text;

namespace AppleDust.Shared;

internal sealed class DuplexPipe : IDisposable
{
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public DuplexPipe(Stream inPipe, Stream outPipe)
    {
        var encoding = new UTF8Encoding(false);
        _reader = new StreamReader(inPipe, encoding);
        _writer = new StreamWriter(outPipe, encoding) { AutoFlush = true };
    }

    public DuplexPipe(StreamReader reader, StreamWriter writer)
    {
        _reader = reader;
        _writer = writer;
        _writer.AutoFlush = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
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
