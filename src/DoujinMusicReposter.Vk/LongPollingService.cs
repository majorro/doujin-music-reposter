using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Json;
using DoujinMusicReposter.Vk.Json.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DoujinMusicReposter.Vk;

public class LongPollingService(
    ILogger<LongPollingService> logger,
    IVkApiClient apiClient,
    IJsonSerializingService serializingService,
    ChannelWriter<PostDto> channelWriter) : BackgroundService
{
    private LongPollingServerConfigDto _serverConfig = null!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateServerConfigAsync();
        while (!stoppingToken.IsCancellationRequested)
        {
            var posts = await GetUpdatesAsync();
            foreach (var post in posts)
                await channelWriter.WriteAsync(post, stoppingToken);
        }
    }

    private async Task UpdateServerConfigAsync()
    {
        await using var stream = await apiClient.GetLongPollServerAsync();
        _serverConfig = serializingService.ParseGetLongPollServerResponse(stream);
    }

    private async Task<List<PostDto>> GetUpdatesAsync()
    {
        await using var stream = await apiClient.GetNewEvents(_serverConfig);

        var result = serializingService.ParseGetNewEventsResponse(stream);
        if (result is null)
        {
            await UpdateServerConfigAsync();
            return [];
        }

        _serverConfig.LastEventNumber = result.Value.LastEventNumber;
        return result.Value.Posts;
    }
}