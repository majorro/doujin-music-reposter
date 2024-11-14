namespace DoujinMusicReposter.Vk.Json.Dtos;

public record CommentDto
{
    public int Id { get; internal set; }
    public string? Text { get; internal set; }
    public List<AudioArchiveDto> AudioArchives { get; internal set; } = [];
    public bool IsFromAuthor { get; internal set; }
}