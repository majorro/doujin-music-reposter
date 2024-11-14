using System.Threading.Channels;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Json;
using DoujinMusicReposter.Vk.Json.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DoujinMusicReposter.Vk;

public static class ConfigurationExtensions
{
    public static IHostApplicationBuilder AddVkApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IVkApiClient, VkApiClient>();
        builder.Services.AddSingleton<IJsonSerializingService, JsonSerializingService>();

        if (builder.Services.Any(x => x.ServiceType == typeof(ChannelWriter<PostDto>))) // TODO: needed?
            throw new InvalidOperationException($"{nameof(ChannelWriter<PostDto>)} is not registered");
        builder.Services.AddHostedService<LongPollingService>();

        return builder;
    }
}

