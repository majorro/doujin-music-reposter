namespace DoujinMusicReposter.Vk.Dtos;

public record VkPostDto
{
    public int Id { get; internal set; }
    public bool IsDonut { get; internal set; }
    public string Text { get; internal set; } = null!;
    public Uri? Photo { get; internal set; }
    public List<VkAudioArchiveDto> AudioArchives { get; internal set; } = [];
    public List<VkAudioDto> Audios { get; internal set; } = [];
}