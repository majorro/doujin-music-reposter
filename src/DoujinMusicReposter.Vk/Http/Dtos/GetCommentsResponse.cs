using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetCommentsResponse(List<Comment> Comments) : IResponseDto;