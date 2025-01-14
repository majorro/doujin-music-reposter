using System.Text.Json;
using System.Threading.Channels;
using DoujinMusicReposter.Persistence;
using DoujinMusicReposter.Telegram.Services;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace DoujinMusicReposter.App.Workers;

internal class FeedIntegrityVerifyingWorker(
    ILogger<FeedIntegrityVerifyingWorker> logger,
    ChannelWriter<Post> channelWriter,
    SemaphoreSlim semaphore,
    IVkApiClient vkClient,
    PostsManagingService postsManager,
    PostsRepository postsDb,
    TelegramBotClientPoolService botPool,
    IOptions<TgConfig> tgConfig) : BackgroundService
{
    private const int PeriodDays = 7; // TODO: to config

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HashSet<int> dbPostIds, vkPostIds;

                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    logger.LogInformation("Started feed integrity verification");
                    var vkPosts = await ReadAllPostsAsync();
                    // vkPosts = [vkPosts.Single(x => x.Id == 1756)]; // TODO: remove
                    // vkPosts = vkPosts.Skip(869).Take(1000).ToList(); // TODO: remove
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
                foreach (var id in toRemove)
                {
                    var messagesIds = postsDb.GetByVkId(id);
                    await postsManager.DeleteMessagesAsync(messagesIds!.ToArray());
                    postsDb.RemoveByVkId(id);
                }

                logger.LogInformation("Finished feed integrity verification");
                await Task.Delay(TimeSpan.FromDays(PeriodDays), stoppingToken);
            }
        }
        catch (Exception e)
        {
            // TODO: use serilog with tg sink
            var tgClient = botPool.GetClient();
            await tgClient.SendMessage(tgConfig.Value.ChatAdminId, $"ВСЁ В ДЕРЬМЕ:\n{e}", cancellationToken: CancellationToken.None);
            throw;
        }
    }

    private async Task<List<Post>> ReadAllPostsAsync()
    {
        // load from file if possible TODO: remove
        if (File.Exists("posts.json"))
        {
            var jsn = await File.ReadAllTextAsync("posts.json");
            return JsonSerializer.Deserialize<List<Post>>(jsn, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        int? totalPosts = null;
        var currentOffset = 0;
        var result = new List<Post>();

        do
        {
            var response = await vkClient.GetPostsAsync(currentOffset);

            result.AddRange(response.Data!.Posts);
            totalPosts ??= response.Data!.TotalCount;
            currentOffset += response.Data!.Posts.Count;
            if (currentOffset % 500 == 0)
                logger.LogInformation("Read {Count}/{Total} posts", currentOffset, totalPosts);
        } while (currentOffset < totalPosts);

        result.Reverse();

        // save result to file TODO: remove
        var json = JsonSerializer.Serialize(result);
        await File.WriteAllTextAsync("posts.json", json);
        return result;
    }
}