using DoujinMusicReposter.Vk.Json.Dtos;

namespace DoujinMusicReposter.Vk.Http;

public interface IVkApiClient
{
    Task<Stream> GetPostsAsync(int offset = 0, int count = 100);
    Task<Stream> GetCommentsAsync(int postId, int offset = 0, int count = 100, int previewLength = 0);
    Task<Stream> GetLongPollServerAsync();
    Task<Stream> GetNewEvents(LongPollingServerConfigDto config);
}