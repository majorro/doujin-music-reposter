using System.Globalization;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DoujinMusicReposter.Telegram.Services.AdminBot;

internal class AdminCommandHandlingService(IOptions<TgConfig> tgConfig, IRepostingService repostingService)
{
    private const string Usage = "Usage: /repost <VK post ID> [more IDs separated by spaces]";
    private readonly long _adminId = long.Parse(tgConfig.Value.ChatAdminId, CultureInfo.InvariantCulture);

    public async Task<string?> HandleAsync(Message message, string botUsername, CancellationToken ctk = default)
    {
        if (message.Chat.Type != ChatType.Private || message.From?.Id != _adminId || message.Chat.Id != _adminId)
            return null;
        if (message.Text is null)
            return Usage;

        var parts = message.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Usage;

        var command = parts[0].Split('@', 2);
        if (command.Length == 2 && !command[1].Equals(botUsername, StringComparison.OrdinalIgnoreCase))
            return null;
        if (command[0] != "/repost" || parts.Length == 1)
            return Usage;

        var postIds = new List<int>();
        foreach (var part in parts.Skip(1))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var postId) || postId <= 0)
                return $"Invalid VK post ID: {part}\n{Usage}";
            postIds.Add(postId);
        }

        var results = await repostingService.RepostAsync(postIds.Distinct().ToArray(), ctk);
        return string.Join('\n', results.Select(x => $"{x.PostId}: {x.Status}"));
    }
}