namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.AudioTags;

internal class AudioFileAbstraction(Stream stream, string? fileName = null) : TagLib.File.IFileAbstraction
{
    public Stream ReadStream { get; } = stream;
    public Stream WriteStream { get; } = stream;
    public string Name { get; } = fileName ?? "audio.mp3";

    public void CloseStream(Stream stream)
    {
        ReadStream.Position = 0;
    }
}