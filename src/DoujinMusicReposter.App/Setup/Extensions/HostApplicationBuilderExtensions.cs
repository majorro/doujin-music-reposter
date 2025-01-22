using System.Threading.Channels;
using DoujinMusicReposter.App.Setup.Configuration;
using DoujinMusicReposter.App.Workers;
using DoujinMusicReposter.Vk.Dtos;
using Majorro.Common.Setup.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace DoujinMusicReposter.App.Setup.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddLogging(this IHostApplicationBuilder builder)
    {
        builder.Configure<SerilogConfig>();

        const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
        builder.Services.AddSerilog((sp, config) =>
        {
            var serilogConfig = sp.GetRequiredService<IOptions<SerilogConfig>>().Value;

            config
                .MinimumLevel.Information()
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.Telegram(
                    serilogConfig.TelegramBotToken,
                    serilogConfig.TelegramChatId,
                    restrictedToMinimumLevel: LogEventLevel.Error)
                .WriteTo.File(
                    "logs/log.txt",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: outputTemplate);
        });

        return builder;
    }

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
