using DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;
using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.AudioTags;

public class AudioTaggingService(
    ILogger<AudioTaggingService> logger,
    IEncodingRepairingService encodingRepairer)
{
    public AudioInfo ReadAndFix(Stream stream, string? fileName = null)
    {
        try
        {
            using var tagFile = TagLib.File.Create(new AudioFileAbstraction(stream, fileName));
            TryFixTags(tagFile);
            tagFile.Save();

            return GetInfo(tagFile);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unable to read tags from {FileName}", fileName);
            return new AudioInfo(null, null, 0);
        }
    }

    public void WriteAndFix(Stream stream, AudioInfo info, string? fileName = null)
    {
        try
        {
            using var tagFile = TagLib.File.Create(new AudioFileAbstraction(stream, fileName));
            tagFile.Tag.Title = encodingRepairer.TryFix(info.Title);
            tagFile.Tag.Performers = [encodingRepairer.TryFix(info.Artist)];
            tagFile.Save();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unable to write tags to {FileName}", fileName);
        }
    }

    private void TryFixTags(TagLib.File tagFile) // TODO: remove redundant
    {
        tagFile.Tag.Title = encodingRepairer.TryFix(tagFile.Tag.Title);
        tagFile.Tag.Album = encodingRepairer.TryFix(tagFile.Tag.Album);
        tagFile.Tag.Performers = tagFile.Tag.Performers.Select(encodingRepairer.TryFix).ToArray();
        tagFile.Tag.AlbumArtists = tagFile.Tag.AlbumArtists.Select(encodingRepairer.TryFix).ToArray();
        tagFile.Tag.Composers = tagFile.Tag.Composers.Select(encodingRepairer.TryFix).ToArray();
        tagFile.Tag.Genres = tagFile.Tag.Genres.Select(encodingRepairer.TryFix).ToArray();
        tagFile.Tag.Comment = encodingRepairer.TryFix(tagFile.Tag.Comment);
        tagFile.Tag.Lyrics = encodingRepairer.TryFix(tagFile.Tag.Lyrics);
        // tagFile.Tag.Grouping = encodingRepairer.TryFix(tagFile.Tag.Grouping);
        // tagFile.Tag.Conductor = encodingRepairer.TryFix(tagFile.Tag.Conductor);
        // tagFile.Tag.Copyright = encodingRepairer.TryFix(tagFile.Tag.Copyright);
        // tagFile.Tag.RemixedBy = encodingRepairer.TryFix(tagFile.Tag.RemixedBy);
        // tagFile.Tag.Publisher = encodingRepairer.TryFix(tagFile.Tag.Publisher);
        // tagFile.Tag.ISRC = encodingRepairer.TryFix(tagFile.Tag.ISRC);
        // tagFile.Tag.InitialKey = encodingRepairer.TryFix(tagFile.Tag.InitialKey);
        // tagFile.Tag.Subtitle = encodingRepairer.TryFix(tagFile.Tag.Subtitle);
        // tagFile.Tag.MusicBrainzReleaseStatus = encodingRepairer.TryFix(tagFile.Tag.MusicBrainzReleaseStatus);
        // tagFile.Tag.MusicBrainzReleaseType = encodingRepairer.TryFix(tagFile.Tag.MusicBrainzReleaseType);
        // tagFile.Tag.MusicBrainzReleaseCountry = encodingRepairer.TryFix(tagFile.Tag.MusicBrainzReleaseCountry);
    }

    private static AudioInfo GetInfo(TagLib.File tagFile)
    {
        var title = tagFile.Tag.Title;
        var artist = string.IsNullOrWhiteSpace(tagFile.Tag.JoinedPerformers)
            ? tagFile.Tag.JoinedAlbumArtists
            : tagFile.Tag.JoinedPerformers;
        var durationSeconds = (int)tagFile.Properties.Duration.TotalSeconds;
        return new AudioInfo(title, artist, durationSeconds);
    }
}