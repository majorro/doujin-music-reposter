using System.Threading.Channels;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Http.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DoujinMusicReposter.Vk;

public class UpdateTrackingWorker(
    ILogger<UpdateTrackingWorker> logger,
    ChannelWriter<Post> channelWriter,
    SemaphoreSlim semaphore,
    IVkApiClient apiClient) : BackgroundService
{
    private LongPollingServerConfig _serverConfig = null!;

    private readonly ResiliencePipeline<int> _commentPollingPipeline = new ResiliencePipelineBuilder<int>()
        .AddRetry(new RetryStrategyOptions<int>
        {
            Name = "NewPostCommentPoll",
            MaxRetryAttempts = 4,
            BackoffType = DelayBackoffType.Constant,
            Delay = TimeSpan.FromSeconds(15),
            ShouldHandle = new PredicateBuilder<int>().HandleResult(x => x == 0)
        })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken ctk)
    {
        await UpdateServerConfigAsync();
        while (!ctk.IsCancellationRequested)
        {
            var posts = await GetUpdatesAsync(ctk);
            if (posts.Count == 0)
                continue;

            logger.LogInformation("Got {Count} new posts", posts.Count);

            await semaphore.WaitAsync(ctk);
            try
            {
                foreach (var post in posts)
                {
                    if (post.AudioArchives.Count < 2) // fresh archives should be valid
                    {
                        // TODO: sub for comment event?
                        var count = await _commentPollingPipeline.ExecuteAsync(async (p, _) => await AddArchivesFromComments(p), post, ctk);
                        logger.LogInformation("Added {Count} audio archives to PostId={PostId}", count, post.Id);
                    }

                    await channelWriter.WriteAsync(post, ctk);
                    logger.LogInformation("Sent PostId={PostId} to posting queue", post.Id);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    // TODO: same as in fiv worker, move to some helper?
    private async Task<int> AddArchivesFromComments(Post post)
    {
        var response = await apiClient.GetCommentsAsync(post.Id, count: 5);

        var audioArchives = response.Data!.Comments
            .Where(x => x.IsFromAuthor)
            .SelectMany(x => x.AudioArchives)
            .Where(x => post.AudioArchives.All(y => x.FileName != y.FileName)) // for accidental duplicates
            .ToArray();
        post.AudioArchives.AddRange(audioArchives);

        return audioArchives.Length;
    }

    private async Task UpdateServerConfigAsync()
    {
        var response = await apiClient.GetLongPollServerAsync();

        _serverConfig = response.Data!.Config;
        logger.LogInformation("Updated server config: Ts={Ts}", _serverConfig.Timestamp);
    }

    private async Task<List<Post>> GetUpdatesAsync(CancellationToken ctk = default)
    {
        var response = await apiClient.GetNewEvents(_serverConfig, ctk);
        if (response.IsSuccess)
        {
            _serverConfig.Timestamp = response.Data!.Timestamp;
            return response.Data.Posts;
        }
        else if (response.ErrorCode is 2 or 3)
        {
            await UpdateServerConfigAsync();
            return [];
        }
        else
        {
            throw new VkApiException($"Failed to get {nameof(GetNewEventsResponse)}: {response}");
        }
    }
}