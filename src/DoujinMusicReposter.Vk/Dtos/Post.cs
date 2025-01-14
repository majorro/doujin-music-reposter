namespace DoujinMusicReposter.Vk.Dtos;

public record Post
{
    public int Id { get; internal set; }
    public string Text { get; internal set; } = null!;
    public Uri? Photo { get; internal set; }
    public List<AudioArchive> AudioArchives { get; internal set; } = [];
    public List<Audio> Audios { get; internal set; } = [];
}