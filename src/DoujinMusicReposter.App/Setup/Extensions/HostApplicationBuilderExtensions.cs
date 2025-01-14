using System.Threading.Channels;
using DoujinMusicReposter.App.Workers;
using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.App.Setup.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddShared(this IHostApplicationBuilder builder) // ?
    {
        var postBuildingQueue = Channel.CreateUnbounded<Post>(new UnboundedChannelOptions
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

    public static IHostApplicationBuilder AddAppWorkers(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHostedService<PostProcessingWorker>();
        builder.Services.AddHostedService<FeedIntegrityVerifyingWorker>();

        return builder;
    }
}
