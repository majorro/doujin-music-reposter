using System.Net;
using DoujinMusicReposter.Telegram.Services;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using Majorro.Common.Setup.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Telegram.Bot;

namespace DoujinMusicReposter.Telegram.Setup;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddTelegram(this IHostApplicationBuilder builder)
    {
        builder.Configure<TgConfig>();

        builder.AddBotClients();

        builder.Services.AddSingleton<EncodingRepairingService>();

        builder.Services
            .AddHttpClient<TgPostBuildingService>(x => x.Timeout = TimeSpan.FromMinutes(5))
            .AddPolicyHandler(_ => HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, retryAttempt)))));

        builder.Services.AddSingleton<PostsManagingService>();

        return builder;
    }

    private static IHostApplicationBuilder AddBotClients(this IHostApplicationBuilder builder)
    {
        var sp = builder.Services.BuildServiceProvider();
        var botConfig = sp.GetRequiredService<IOptions<TgConfig>>().Value.BotConfig;

        for (var _ = 0; _ < botConfig.Tokens.Length; ++_)
        {
            var i = _;
            builder.Services
                .AddHttpClient($"TelegramBotClient{i}", x => x.Timeout = TimeSpan.FromDays(24))
                .AddTypedClient<ITelegramBotClient>(httpClient =>
                {
                    var token = botConfig.Tokens[i];
                    var apiServerUri = botConfig.ApiServerUris[i % botConfig.ApiServerUris.Length];
                    TelegramBotClientOptions options = new(token, apiServerUri);
                    return new TelegramBotClient(options, httpClient);
                })
                .AddPolicyHandler(_ => HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(resp =>
                        resp.StatusCode == HttpStatusCode.BadRequest &&
                        resp.Content.ReadAsStringAsync().Result.Contains("Too many requests"))
                    .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(8 * retryAttempt))); // forever?
        }

        builder.Services.AddSingleton<TelegramBotClientPoolService>();
        return builder;
    }
}
