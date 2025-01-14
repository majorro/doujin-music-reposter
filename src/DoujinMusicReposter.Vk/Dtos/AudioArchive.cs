namespace DoujinMusicReposter.Vk.Dtos;

public record AudioArchive
{
    public Uri Link { get; internal set; } = null!;
    public string FileName { get; internal set; } = null!;
    public long SizeBytes { get; internal set; }
}