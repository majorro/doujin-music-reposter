using DoujinMusicReposter.App.Services.Interfaces;

namespace DoujinMusicReposter.App.Workers;

internal class FeedIntegrityVerifyingWorker(
    ILogger<FeedIntegrityVerifyingWorker> logger,
    IFeedIntegrityVerifyingService feedIntegrityVerifier,
    SemaphoreSlim semaphore) : BackgroundService
{
    private const int PeriodDays = 3; // TODO: to config + define via crontab with https://github.com/atifaziz/NCrontab/

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isFirstRun = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            await semaphore.WaitAsync(stoppingToken);
            try
            {
                logger.LogInformation("Started feed integrity verification");

                await feedIntegrityVerifier.PublishNewPostsAsync(isFirstRun, stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }

            logger.LogInformation("Finished feed integrity verification");
            isFirstRun = false;
            await Task.Delay(TimeSpan.FromDays(PeriodDays), stoppingToken);
        }
    }
}