namespace DoujinMusicReposter.App.Services.Interfaces;

internal interface IFeedIntegrityVerifyingService
{
    Task PublishNewPostsAsync(bool onlyRecent, CancellationToken stoppingToken);
}