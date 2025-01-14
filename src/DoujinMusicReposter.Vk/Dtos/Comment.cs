namespace DoujinMusicReposter.Vk.Dtos;

public record Comment
{
    public int Id { get; internal set; }
    public string? Text { get; internal set; }
    public List<AudioArchive> AudioArchives { get; internal set; } = [];
    public bool IsFromAuthor { get; internal set; }
}