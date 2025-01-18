using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding;

public class ResilientStream(ILogger logger, Stream stream) : Stream
{
    private readonly ResiliencePipeline _retryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            Name = "StreamReadRetry",
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(2),
            ShouldHandle = new PredicateBuilder().Handle<IOException>(x => x.Message.Contains("The response ended prematurely")),
            OnRetry = response =>
            {
                logger.LogWarning("Failed to read from stream: {Error}, retrying...", response.Outcome.Result);
                return default;
            }
        })
        .Build();

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken ctk) =>
        await _retryPipeline.ExecuteAsync(
            async (ctx, ct) => await stream.CopyToAsync(ctx.Destination, ctx.BufferSize, ct),
            new
            {
                Destination = destination,
                BufferSize = bufferSize,
            },
            ctk);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ctk) =>
        await _retryPipeline.ExecuteAsync(
            async (buf, ct) => await stream.ReadAsync(buf, ct),
            buffer,
            ctk);

    #region Unused
    public override void Flush() => throw new NotImplementedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override void SetLength(long value) => throw new NotImplementedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => stream.Length;
    public override long Position { get => stream.Position; set => stream.Position = value; }
    #endregion
}