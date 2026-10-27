using DoujinMusicReposter.Telegram.Services.AdminBot;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Telegram.Services.Models;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Tests.DoujinMusicReposter.Telegram;

public class AdminCommandHandlingServiceTests
{
    private readonly Mock<IRepostingService> _repostingService = new(MockBehavior.Strict);
    private readonly AdminCommandHandlingService _service;

    public AdminCommandHandlingServiceTests()
    {
        var config = Options.Create(new TgConfig
        {
            AdminBotWhitelist = [101, 202],
            ChatAdminId = "303",
            ChatId = "-100404",
            LocalFilesDir = null!,
            BotApiServerFilesDir = null!,
            BotConfig = null!
        });
        _service = new AdminCommandHandlingService(config, _repostingService.Object);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(202)]
    public async Task HandleAsync_ShouldAllowEachConfiguredAdmin(long userId)
    {
        _repostingService.Setup(x => x.RepostAsync(
                It.Is<int[]>(ids => ids.SequenceEqual(new[] { 123 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepostResult[] { new(123, "Queued") });
        var message = new Message
        {
            From = new User { Id = userId },
            Chat = new Chat { Id = userId, Type = ChatType.Private },
            Text = "/repost 123"
        };

        var result = await _service.HandleAsync(message, "admin_bot");

        result.Should().Be("123: Queued");
        _repostingService.Verify(x => x.RepostAsync(
            It.IsAny<int[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(303, ChatType.Private)]
    [InlineData(101, ChatType.Supergroup)]
    public async Task HandleAsync_ShouldIgnoreUnauthorizedOrNonPrivateCommands(long userId, ChatType chatType)
    {
        var message = new Message
        {
            From = new User { Id = userId },
            Chat = new Chat { Id = userId, Type = chatType },
            Text = "/repost 123"
        };

        var result = await _service.HandleAsync(message, "admin_bot");

        result.Should().BeNull();
        _repostingService.VerifyNoOtherCalls();
    }
}