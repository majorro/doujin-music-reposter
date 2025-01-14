using Majorro.Common.Setup.Configuration;

namespace DoujinMusicReposter.Vk.Setup.Configuration;

public record VkConfig : IConfig
{
    public static string SectionName => nameof(VkConfig);

    public required Uri ApiHost { get; init; }
    public required string[] AppTokens { get; init; }
    public required int GroupId { get; init; }
    public required string GroupToken { get; init; }
}