using System.Threading.Channels;

namespace DoujinMusicReposter.Telegram;

public class PostingService : BackgroundService
{
    private readonly ILogger<PostingService> _logger;
    private readonly Channel<Task> _taskQueue;

    public PostingService(ILogger<PostingService> logger)
    {
        _logger = logger;

        _taskQueue = Channel.CreateUnbounded<Task>(new UnboundedChannelOptions
        {
            SingleReader = false, // TODO: ?
            SingleWriter = true
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}