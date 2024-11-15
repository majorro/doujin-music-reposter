﻿namespace DoujinMusicReposter.Vk.Json.Dtos;

public record AudioArchiveDto
{
    public Uri Link { get; internal set; } = null!;
    public long SizeBytes { get; internal set; }
}