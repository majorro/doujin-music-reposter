using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http.Dtos;

namespace DoujinMusicReposter.Vk.Http;

public interface IVkApiClient
{
    Task<VkResponse<GetPostsResponse>> GetPostsAsync(int offset = 0, int count = 100);
    Task<VkResponse<GetCommentsResponse>> GetCommentsAsync(int postId, int offset = 0, int count = 100, int previewLength = 0);
    Task<VkResponse<GetLongPollServerResponse>> GetLongPollServerAsync();
    Task<VkResponse<GetNewEventsResponse>> GetNewEvents(LongPollingServerConfigDto config, CancellationToken ctk);
}