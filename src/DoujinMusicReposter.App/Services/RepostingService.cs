using System.Threading.Channels;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Telegram.Services.Models;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Exceptions;
using DoujinMusicReposter.Vk.Services;

namespace DoujinMusicReposter.App.Services;

internal class RepostingService(
    ILogger<RepostingService> logger,
    IVkApiClient vkClient,
    VkPostPreparingService postPreparer,
    IPostsManagingService postsManager,
    IPostsRepository postsDb,
    ChannelWriter<VkPostDto> channelWriter) : IRepostingService
{
    public async Task<IReadOnlyList<RepostResult>> RepostAsync(int[] postIds, CancellationToken ctk = default)
    {
        var results = new List<RepostResult>();
        foreach (var postId in postIds.Distinct())
        {
            ctk.ThrowIfCancellationRequested();
            try
            {
                results.Add(await RepostAsync(postId, ctk));
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to queue repost for PostId={PostId}", postId);
                results.Add(new RepostResult(postId, "Failed; see service logs"));
            }
        }

        return results;
    }

    private async Task<RepostResult> RepostAsync(int postId, CancellationToken ctk)
    {
        var response = await vkClient.GetPostByIdAsync(postId, ctk);
        if (!response.IsSuccess || response.Data is null)
            throw new VkApiException($"Failed to get PostId={postId}: {response}");

        var post = response.Data.Post;
        if (post is null || post.Id != postId)
            return new RepostResult(postId, "Skipped: VK post is unavailable");
        if (post.IsDonut)
            return new RepostResult(postId, "Skipped: Donut post");

        await postPreparer.PrepareAsync(post, ctk);

        var messageIds = postsDb.GetByVkId(postId);
        if (messageIds is null)
        {
            logger.LogWarning("No Telegram mapping for PostId={PostId}; queueing anyway", postId);
        }
        else
        {
            if (messageIds.Length > 0)
                await postsManager.DeleteMessagesAsync(messageIds);
            postsDb.RemoveByVkId(postId);
        }

        await channelWriter.WriteAsync(post);
        logger.LogInformation("Sent PostId={PostId} to reposting queue", postId);
        return new RepostResult(postId, "Queued");
    }
}