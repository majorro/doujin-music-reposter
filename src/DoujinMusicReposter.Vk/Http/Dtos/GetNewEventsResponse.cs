﻿using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetNewEventsResponse(string Timestamp, List<VkPostDto> Posts) : IResponseDto;