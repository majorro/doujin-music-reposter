using DoujinMusicReposter.Api.Http;
using DoujinMusicReposter.Api.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DoujinMusicReposter.Api;

public static class ConfigurationExtensions
{
    public static IHostApplicationBuilder AddVkApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<VkApiClient>();
        builder.Services.AddSingleton<JsonParsingService>();
        builder.Services.AddSingleton<VkApiService>();

        return builder;
    }
}

