using System.Net.Http.Headers;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Json;
using DoujinMusicReposter.Vk.Setup.Configuration;
using Majorro.Common.Setup.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace DoujinMusicReposter.Vk.Setup;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddVk(this IHostApplicationBuilder builder)
    {
        builder.Configure<VkConfig>();

        builder.Services.AddHttpClient<IVkApiClient, VkApiClient>((sp, client) =>
            {
                var vkConfig = sp.GetRequiredService<IOptions<VkConfig>>().Value;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vkConfig.AppTokens[0]);
            })
            .AddPolicyHandler(_ => HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, retryAttempt)))));
        builder.Services.AddSingleton<IJsonSerializingService, JsonSerializingService>();

        builder.Services.AddHostedService<UpdateTrackingWorker>();

        return builder;
    }
}

