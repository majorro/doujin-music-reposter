using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetLongPollServerResponse(LongPollingServerConfig Config) : IResponseDto;