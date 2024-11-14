using DoujinMusicReposter.Vk.Json.Dtos;

namespace DoujinMusicReposter.Vk.Json;

public interface IJsonSerializingService
{
    (int TotalCount, List<PostDto> Posts) ParseGetPostsResponse(Stream stream);
    List<CommentDto> ParseGetCommentsResponse(Stream stream);
    LongPollingServerConfigDto ParseGetLongPollServerResponse(Stream stream);
    (string LastEventNumber, List<PostDto> Posts)? ParseGetNewEventsResponse(Stream stream);
}