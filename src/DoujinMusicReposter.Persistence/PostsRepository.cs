using DoujinMusicReposter.Persistence.Setup.Configuration;
using Microsoft.Extensions.Options;
using RocksDbSharp;

namespace DoujinMusicReposter.Persistence;

// TODO: serialize to bytes
public class PostsRepository : IDisposable
{
    private readonly RocksDb _db;
    private readonly ColumnFamilyHandle _vkToTgCf;
    private readonly ColumnFamilyHandle _tgToVkCf;

    public PostsRepository(IOptions<RocksDbConfig> dbConfig)
    {
        var options = new DbOptions().SetCreateIfMissing().SetCreateMissingColumnFamilies();
        var cfOptions = new ColumnFamilyOptions();

        var columnFamilies = new ColumnFamilies
        {
            {"default", cfOptions},
            {"vk_to_tg", cfOptions},
            {"tg_to_vk", cfOptions}
        };

        _db = RocksDb.Open(options, dbConfig.Value.DirectoryPath, columnFamilies);
        _vkToTgCf = _db.GetColumnFamily("vk_to_tg");
        _tgToVkCf = _db.GetColumnFamily("tg_to_vk");
    }

    public List<int> GetAllVkIds()
    {
        var result = new List<int>();

        using var iterator = _db.NewIterator(_vkToTgCf);
        for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next())
            result.Add(int.Parse(iterator.StringKey()));

        return result;
    }

    public IEnumerable<int>? GetByVkId(int vkId) => ToIntEnumerable(_db.Get(vkId.ToString(), _vkToTgCf));

    public void Put(int vkId, IEnumerable<int> tgIds)
    {
        var vkIdKey = vkId.ToString();
        var tgIdsKey = FromArray(tgIds);

        _db.Put(vkIdKey, tgIdsKey, _vkToTgCf);
        _db.Put(tgIdsKey, vkIdKey, _tgToVkCf);
    }

    public void RemoveByVkId(int vkId)
    {
        var vkIdKey = vkId.ToString();
        var tgIdsKey = _db.Get(vkIdKey, _vkToTgCf);

        _db.Remove(vkIdKey, _vkToTgCf);
        _db.Remove(tgIdsKey, _tgToVkCf);
    }

    public void ForceSaveChanges() => _db.Flush(new FlushOptions());

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string FromArray<T>(IEnumerable<T> keys) => string.Join('-', keys);
    private static IEnumerable<int>? ToIntEnumerable(string? keys) => keys?.Split('-').Select(int.Parse);

}