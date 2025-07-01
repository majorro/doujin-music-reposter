using System.Threading.Channels;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Http.Exceptions;
using DoujinMusicReposter.Vk.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DoujinMusicReposter.Vk;

public class UpdateTrackingWorker(
    ILogger<UpdateTrackingWorker> logger,
    ChannelWriter<VkPostDto> channelWriter,
    SemaphoreSlim semaphore,
    IVkApiClient vkClient,
    IPostsRepository postsDb) : BackgroundService
{
    private LongPollingServerConfigDto _serverConfig = null!;

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

            posts = posts
                .Where(x =>
                    postsDb.GetByVkId(x.Id) is null && // there can be multiple updates for a single post TODO: check polling (it should be good but stilll)
                    !x.IsDonut)
                .ToList();

            logger.LogInformation("Got {Count} new posts", posts.Count);

            await semaphore.WaitAsync(ctk);
            try
            {
                foreach (var post in posts)
                {
                    if (post.VkAudioArchives.Count < 2) // older posts may have more than a single attachment
                    {
                        // TODO: sub for comment event?
                        var count = await _commentPollingPipeline.ExecuteAsync(async (p, _) => await PreparePostAsync(p), post, ctk);
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
    private async Task<int> PreparePostAsync(VkPostDto post)
    {
        var vkComments = await vkClient.GetCommentsAsync(post.Id, count: 5);
        var authorComments = vkComments.Data!.Comments.Where(x => x.IsFromAuthor).ToArray();

        var vkCommentsAudioArchives = authorComments
            .SelectMany(x => x.AudioArchives)
            .ToArray();
        post.VkAudioArchives.AddRange(vkCommentsAudioArchives);

        var postPixelDrainAudioArchives = LinkExtractor
            .GetPixeldrainLinks(post.Text)
            .Select(x => new PixelDrainAudioArchiveDto(new Uri(x)));
        post.PixelDrainAudioArchives.AddRange(postPixelDrainAudioArchives);

        var commentsPixelDrainAudioArchives = authorComments
            .SelectMany(x => LinkExtractor.GetPixeldrainLinks(x.Text ?? ""))
            .Select(x => new PixelDrainAudioArchiveDto(new Uri(x)));
        post.PixelDrainAudioArchives.AddRange(commentsPixelDrainAudioArchives);

        return vkCommentsAudioArchives.Length;
    }

    private async Task UpdateServerConfigAsync()
    {
        var response = await vkClient.GetLongPollServerAsync();

        _serverConfig = response.Data!.Config;
        logger.LogInformation("Updated server config: Ts={Ts}", _serverConfig.Timestamp);
    }

    private async Task<List<VkPostDto>> GetUpdatesAsync(CancellationToken ctk = default)
    {
        var response = await vkClient.GetNewEvents(_serverConfig, ctk);
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