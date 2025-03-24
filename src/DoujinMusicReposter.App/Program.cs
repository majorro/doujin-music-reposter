using DoujinMusicReposter.App.Setup.Extensions;
using DoujinMusicReposter.Persistence.Setup;
using DoujinMusicReposter.Telegram.Setup;
using DoujinMusicReposter.Vk.Setup;
using Majorro.Common.Setup;

DotEnv.TryLoad();

var builder = Host.CreateApplicationBuilder(args);

builder.AddLogging();

builder
    .AddShared()
    .AddPersistence();

builder
    .AddVk()
    .AddTelegram()
    .AddApp();

// TODO: maybe add some post trace
// TODO: cleanup downloads directory in case of exception
var host = builder.Build();
host.Run();