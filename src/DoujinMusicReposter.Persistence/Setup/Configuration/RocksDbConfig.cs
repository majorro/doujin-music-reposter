using Majorro.Common.Setup.Configuration;

namespace DoujinMusicReposter.Persistence.Setup.Configuration;

public record RocksDbConfig : IConfig
{
    public static string SectionName => nameof(RocksDbConfig);

    public required string DirectoryPath { get; init; }
}