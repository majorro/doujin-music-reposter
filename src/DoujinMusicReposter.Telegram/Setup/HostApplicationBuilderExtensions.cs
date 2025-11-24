using System.Net;
using DoujinMusicReposter.Telegram.Services;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.AudioTags;
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

        var sp = builder.Services.BuildServiceProvider();
        builder.AddBotClients(sp);

        builder.Services.AddSingleton<IEncodingRepairingService, EncodingRepairingService>();
        builder.Services.AddSingleton<AudioTaggingService>();

        // TODO: do not handle 504, implement proper error handling
        var logger = sp.GetRequiredService<ILogger<TgPostBuildingService>>();
        builder.Services
            .AddHttpClient<TgPostBuildingService>(x => x.Timeout = TimeSpan.FromMinutes(10));
            // .AddPolicyHandler(x => HttpPolicyExtensions
            //     .HandleTransientHttpError()
            //     .WaitAndRetryForeverAsync(
            //         retryAttempt => TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, retryAttempt))),
            //         (result, i, _) => logger.LogWarning("Request {Request} failed with response: {Code}: {Error}, retry #{I}", result.Result?.RequestMessage, (int?)result.Result?.StatusCode, result.Result?.ReasonPhrase, i)));

        builder.Services.AddSingleton<IPostsManagingService, PostsManagingService>();

        return builder;
    }

    private static IHostApplicationBuilder AddBotClients(this IHostApplicationBuilder builder, ServiceProvider sp)
    {
        var botConfig = sp.GetRequiredService<IOptions<TgConfig>>().Value.BotConfig;
        var logger = sp.GetRequiredService<ILogger<TelegramBotClient>>();

        for (var _ = 0; _ < botConfig.Tokens.Length; ++_)
        {
            var i = _;
            builder.Services
                .AddHttpClient($"TelegramBotClient{i}", x => x.Timeout = TimeSpan.FromDays(24))
                .AddTypedClient<ITelegramBotClient>(httpClient =>
                {
                    var token = botConfig.Tokens[i];
                    var apiServerUri = botConfig.ApiServerUris[i % botConfig.ApiServerUris.Length];
                    TelegramBotClientOptions options = new(token, apiServerUri)
                    {
                        RetryThreshold = int.MaxValue,
                        RetryCount = 10,
                    };
                    return new TelegramBotClient(options, httpClient);
                })
                .AddPolicyHandler(_ => HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(resp =>
                        resp.StatusCode == HttpStatusCode.BadRequest && // lmao
                        resp.Content.ReadAsStringAsync().Result.Contains("too many requests", StringComparison.CurrentCultureIgnoreCase))
                    .WaitAndRetryForeverAsync( // forever?
                        retryAttempt => TimeSpan.FromSeconds(8 * retryAttempt),
                        (result, i, _) => logger.LogWarning("Request {Request} failed with response: {Code}: {Error}, retry #{I}", result.Result?.RequestMessage, (int?)result.Result?.StatusCode, result.Result?.ReasonPhrase, i))); // TODO: rewrite
        }

        builder.Services.AddSingleton<TelegramBotClientPoolService>();
        return builder;
    }
}
