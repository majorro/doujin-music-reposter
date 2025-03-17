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
    ChannelWriter<Post> channelWriter,
    SemaphoreSlim semaphore,
    IVkApiClient vkClient,
    PostsManagingService postsManager,
    PostsRepository postsDb) : BackgroundService
{
    private const int PERIOD_DAYS = 3; // TODO: to config
    private static readonly int[] SkipIds = [47884]; // TODO: to config

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            HashSet<int> dbPostIds, vkPostIds;

            await semaphore.WaitAsync(stoppingToken);
            try
            {
                logger.LogInformation("Started feed integrity verification");
                var vkPosts = await ReadAllPostsAsync();
                vkPosts = vkPosts.Where(x => !SkipIds.Contains(x.Id)).ToList();
                logger.LogInformation("Read {Count} posts", vkPosts.Count);

                vkPostIds = vkPosts.Select(p => p.Id).ToHashSet();
                dbPostIds = postsDb.GetAllVkIds().ToHashSet();

                var toAdd = vkPostIds.Except(dbPostIds).ToArray();
                logger.LogInformation("Found {Count} unposted posts", toAdd.Length);
                foreach (var id in toAdd)
                {
                    var post = vkPosts.Single(p => p.Id == id);

                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    var response = await vkClient.GetCommentsAsync(post.Id, count: 5);

                    var audioArchives = response.Data!.Comments
                        .Where(x => x.IsFromAuthor)
                        .SelectMany(x => x.AudioArchives)
                        .ToArray();
                    post.AudioArchives.AddRange(audioArchives);
                    if (audioArchives.Length > 0)
                        logger.LogInformation("Added {Count} audio archives to PostId={PostId}", audioArchives.Length, post.Id);

                    await channelWriter.WriteAsync(post, stoppingToken);
                    logger.LogInformation("Sent PostId={PostId} to posting queue", post.Id);
                }
            }
            finally
            {
                semaphore.Release();
            }

            var toRemove = dbPostIds.Except(vkPostIds).ToArray();
            logger.LogInformation("Found {Count} posts to remove", toRemove.Length);
            var toRemoveTgIds = toRemove.SelectMany(postsDb.GetByVkId!).ToArray();
            await postsManager.DeleteMessagesAsync(toRemoveTgIds);
            Array.ForEach(toRemove, postsDb.RemoveByVkId);

            logger.LogInformation("Finished feed integrity verification");
            await Task.Delay(TimeSpan.FromDays(PERIOD_DAYS), stoppingToken);
        }
    }

    private async Task<List<Post>> ReadAllPostsAsync()
    {
        int? totalPosts = null;
        var currentOffset = 0;
        var result = new List<Post>();

        do
        {
            var response = await vkClient.GetPostsAsync(currentOffset);

            result.AddRange(response.Data!.Posts);
            totalPosts ??= response.Data!.TotalCount;
            currentOffset += response.Data!.Posts.Count;
            if (currentOffset % 500 < 10)
                logger.LogInformation("Read {Count}/{Total} posts", currentOffset, totalPosts);
        } while (currentOffset < totalPosts);

        result.Reverse();

        return result.DistinctBy(x => x.Id).ToList(); // vk moment
    }
}