using System.Threading.Channels;
using DoujinMusicReposter.App.Services;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.DoujinMusicReposter.App;

public class FeedIntegrityVerifyingServiceTests
{
    [Fact]
    public async Task PublishNewPostsAsync_ShouldRequestOnlyTheRemainingRecentPosts()
    {
        var posts = Enumerable.Range(1, 100).Select(id => new VkPostDto { Id = id, Text = "Music" }).ToArray();
        var vkClient = new Mock<IVkApiClient>(MockBehavior.Strict);
        vkClient.Setup(x => x.GetPostsAsync(0, 100))
            .ReturnsAsync(new VkResponse<GetPostsResponse>(new GetPostsResponse(100, posts.Take(99).ToList())));
        vkClient.Setup(x => x.GetPostsAsync(99, 1))
            .ReturnsAsync(new VkResponse<GetPostsResponse>(new GetPostsResponse(100, [posts[^1]])));
        var postsDb = new Mock<IPostsRepository>(MockBehavior.Strict);
        postsDb.Setup(x => x.GetAllVkIds()).Returns(posts.Select(x => x.Id).ToList());
        var queue = Channel.CreateUnbounded<VkPostDto>();
        var service = new FeedIntegrityVerifyingService(
            NullLogger<FeedIntegrityVerifyingService>.Instance,
            queue.Writer,
            vkClient.Object,
            new VkPostPreparingService(vkClient.Object),
            new Mock<IPostsManagingService>(MockBehavior.Strict).Object,
            postsDb.Object);

        await service.PublishNewPostsAsync(onlyRecent: true);

        vkClient.Verify(x => x.GetPostsAsync(0, 100), Times.Once);
        vkClient.Verify(x => x.GetPostsAsync(99, 1), Times.Once);
        vkClient.VerifyNoOtherCalls();
    }
}