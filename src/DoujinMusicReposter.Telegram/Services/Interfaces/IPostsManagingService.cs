using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

namespace DoujinMusicReposter.Telegram.Services.Interfaces;

public interface IPostsManagingService
{
    Task<List<int>> SendAsync(TgPost post);
    Task DeleteMessagesAsync(int[] messagesIds);
}