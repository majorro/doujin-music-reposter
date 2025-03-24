namespace DoujinMusicReposter.Vk.Dtos;

public record LongPollingServerConfigDto
{
    public string Key { get; internal set; } = null!;
    public string Server { get; internal set; } = null!;
    public string Timestamp { get; internal set; } = null!;
}