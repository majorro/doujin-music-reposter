using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetPostsResponse(int TotalCount, List<VkPostDto> Posts) : IResponseDto;