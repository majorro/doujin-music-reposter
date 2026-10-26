using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetPostByIdResponse(VkPostDto? Post) : IResponseDto;
