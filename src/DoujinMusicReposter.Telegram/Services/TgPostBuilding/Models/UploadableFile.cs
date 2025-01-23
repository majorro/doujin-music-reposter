namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;

public abstract record UploadableFile : IDisposable
{
    private bool _disposed;

    public string ServerFullName { get; private init; } = null!;
    public string LocalFullName { get; private init; } = null!;
    public string LocalDirectory => Path.GetDirectoryName(LocalFullName)!;
    public string FileName => Path.GetFileName(LocalFullName);
    public string Extension => Path.GetExtension(LocalFullName);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Directory.Delete(LocalDirectory, true);
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static FileStream CreateAndOpen<T>(string fileName, string localDirectory, string serverDirectory, out T file)
        where T: UploadableFile, new()
    {
        var dir = Path.GetRandomFileName();
        file = new T
        {
            ServerFullName = Path.Combine(serverDirectory, dir, fileName),
            LocalFullName = Path.Combine(localDirectory, dir, fileName),
        };
        Directory.CreateDirectory(file.LocalDirectory);
        return File.Create(file.LocalFullName);
    }
}