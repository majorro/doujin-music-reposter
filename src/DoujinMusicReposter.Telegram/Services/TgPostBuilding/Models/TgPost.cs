namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public record TgPost : IDisposable
{
    private bool _disposed;

    public string[] TextParts { get; internal set; } = null!;
    public Uri? Photo { get; internal set; }
    public List<AudioFile> AudioFiles { get; internal set; } = [];
    public List<AudioArchiveFile> AudioArchives { get; internal set; } = [];

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            foreach (var audioFile in AudioFiles)
                audioFile.Dispose();
            foreach (var audioArchive in AudioArchives)
                audioArchive.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
};