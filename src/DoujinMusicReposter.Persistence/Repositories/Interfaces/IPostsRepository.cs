namespace DoujinMusicReposter.Persistence.Repositories.Interfaces;

public interface IPostsRepository : IDisposable
{
    List<int> GetAllVkIds();
    int[]? GetByVkId(int vkId);
    void Put(int vkId, IEnumerable<int> tgIds);
    void RemoveByVkId(int vkId);
    void ForceSaveChanges();
}