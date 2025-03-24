namespace DoujinMusicReposter.Vk.Dtos;

public record VkCommentDto
{
    public int Id { get; internal set; }
    public string? Text { get; internal set; }
    public List<VkAudioArchiveDto> AudioArchives { get; internal set; } = [];
    public bool IsFromAuthor { get; internal set; }
}