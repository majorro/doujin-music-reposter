using DoujinMusicReposter.Vk.Http.Dtos;

namespace DoujinMusicReposter.Vk.Json;

public interface IJsonSerializingService
{
    VkResponse<GetPostsResponse> ParseGetPostsResponse(Stream stream);
    VkResponse<GetCommentsResponse> ParseGetCommentsResponse(Stream stream);
    VkResponse<GetLongPollServerResponse> ParseGetLongPollServerResponse(Stream stream);
    VkResponse<GetNewEventsResponse> ParseGetNewEventsResponse(Stream stream);
}