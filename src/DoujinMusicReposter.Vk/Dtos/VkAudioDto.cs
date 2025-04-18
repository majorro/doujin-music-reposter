﻿namespace DoujinMusicReposter.Vk.Dtos;

public record VkAudioDto
{
    public Uri Link { get; internal set; } = null!;
    public string Title { get; internal set; } = null!;
    public string Artist { get; internal set; } = null!;
    public int DurationSeconds { get; internal set; }
}