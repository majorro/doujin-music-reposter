using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding;

public class ResilientStream : Stream
{
    private Stream _stream;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ResilientStream(Stream stream, ILogger logger, Func<Task<Stream?>> newStreamFunc)
    {
        _stream = stream;

        _retryPolicy = Policy
            .Handle<IOException>(ex => ex.Message.Contains("The response ended prematurely"))
            .WaitAndRetryAsync(
                retryCount: int.MaxValue,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetryAsync: async (exception, delay, retryCount, _) =>
                {
                    logger.LogWarning(exception, "Failed to read from stream (attempt {RetryCount}), retrying in {Delay}s", retryCount, delay.TotalSeconds);
                    _stream = await newStreamFunc() ?? throw new InvalidOperationException("Failed to create new stream");
                });
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ctk)
    {
        await _retryPolicy.ExecuteAsync(async token =>
        {
            try
            {
                await _stream.CopyToAsync(destination, bufferSize, token);
            }
            catch (IOException)
            {
                await _stream.DisposeAsync();
                destination.SetLength(0);
                throw;
            }
        }, ctk);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ctk = default)
    {
        return await _retryPolicy.ExecuteAsync(async token =>
        {
            try
            {
                return await _stream.ReadAsync(buffer, token);
            }
            catch (IOException)
            {
                await _stream.DisposeAsync();
                throw;
            }
        }, ctk);
    }

    #region Disposal

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