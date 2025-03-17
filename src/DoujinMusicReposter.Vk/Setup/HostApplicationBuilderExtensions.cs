using System.Net.Http.Headers;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Json;
using DoujinMusicReposter.Vk.Setup.Configuration;
using Majorro.Common.Setup.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace DoujinMusicReposter.Vk.Setup;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddVk(this IHostApplicationBuilder builder)
    {
        builder.Configure<VkConfig>();

        var sp = builder.Services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<VkApiClient>>();
        builder.Services.AddHttpClient<IVkApiClient, VkApiClient>((sp, client) =>
            {
                var vkConfig = sp.GetRequiredService<IOptions<VkConfig>>().Value;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vkConfig.AppTokens[0]);
                client.Timeout = TimeSpan.FromMinutes(5); // TODO: timeouts are not retried? check it!
            })
            .AddPolicyHandler(_ => HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryForeverAsync(
                    retryAttempt => TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, retryAttempt))),
                    (result, i, _) => logger.LogWarning("Request {Request} failed with response: {Code}: {Error}, retry #{I}", result.Result?.RequestMessage, (int?)result.Result?.StatusCode, result.Result?.ReasonPhrase, i)));
        builder.Services.AddSingleton<IJsonSerializingService, JsonSerializingService>();

        builder.Services.AddHostedService<UpdateTrackingWorker>();

        return builder;
    }
}

