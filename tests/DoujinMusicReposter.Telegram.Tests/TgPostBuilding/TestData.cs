using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Telegram.Tests.TgPostBuilding;

public static class TestData
{
    public static readonly string DataPath = Path.Combine(AppContext.BaseDirectory, "../../../TgPostBuilding", "TestData");
    public const int AudioInArchiveCount = 2;
    public const int Mp3AudioIndexInArchive = 1;

    public static VkAudioArchiveDto VkMp3Archive => new()
    {
        Link = new Uri(Path.Combine(DataPath, "Mp3AudioArchive.zip")),
        SizeBytes = 24_919_639,
        FileName = "Mp3AudioArchive.zip",
    };

    public static PixelDrainAudioArchiveDto PixelDrainMp3Archive =>
        new(new Uri(Path.Combine(DataPath, "Mp3AudioArchive.zip")));

    public static VkAudioDto Mp3Audio => new()
    {
        Link = new Uri(Path.Combine(DataPath, "Audio.mp3")),
        Artist = "Pa's Lam System",
        DurationSeconds = 306,
        Title = "Looooooop",
    };
}