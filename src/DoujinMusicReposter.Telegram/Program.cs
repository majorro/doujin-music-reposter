using DoujinMusicReposter.Api;
using DoujinMusicReposter.Telegram;

var builder = Host.CreateApplicationBuilder(args);

builder.AddVkApi();
builder.Services.AddHostedService<PostingService>();

var host = builder.Build();
host.Run();