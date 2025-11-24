using System.Net;
using System.Reflection;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.AudioTags;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Setup.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Contrib.HttpClient;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace DoujinMusicReposter.Telegram.Tests.TgPostBuilding;

public class TgPostBuildingServiceTests : IDisposable
{
    private static readonly int LimitedMaxAttachmentSize = (int)(TestData.VkMp3Archive.SizeBytes - TestData.VkMp3Archive.SizeBytes / 4);

    private readonly string _tempPath;
    private readonly Mock<IOptions<TgConfig>> _appConfigMock;
    private readonly Mock<IOptions<VkConfig>> _vkConfigMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IEncodingRepairingService> _encodingRepairerMock;
    private readonly TgPostBuildingService _service;

    public TgPostBuildingServiceTests()
    {
        _tempPath = Path.Combine(TestData.DataPath, Path.GetRandomFileName());

        _appConfigMock = new Mock<IOptions<TgConfig>>();
        _appConfigMock.Setup(x => x.Value).Returns(new TgConfig
        {
            LocalFilesDir = _tempPath,
            BotApiServerFilesDir = _tempPath,
            ChatId = null!,
            ChatAdminId = null!,
            BotConfig = null!
        });

        _vkConfigMock = new Mock<IOptions<VkConfig>>();
        _vkConfigMock.Setup(x => x.Value).Returns(new VkConfig
        {
            GroupId = 0,
            ApiHost = null!,
            AppTokens = [],
            GroupToken = null!
        });

        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = _httpMessageHandlerMock.CreateClient();
        _httpMessageHandlerMock.SetupAnyRequest().ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
        {
            if (request.RequestUri!.Scheme != "file")
            {
                throw new NotImplementedException("Unexpected http request");
            }

            var uri = request.RequestUri!.OriginalString;
            var stream = new FileStream(uri, FileMode.Open, FileAccess.Read, FileShare.Read);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            return response;
        });

        _encodingRepairerMock = new Mock<IEncodingRepairingService>();
        _encodingRepairerMock.Setup(x => x.TryFix(It.IsAny<string>())).Returns<string>(x => x);

        _service = new TgPostBuildingService(
            Mock.Of<ILogger<TgPostBuildingService>>(),
            _appConfigMock.Object,
            _vkConfigMock.Object,
            httpClient,
            _encodingRepairerMock.Object,
            new AudioTaggingService(Mock.Of<ILogger<AudioTaggingService>>(), _encodingRepairerMock.Object)
        );
    }

    public void Dispose()
    {
        Directory.Delete(_tempPath, true);
    }

    [Fact]
    public async Task BuildAsync_GivenPostWithMp3Archive_Check()
    {
        var post = new VkPostDto
        {
            Id = 228,
            Text = "Text",
            VkAudioArchives = [TestData.VkMp3Archive],
        };

        using var result = await _service.BuildAsync(post);

        result.TextParts[0].Should().StartWith("Text");
        result.Photo.Should().BeNull();
        result.AudioArchives.Should().HaveCount(1);
        result.AudioFiles.Should().HaveCount(TestData.AudioInArchiveCount);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].Title.Should().Be(TestData.Mp3Audio.Title);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].Artist.Should().Be(TestData.Mp3Audio.Artist);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].DurationSeconds.Should().Be(TestData.Mp3Audio.DurationSeconds);

        var extractedMp3ArchivePath = ExtractArchive(result.AudioArchives[0].LocalFullName);
        var expectedExtractedPath = Path.Combine(TestData.DataPath, TestData.VkMp3Archive.FileName.Replace(".zip", "Extracted"));
        AssertDirectoriesEquivalence(expectedExtractedPath, extractedMp3ArchivePath);
    }

    [Fact]
    public async Task BuildAsync_GivenPostWithMp3ArchiveExceedingMaxAttachmentSize_Check()
    {
        var fieldInfo = typeof(TgPostBuildingService).GetField("MaxAttachmentSize", BindingFlags.NonPublic | BindingFlags.Static);
        fieldInfo!.SetValue(_service, LimitedMaxAttachmentSize);

        var post = new VkPostDto
        {
            Id = 228,
            Text = "Text",
            VkAudioArchives = [TestData.VkMp3Archive],
        };

        using var result = await _service.BuildAsync(post);

        result.TextParts[0].Should().StartWith("Text");
        result.Photo.Should().BeNull();
        result.AudioArchives.Should().HaveCount(2);
        result.AudioFiles.Should().HaveCount(TestData.AudioInArchiveCount);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].Title.Should().Be(TestData.Mp3Audio.Title);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].Artist.Should().Be(TestData.Mp3Audio.Artist);
        result.AudioFiles[TestData.Mp3AudioIndexInArchive].DurationSeconds.Should().Be(TestData.Mp3Audio.DurationSeconds);

        var mergedMp3ArchivePath = MergeAndSaveArchive(result.AudioArchives);
        var extractedMp3ArchivePath = ExtractArchive(mergedMp3ArchivePath);
        var expectedExtractedPath = Path.Combine(TestData.DataPath, TestData.VkMp3Archive.FileName.Replace(".zip", "Extracted"));
        AssertDirectoriesEquivalence(expectedExtractedPath, extractedMp3ArchivePath);
    }

    private string MergeAndSaveArchive(params IEnumerable<AudioArchiveFile> archiveFileParts)
    {
        var mergedLargeArchiveName = Path.GetRandomFileName() + ".zip";
        var mergedArchivePath = Path.Combine(_tempPath, mergedLargeArchiveName);
        using var mergedStream = new FileStream(mergedArchivePath, FileMode.Create);
        foreach (var archive in archiveFileParts)
        {
            var partPath = archive.LocalFullName;
            using var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read);
            partStream.CopyTo(mergedStream);
        }

        return mergedArchivePath;
    }

    private string ExtractArchive(string archivePath)
    {
        var extractedPath = Path.Combine(_tempPath, Path.GetFileNameWithoutExtension(archivePath) + "Extracted");
        Directory.CreateDirectory(extractedPath);
        using var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
        using var reader = ReaderFactory.Open(archiveStream);
        reader.WriteAllToDirectory(extractedPath, new ExtractionOptions { ExtractFullPath = true });
        return extractedPath;
    }

    private static void AssertDirectoriesEquivalence(string expectedPath, string actualPath)
    {
        var expectedFiles = Directory.GetFiles(expectedPath, "*", SearchOption.AllDirectories)
            .Select(f => f[(expectedPath.Length + 1)..]).OrderBy(f => f).ToList();
        var actualFiles = Directory.GetFiles(actualPath, "*", SearchOption.AllDirectories)
            .Select(f => f[(actualPath.Length + 1)..]).OrderBy(f => f).ToList();

        expectedFiles.Should().BeEquivalentTo(actualFiles);

        foreach (var file in expectedFiles)
        {
            var expectedFilePath = Path.Combine(expectedPath, file);
            var actualFilePath = Path.Combine(actualPath, file);
            File.ReadAllBytes(expectedFilePath).Should().Equal(File.ReadAllBytes(actualFilePath));
        }
    }
}