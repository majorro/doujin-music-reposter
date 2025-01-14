namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public record TgPost
{
    public string[] TextParts { get; internal set; } = null!;
    public Uri? Photo { get; internal set; }
    public List<AudioFile> AudioFiles { get; internal set; } = [];
    public List<AudioArchiveFile> AudioArchives { get; internal set; } = [];
};