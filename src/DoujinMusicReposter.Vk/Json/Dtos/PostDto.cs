namespace DoujinMusicReposter.Vk.Json.Dtos;

public record PostDto
{
    public Uri? Photo { get; internal set; }
    public List<AudioArchiveDto> AudioArchives { get; internal set; } = [];
    public int Id { get; internal set; }
    public string Text { get; internal set; } = null!;
}