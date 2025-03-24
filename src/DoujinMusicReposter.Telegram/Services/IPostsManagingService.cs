using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

namespace DoujinMusicReposter.Telegram.Services;

public interface IPostsManagingService
{
    Task<List<int>> SendAsync(TgPost post);
    Task DeleteMessagesAsync(int[] messagesIds);
}