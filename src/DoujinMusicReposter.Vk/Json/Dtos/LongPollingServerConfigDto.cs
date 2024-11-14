namespace DoujinMusicReposter.Vk.Json.Dtos;

public record LongPollingServerConfigDto
{
    public string Key { get; internal set; } = null!;
    public string Server { get; internal set; } = null!;
    public string LastEventNumber { get; internal set; } = null!;
}