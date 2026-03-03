using System.Text.Json;
using System.Threading.Channels;
using DoujinMusicReposter.App.Services;
using DoujinMusicReposter.App.Services.Interfaces;
using DoujinMusicReposter.App.Workers;
using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.App.Setup;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(x =>
        {
            x.IncludeScopes = true;
            if (!builder.Environment.IsProduction())
                x.JsonWriterOptions = new JsonWriterOptions() { Indented = true };
        });

        return builder;
    }

    public static IHostApplicationBuilder AddShared(this IHostApplicationBuilder builder) // ?
    {
        var postBuildingQueue = Channel.CreateUnbounded<VkPostDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        builder.Services.AddSingleton(postBuildingQueue.Writer);
        builder.Services.AddSingleton(postBuildingQueue.Reader);

        var postBuildingSemaphore = new SemaphoreSlim(1, 1);
        builder.Services.AddSingleton(postBuildingSemaphore);

        return builder;
    }

    public static IHostApplicationBuilder AddApp(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IFeedIntegrityVerifyingService, FeedIntegrityVerifyingService>();

        builder.Services.AddHostedService<PostProcessingWorker>();
        builder.Services.AddHostedService<FeedIntegrityVerifyingWorker>();

        return builder;
    }
}
