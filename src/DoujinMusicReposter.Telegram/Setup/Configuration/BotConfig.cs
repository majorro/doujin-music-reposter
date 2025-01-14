using Majorro.Common.Setup.Configuration;

namespace DoujinMusicReposter.Telegram.Setup.Configuration;

public record BotConfig : IConfig
{
    public static string SectionName => nameof(BotConfig);

    public required string[] Tokens { get; init; }
    public required string[] ApiServerUris { get; init; }
}