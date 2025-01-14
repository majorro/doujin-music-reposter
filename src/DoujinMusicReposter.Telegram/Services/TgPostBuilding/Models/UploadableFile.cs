namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public abstract record UploadableFile
{
    public string ServerFullName { get; init; } = null!;
    public string LocalFullName { get; init; } = null!;
    public string FileName => Path.GetFileName(LocalFullName);
    public string Extension => Path.GetExtension(LocalFullName);
}