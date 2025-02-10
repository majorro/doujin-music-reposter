using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.AudioTags;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Telegram.Utils;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Setup.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding;

// TODO: refactor
public partial class TgPostBuildingService(
    ILogger<TgPostBuildingService> logger,
    IOptions<TgConfig> appConfig,
    IOptions<VkConfig> vkConfig,
    HttpClient httpClient,
    EncodingRepairingService encodingRepairer,
    AudioTaggingService audioTagger) // what to post
{
    private const int MAX_ATTACHMENT_SIZE = int.MaxValue - 100000000; // idk
    private const int POSSIBLY_BROKEN_ARCHIVE_SIZE_THRESHOLD = 1 * 1024 * 1024; // 1mb
    private static readonly HashSet<string> AudioExtensions =
    [
        ".mp3", ".wav", ".wma", ".flac", ".aac", ".alac", ".m4a", ".ape", ".wv", ".ogg", ".opus",
    ];

    private readonly string _localFilesDir = appConfig.Value.LocalFilesDir;
    private readonly string _botApiServerFilesDir = appConfig.Value.BotApiServerFilesDir;
    private readonly int _vkGroupId = vkConfig.Value.GroupId;
    // TODO: try https://github.com/itn3000/PooledStream
    private readonly RecyclableMemoryStreamManager _memoryStreamPool = new(new RecyclableMemoryStreamManager.Options
    {
        BlockSize = 1024 * 512, // 512kb
        LargeBufferMultiple = 1024 * 1024, // 1mb
        MaximumBufferSize = 32 * 1024 * 1024, // 32mb
        MaximumSmallPoolFreeBytes = 32 * 1024 * 1024, // 32mb
        MaximumLargePoolFreeBytes = 32 * 1024 * 1024, // 32mb
        UseExponentialLargeBuffer = true,
        ThrowExceptionOnToArray = true,
    });

    [GeneratedRegex(@"Disc\s\d|CD\d", RegexOptions.IgnoreCase)]
    private static partial Regex CdFileNameRegex();

    public async Task<TgPost> BuildAsync(Post post)
    {
        logger.LogInformation("Building PostId={PostId}", post.Id);
        var result = new TgPost()
        {
            TextParts = TextHelper.GetPreparedText(post, _vkGroupId), // TODO: try to replace vk links with tg links
            Photo = post.Photo
        };

        var timer = Stopwatch.StartNew();
        var audioArchiveFilesTasks = post.AudioArchives
            .OrderBy(x => x.SizeBytes)
            .Select(SaveAudioArchiveAsync)
            .ToArray();
        await Task.WhenAll(audioArchiveFilesTasks);
        timer.Stop();
        logger.LogInformation("Saved audio archives for PostId={PostId} in {Elapsed}", post.Id, timer.Elapsed);

        timer.Restart();
        var audioArchiveFiles = audioArchiveFilesTasks
            .Where(x => x.Result.Length != 0)
            .Select(x => x.Result);
        foreach (var archiveFiles in audioArchiveFiles)
        {
            if (result.AudioFiles.Count == 0 || CdFileNameRegex().IsMatch(archiveFiles[0].FileName))
            {
                var audioFiles = await SaveAudioFilesAsync(archiveFiles);
                if (audioFiles.Count == 0)
                {
                    foreach (var audioArchive in archiveFiles)
                        audioArchive.Dispose();
                    continue;
                }

                result.AudioFiles.AddRange(audioFiles);
            }

            // TODO: check archive for validity
            result.AudioArchives.AddRange(archiveFiles);
        }
        if (result.AudioFiles.All(x => char.IsDigit(x.FileName[0]) && (x.FileName[1] == '_' || x.FileName[2] == '_')))
            result.AudioFiles = result.AudioFiles.OrderBy(x => GetOrderFromFilename(x.FileName)).ToList();

        if (result.AudioArchives.Count > 2)
            logger.LogWarning("Too many audio archives for PostId={PostId}", post.Id);

        if (result.AudioFiles.Count == 0 && post.Audios.Count != 0)
        {
            var audioFileTasks = post.Audios.Select(SaveAudioAsync).ToArray();
            await Task.WhenAll(audioFileTasks);
            result.AudioFiles = audioFileTasks.SelectMany(x => x.Result).ToList();
        }
        timer.Stop();
        logger.LogInformation("Saved audios for PostId={PostId} in {Elapsed}", post.Id, timer.Elapsed);
        logger.LogInformation("Built PostId={PostId}", post.Id);

        return result;
    }

    private static short GetOrderFromFilename(string fileName) =>
        short.TryParse(fileName[..2], out var order) ? order :
        short.TryParse(fileName[..1], out order) ? order : (short)0;

    private async Task<List<AudioFile>> SaveAudioFilesAsync(AudioArchiveFile[] archiveFiles)
    {
        await using var stream = await OpenAudioArchiveFilesAsync(archiveFiles);

        return await SaveAudioFilesFromStreamAsync(stream, archiveFiles);
    }

    private async Task<List<AudioFile>> SaveAudioFilesFromStreamAsync(Stream stream, AudioArchiveFile[] archiveFiles)
    {
        var result = new List<AudioFile>();
        var trackNumber = 0;
        var archive = archiveFiles[0].AudioArchive;

        var readerOptions = new ReaderOptions { LeaveStreamOpen = true };
        try
        {
            using var reader = ReaderFactory.Open(stream, readerOptions);
            while (reader.MoveToNextEntry()) // TODO: fix tags in audio and archive
            {
                var entry = reader.Entry;
                if (entry.IsDirectory || !IsAudioFile(entry))
                    continue;

                await using var entryStream = _memoryStreamPool.GetStream("archiveEntry", entry.Size);
                await using var es = reader.OpenEntryStream();
                await es.CopyToAsync(entryStream);

                var entryFileName = encodingRepairer.TryFix(Path.GetFileName(entry.Key!))!;
                var (title, artist, durationSeconds) = audioTagger.ReadAndFix(entryStream, entryFileName);
                var audioFileParts = await SaveAudioFromStreamAsync(
                    entryStream,
                    title,
                    artist,
                    durationSeconds,
                    trackNumber,
                    entry.Size,
                    entryFileName
                );

                result.AddRange(audioFileParts);
                ++trackNumber;
            }
        }
        catch (InvalidOperationException e) // broken stream
        {
            logger.LogWarning(e, "Failed to open archive: {FileName} ({Link})", archive.FileName, archive.Link);
            return [];
        }
        catch (Exception e) when (e is
                                      InvalidFormatException or // broken archive/bug https://github.com/adamhathcock/sharpcompress/issues/890
                                      EndOfStreamException) // like this https://vk.com/wall-60027733_20309
        {
            logger.LogWarning(e, "Failed to read archive: {FileName} ({Link})", archive.FileName, archive.Link);
            return [];
        }

        return result;
    }

    private static bool IsAudioFile(IEntry entry)
    {
        var extension = Path.GetExtension(entry.Key)?.ToLower();
        return extension != null && AudioExtensions.Contains(extension);
    }

    private async Task<AudioArchiveFile[]> SaveAudioArchiveAsync(AudioArchive archive)
    {
        await using var stream = await AsDownloadableStreamAsync(archive.Link);
        if (stream == null)
            return [];

        var archiveFile = await SaveFromStreamAsync<AudioArchiveFile>(stream, archive.FileName, archive.SizeBytes);
        foreach (var audioArchiveFile in archiveFile)
            audioArchiveFile.AudioArchive = archive;

        return archiveFile;
    }

    private async Task<FileStream> OpenAudioArchiveFilesAsync(AudioArchiveFile[] archiveFiles)
    {
        if (archiveFiles.Length == 1)
            return File.OpenRead(archiveFiles[0].LocalFullName);

        var extension = Path.GetExtension(Path.GetFileNameWithoutExtension(archiveFiles[0].LocalFullName)); // strip .001
        var combinedArchivePath = Path.Combine(_localFilesDir, Path.ChangeExtension(Path.GetRandomFileName(), extension));
        var combinedFs = new FileStream(combinedArchivePath, new FileStreamOptions()
        {
            Mode = FileMode.Create,
            Access = FileAccess.ReadWrite,
            Options = FileOptions.DeleteOnClose | FileOptions.SequentialScan | FileOptions.Asynchronous
        });

        foreach (var archiveFile in archiveFiles)
        {
            await using var fs = File.OpenRead(archiveFile.LocalFullName);
            await fs.CopyToAsync(combinedFs);
        }
        combinedFs.Position = 0;

        return combinedFs;
    }

    private async Task<AudioFile[]> SaveAudioAsync(Audio audio, int trackNumber)
    {
        await using var stream = await AsDownloadableStreamAsync(audio.Link);
        if (stream == null)
            return [];

        await using var memoryStream = _memoryStreamPool.GetStream();
        await stream.CopyToAsync(memoryStream);

        audioTagger.WriteAndFix(memoryStream, new AudioInfo(audio.Title, audio.Artist, audio.DurationSeconds), $"{audio.Title}.mp3");

        return await SaveAudioFromStreamAsync(
            memoryStream,
            encodingRepairer.TryFix(audio.Title),
            encodingRepairer.TryFix(audio.Artist),
            audio.DurationSeconds,
            trackNumber,
            0, // let's hope there won't be any 2gb mp3s xd
            ""
        );
    }

    private async Task<AudioFile[]> SaveAudioFromStreamAsync(
        Stream stream,
        string? title,
        string? artist,
        int durationSeconds,
        int trackNumber,
        long sizeBytes,
        string fileName)
    {
        // TODO: convert with NAudio in case of any issues
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            fileName = $"{trackNumber + 1:D2}. {artist} - {title}.mp3";
        else if (!string.IsNullOrWhiteSpace(title))
            fileName = $"{trackNumber + 1:D2}. {title}.mp3";
        else
            fileName = Path.ChangeExtension(fileName, ".mp3");
        var audioFileParts = await SaveFromStreamAsync<AudioFile>(stream, fileName, sizeBytes);
        Array.ForEach(audioFileParts, x =>
        {
            x.Title = title;
            x.Artist = string.IsNullOrWhiteSpace(title) ? null : artist; // TODO: better fix to "artist - Unknown track" issue
            x.DurationSeconds = durationSeconds;
        });

        return audioFileParts;
    }

    private async Task<Stream?> AsDownloadableStreamAsync(Uri uri) // TODO: move to resilient stream method?
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); // TODO: ignore errors?
        if (response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.GatewayTimeout ||
            response.Content.Headers.ContentLength < POSSIBLY_BROKEN_ARCHIVE_SIZE_THRESHOLD)
            return null;
        response.EnsureSuccessStatusCode();
        return new ResilientStream(await response.Content.ReadAsStreamAsync(), logger, () => AsDownloadableStreamAsync(uri));
    }

    private async Task<T[]> SaveFromStreamAsync<T>(Stream stream, string fileName, long sizeBytes)
        where T : UploadableFile, new()
    {
        fileName = TextHelper.EnsureFilenameValidity(fileName);

        if (sizeBytes <= MAX_ATTACHMENT_SIZE)
        {
            await using var fileStream = UploadableFile.CreateAndOpen<T>(fileName, _localFilesDir, _botApiServerFilesDir, out var file);
            await stream.CopyToAsync(fileStream);
            return [file];
        }

        var result = new List<T>();
        var partNumber = 1;
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024 * 1024); // 16mb
        for (long offset = 0; offset < sizeBytes; offset += MAX_ATTACHMENT_SIZE)
        {
            var partFileName = $"{fileName}.{partNumber++:D3}";
            await using var partFileStream = UploadableFile.CreateAndOpen<T>(partFileName, _localFilesDir, _botApiServerFilesDir, out var file);
            for (long partOffset = 0; partOffset < MAX_ATTACHMENT_SIZE; partOffset += buffer.Length)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                    break;
                await partFileStream.WriteAsync(buffer.AsMemory(0, read));
            }

            result.Add(file);
        }

        return result.ToArray();
    }
}