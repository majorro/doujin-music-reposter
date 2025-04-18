﻿using DoujinMusicReposter.Persistence.Repositories.Implementations;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Persistence.Setup.Configuration;
using Majorro.Common.Setup.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DoujinMusicReposter.Persistence.Setup;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Configure<RocksDbConfig>();
        builder.Services.AddSingleton<IPostsRepository, PostsRepository>();
        return builder;
    }
}
