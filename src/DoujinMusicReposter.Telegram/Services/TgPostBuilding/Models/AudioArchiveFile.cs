using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public record AudioArchiveFile : UploadableFile
{
    public AudioArchive AudioArchive { get; internal set; } = null!;
}