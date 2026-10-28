using System.Threading.Channels;
using DoujinMusicReposter.App.Services;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.DoujinMusicReposter.App;

public class RepostingServiceTests
{
    private readonly Mock<IVkApiClient> _vkClient = new();
    private readonly Mock<IPostsRepository> _postsDb = new(MockBehavior.Strict);
    private readonly Mock<IPostsManagingService> _postsManager = new(MockBehavior.Strict);
    private readonly Channel<VkPostDto> _queue = Channel.CreateUnbounded<VkPostDto>();
    private readonly RepostingService _service;

    public RepostingServiceTests()
    {
        _postsDb.Setup(x => x.GetByVkId(It.IsAny<int>())).Returns((int[]?)null);
        _vkClient.Setup(x => x.GetCommentsAsync(It.IsAny<int>(), 0, 5, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VkResponse<GetCommentsResponse>(new GetCommentsResponse([])));

        _service = new RepostingService(
            NullLogger<RepostingService>.Instance,
            _vkClient.Object,
            new VkPostPreparingService(_vkClient.Object),
            _postsManager.Object,
            _postsDb.Object,
            _queue.Writer);
    }

    [Fact]
    public async Task RepostAsync_ShouldDeleteExistingPostBeforeQueueingReplacement()
    {
        var post = SetUpPost(101);
        int[] messageIds = [11, 12];
        var sequence = new MockSequence();
        _postsDb.InSequence(sequence).Setup(x => x.GetByVkId(post.Id)).Returns(messageIds);
        _postsManager.InSequence(sequence).Setup(x => x.DeleteMessagesAsync(messageIds)).Returns(Task.CompletedTask);
        _postsDb.InSequence(sequence).Setup(x => x.RemoveByVkId(post.Id))
            .Callback(() => _queue.Reader.Count.Should().Be(0));

        var results = await _service.RepostAsync([post.Id]);

        results.Should().ContainSingle().Which.Status.Should().Be("Queued");
        _queue.Reader.TryRead(out var queuedPost).Should().BeTrue();
        queuedPost.Should().BeSameAs(post);
        _postsManager.Verify(x => x.DeleteMessagesAsync(messageIds), Times.Once);
        _postsDb.Verify(x => x.RemoveByVkId(post.Id), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepostAsync_ShouldQueuePostWithoutTelegramMessages(bool hasMapping)
    {
        var post = SetUpPost(101);
        int[]? messageIds = hasMapping ? [] : null;
        _postsDb.Setup(x => x.GetByVkId(post.Id)).Returns(messageIds);
        if (hasMapping)
            _postsDb.Setup(x => x.RemoveByVkId(post.Id));

        var results = await _service.RepostAsync([post.Id]);

        results.Should().ContainSingle().Which.Status.Should().Be("Queued");
        _queue.Reader.TryRead(out var queuedPost).Should().BeTrue();
        queuedPost.Should().BeSameAs(post);
        _postsManager.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepostAsync_ShouldSkipUnavailableOrDonutPost(bool isDonut)
    {
        VkPostDto? post = isDonut ? new VkPostDto { Id = 101, Text = "Music", IsDonut = true } : null;
        _vkClient.Setup(x => x.GetPostByIdAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VkResponse<GetPostByIdResponse>(new GetPostByIdResponse(post)));

        var results = await _service.RepostAsync([101]);

        results.Should().ContainSingle().Which.Status.Should().StartWith("Skipped:");
        _queue.Reader.Count.Should().Be(0);
        _postsDb.VerifyNoOtherCalls();
        _postsManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RepostAsync_ShouldKeepMappingOnDeletionFailureAndContinueWithNextPost()
    {
        var failedPost = SetUpPost(101);
        var nextPost = SetUpPost(102);
        int[] messageIds = [11, 12];
        _postsDb.Setup(x => x.GetByVkId(failedPost.Id)).Returns(messageIds);
        _postsManager.Setup(x => x.DeleteMessagesAsync(messageIds))
            .ThrowsAsync(new InvalidOperationException("Deletion failed"));

        var results = await _service.RepostAsync([failedPost.Id, nextPost.Id])
            .WaitAsync(TimeSpan.FromSeconds(5));

        results.Select(x => x.Status).Should().Equal("Failed; see service logs", "Queued");
        _queue.Reader.TryRead(out var queuedPost).Should().BeTrue();
        queuedPost.Should().BeSameAs(nextPost);
        _queue.Reader.Count.Should().Be(0);
        _postsDb.Verify(x => x.RemoveByVkId(failedPost.Id), Times.Never);
    }

    private VkPostDto SetUpPost(int postId)
    {
        var post = new VkPostDto { Id = postId, Text = "Music" };
        _vkClient.Setup(x => x.GetPostByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VkResponse<GetPostByIdResponse>(new GetPostByIdResponse(post)));
        return post;
    }
}