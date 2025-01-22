using Majorro.Common.Setup.Configuration;

namespace DoujinMusicReposter.App.Setup.Configuration;

public record SerilogConfig : IConfig
{
    public static string SectionName => nameof(SerilogConfig);

    public required string TelegramBotToken { get; init; }
    public required string TelegramChatId { get; init; }
};