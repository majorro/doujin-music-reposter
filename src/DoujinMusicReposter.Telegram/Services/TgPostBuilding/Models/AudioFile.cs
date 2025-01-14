namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public record AudioFile : UploadableFile
{
    public string? Title { get; internal set; }
    public string? Artist { get; internal set; }
    public int DurationSeconds { get; internal set; }
}