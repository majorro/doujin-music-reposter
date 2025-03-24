using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public record AudioArchiveFile : UploadableFile
{
    public VkAudioArchiveDto AudioArchive { get; internal set; } = null!;
}