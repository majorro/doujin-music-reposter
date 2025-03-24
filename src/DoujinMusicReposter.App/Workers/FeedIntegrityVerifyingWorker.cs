using System.Text.Json;
using System.Threading.Channels;
using DoujinMusicReposter.Persistence;
using DoujinMusicReposter.Telegram.Services;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using Microsoft.Extensions.Options;

namespace DoujinMusicReposter.App.Workers;

internal class FeedIntegrityVerifyingWorker(
    ILogger<FeedIntegrityVerifyingWorker> logger,
    ChannelWriter<VkPostDto> channelWriter,
    SemaphoreSlim semaphore,
    IVkApiClient vkClient,
    PostsManagingService postsManager,
    PostsRepository postsDb) : BackgroundService
{
    private const int PeriodDays = 3; // TODO: to config + define via crontab with https://github.com/atifaziz/NCrontab/
    private static readonly int[] SkipIds = [47884]; // TODO: to config

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var isFirstRun = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            int[] idsToRemove;

            await semaphore.WaitAsync(stoppingToken);
            try
            {
                logger.LogInformation("Started feed integrity verification");

                var vkPosts = await ReadPostsAsync(isFirstRun ? 100 : null);

                var dbPostIds = postsDb.GetAllVkIds().ToHashSet();
                idsToRemove = isFirstRun
                    ? []
                    : dbPostIds.Except(vkPosts.Select(x => x.Id)).ToArray();

                var unpostedPosts = vkPosts
                    .Where(x =>
                        !SkipIds.Contains(x.Id) &&
                        !x.IsDonut &&
                        !dbPostIds.Contains(x.Id))
                    .ToArray();
                logger.LogInformation("Found {Count} unposted posts", vkPosts.Length);

                await PublishPostsAsync(unpostedPosts, stoppingToken);
            }
            finally
            {
                semaphore.Release();
            }

            logger.LogInformation("Found {Count} posts to remove", idsToRemove.Length);
            await RemovePostsAsync(idsToRemove);

            logger.LogInformation("Finished feed integrity verification");
            isFirstRun = false;
            await Task.Delay(TimeSpan.FromDays(PeriodDays), stoppingToken);
        }
    }

    private async Task PublishPostsAsync(IEnumerable<VkPostDto> posts, CancellationToken ctk)
    {
        foreach (var post in posts)
        {
            var response = await vkClient.GetCommentsAsync(post.Id, count: 5);

            var audioArchives = response.Data!.Comments
                .Where(x => x.IsFromAuthor)
                .SelectMany(x => x.AudioArchives)
                .ToArray();
            post.AudioArchives.AddRange(audioArchives);
            if (audioArchives.Length > 0)
                logger.LogInformation("Added {Count} audio archives to PostId={PostId}", audioArchives.Length, post.Id);

            await channelWriter.WriteAsync(post, ctk);
            logger.LogInformation("Sent PostId={PostId} to posting queue", post.Id);

            await Task.Delay(TimeSpan.FromSeconds(1), ctk);
        }
    }

    private async Task RemovePostsAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            var tgIds = postsDb.GetByVkId(id);
            if (tgIds is null)
            {
                logger.LogWarning("Unable to remove PostId={PostId}: wasn't posted?", id);
                continue;
            }

            await postsManager.DeleteMessagesAsync(tgIds);
            postsDb.RemoveByVkId(id);
        }
    }

    private async Task<VkPostDto[]> ReadPostsAsync(int? limit = null)
    {
        var totalPosts = limit;
        var currentOffset = 0;
        var result = new List<VkPostDto>();

        do
        {
            var response = await vkClient.GetPostsAsync(currentOffset);

            result.AddRange(response.Data!.Posts);
            totalPosts ??= response.Data!.TotalCount;
            currentOffset += response.Data!.Posts.Count;
            if (500 - currentOffset % 500 < 100)
                logger.LogInformation("Read {Count}/{Total} posts", currentOffset, totalPosts);
        } while (currentOffset < totalPosts);

        logger.LogInformation("Read {Count} posts", result.Count);

        result.Reverse();

        return result.DistinctBy(x => x.Id).ToArray(); // vk moment
    }
}