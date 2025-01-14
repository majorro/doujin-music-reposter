using Majorro.Common.Setup.Configuration;

namespace DoujinMusicReposter.Telegram.Setup.Configuration;

public record TgConfig : IConfig
{
    public static string SectionName => nameof(TgConfig);

    public required string ChatId { get; init; }
    public required string ChatAdminId { get; init; }
    public required string LocalFilesDir { get; init; } // mounted to BotApiServerFilesDir in tg bot api server container
    public required string BotApiServerFilesDir { get; init; }
    public required BotConfig BotConfig { get; init; }
}