using DoujinMusicReposter.Telegram.Services.AdminBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace DoujinMusicReposter.Telegram.Workers;

internal class AdminBotWorker(
    [FromKeyedServices("AdminBot")] ITelegramBotClient bot,
    AdminUpdateHandler updateHandler) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ctk) =>
        bot.ReceiveAsync(updateHandler, new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            // Confirm each command on the next poll rather than after a larger batch.
            Limit = 1,
            DropPendingUpdates = false
        }, ctk);
}