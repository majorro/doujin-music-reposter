using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Vk.Http.Dtos;

public record GetPostsResponse(int TotalCount, List<Post> Posts) : IResponseDto;