using System.Threading.Channels;
using DoujinMusicReposter.App.Services.Interfaces;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Services;

namespace DoujinMusicReposter.App.Services;

internal class FeedIntegrityVerifyingService(
    ILogger<FeedIntegrityVerifyingService> logger,
    ChannelWriter<VkPostDto> channelWriter,
    IVkApiClient vkClient,
    VkPostPreparingService postPreparer,
    IPostsManagingService postsManager,
    IPostsRepository postsDb) : IFeedIntegrityVerifyingService
{
    private static readonly int[] SkipIds = [ // TODO: to config
        47884, // dj things
    ];
    private const int RecentPostsLimit = 100;

    public async Task PublishNewPostsAsync(bool onlyRecent = false, CancellationToken ctk = default)
    {
        var vkPosts = await FetchPostsAsync(onlyRecent ? RecentPostsLimit : null);

        var dbPostIds = postsDb.GetAllVkIds().ToHashSet();

        var unpostedPosts = vkPosts
            .Where(x =>
                !SkipIds.Contains(x.Id) &&
                !x.IsDonut &&
                !dbPostIds.Contains(x.Id))
            .ToArray();
        logger.LogInformation("Found {Count} unposted posts", unpostedPosts.Length);

        foreach (var post in unpostedPosts)
            await postPreparer.PrepareAsync(post, ctk);
        logger.LogInformation("Prepared {Count} posts", unpostedPosts.Length);

        await PublishPostsAsync(unpostedPosts, ctk);

        if (!onlyRecent)
        {
            var idsToDelete = dbPostIds.Except(vkPosts.Select(x => x.Id)).ToArray();
            logger.LogInformation("Found {Count} posts to delete", idsToDelete.Length);
            await DeletePostsAsync(idsToDelete);
        }
    }

    private async Task PublishPostsAsync(IEnumerable<VkPostDto> posts, CancellationToken ctk)
    {
        foreach (var post in posts)
        {
            await channelWriter.WriteAsync(post, ctk);
            logger.LogInformation("Sent PostId={PostId} to posting queue", post.Id);

            await Task.Delay(TimeSpan.FromSeconds(1), ctk);
        }
    }

    private async Task DeletePostsAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            var tgIds = postsDb.GetByVkId(id);
            if (tgIds is null)
            {
                logger.LogWarning("Unable to delete PostId={PostId}: wasn't posted?", id);
                continue;
            }

            await postsManager.DeleteMessagesAsync(tgIds);
            postsDb.RemoveByVkId(id);
        }
    }

    private async Task<VkPostDto[]> FetchPostsAsync(int? limit = null)
    {
        var totalPosts = limit;
        var currentOffset = 0;
        var result = new List<VkPostDto>();

        do
        {
            var count = Math.Min(100, (totalPosts ?? 100) - currentOffset);
            var response = await vkClient.GetPostsAsync(currentOffset, count);

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