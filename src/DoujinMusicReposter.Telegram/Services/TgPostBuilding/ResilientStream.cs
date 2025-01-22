using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding;

// TODO: use polly if a single retry not enough
public class ResilientStream(Stream stream, ILogger logger, Func<Task<Stream?>> newStreamFunc) : Stream
{
    private Stream _stream = stream;

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ctk)
    {
        try
        {
            await _stream.CopyToAsync(destination, bufferSize, ctk);
        }
        catch (IOException e) when (e.Message.Contains("The response ended prematurely"))
        {
            logger.LogWarning(e, "Failed to read from stream, creating new");
            await _stream.DisposeAsync();
            await Task.Delay(5000, ctk); // TODO: determine delay
            _stream = (await newStreamFunc())!;
            destination.SetLength(0);
            await _stream.CopyToAsync(destination, bufferSize, ctk);
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ctk)
    {
        try
        {
            return await _stream.ReadAsync(buffer, ctk);
        }
        catch (IOException e) when (e.Message.Contains("The response ended prematurely"))
        {
            logger.LogWarning(e, "Failed to read from stream, creating new");
            await _stream.DisposeAsync();
            await Task.Delay(5000, ctk); // TODO: determine delay
            _stream = (await newStreamFunc())!;
            return await _stream.ReadAsync(buffer, ctk);
        }
    }

    #region Disposing

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await _stream.DisposeAsync();
    }

    public sealed override async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Unused
    public override void Flush() => throw new NotImplementedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override void SetLength(long value) => throw new NotImplementedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _stream.Length;
    public override long Position { get => _stream.Position; set => _stream.Position = value; }
    #endregion
}