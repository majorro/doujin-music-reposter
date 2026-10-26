using DoujinMusicReposter.Telegram.Utils;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace DoujinMusicReposter.Telegram.Services.AdminBot;

internal class AdminUpdateHandler(
    ILogger<AdminUpdateHandler> logger,
    AdminCommandHandlingService commandHandler) : IUpdateHandler
{
    private string? _botUsername;

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();
        if (update.Message is not { } message)
            return;

        _botUsername ??= (await botClient.GetMe(ctk)).Username!;
        var reply = await commandHandler.HandleAsync(message, _botUsername, ctk);
        if (reply is null)
            return;

        foreach (var part in TextHelper.GetTgTextParts(reply, false))
            await botClient.SendMessage(message.Chat.Id, part, cancellationToken: ctk);
    }

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken ctk)
    {
        logger.LogWarning(exception, "Admin bot error from {Source}", source);

        if (exception is ApiRequestException { Parameters.RetryAfter: > 0 } apiException)
            await Task.Delay(TimeSpan.FromSeconds(apiException.Parameters.RetryAfter.Value), ctk);
        else if (source == HandleErrorSource.PollingError)
            await Task.Delay(TimeSpan.FromSeconds(5), ctk);
    }
}