using System.Text.Json;
using DoujinMusicReposter.Persistence.Setup.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocksDbSharp;

namespace DoujinMusicReposter.Persistence;

public class PostsRepository : IDisposable
{
    private readonly ILogger<PostsRepository> _logger;
    private readonly RocksDb _db;
    private readonly ColumnFamilyHandle _vkToTgCf;
    private readonly ColumnFamilyHandle _tgToVkCf;

    public PostsRepository(IOptions<RocksDbConfig> dbConfig, ILogger<PostsRepository> logger)
    {
        _logger = logger;

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
            result.Add(Deserialize<int>(iterator.GetKeySpan()));

        return result;
    }

    public int[]? GetByVkId(int vkId)
    {
        var key = Serialize(vkId);

        var tgIds = _db.Get(key, _vkToTgCf);
        return tgIds is null ? null : Deserialize<int[]>(tgIds);
    }

    public void Put(int vkId, IEnumerable<int> tgIds)
    {
        var vkIdKey = Serialize(vkId);
        var tgIdsKey = Serialize(tgIds);

        _db.Put(vkIdKey, tgIdsKey, _vkToTgCf);
        _db.Put(tgIdsKey, vkIdKey, _tgToVkCf);
    }

    public void RemoveByVkId(int vkId)
    {
        var vkIdKey = Serialize(vkId);
        var tgIdsKey = _db.Get(vkIdKey, _vkToTgCf);

        _db.Remove(vkIdKey, _vkToTgCf);
        _db.Remove(tgIdsKey, _tgToVkCf);

        _logger.LogInformation("Removed vkId {VkId} with tgIds {TgIds}", vkId, string.Join("-", Deserialize<int[]>(tgIdsKey)));
    }

    public void ForceSaveChanges() => _db.Flush(new FlushOptions());

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ReadOnlySpan<byte> Serialize<T>(T obj) => JsonSerializer.SerializeToUtf8Bytes(obj);
    private static T Deserialize<T>(ReadOnlySpan<byte> bytes) => JsonSerializer.Deserialize<T>(bytes)!;

}