using DoujinMusicReposter.Telegram.Services.Models;

namespace DoujinMusicReposter.Telegram.Services.Interfaces;

public interface IRepostingService
{
    Task<IReadOnlyList<RepostResult>> RepostAsync(int[] postIds, CancellationToken ctk = default);
}