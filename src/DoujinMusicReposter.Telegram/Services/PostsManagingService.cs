using System.Text;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Telegram.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace DoujinMusicReposter.Telegram.Services;

public class PostsManagingService(
    ILogger<PostsManagingService> logger,
    IOptions<TgConfig> tgConfig,
    TelegramBotClientPoolService botPool) // how to post
{
    private readonly string _chatId = tgConfig.Value.ChatId;
    private readonly string _chatAdminId = tgConfig.Value.ChatAdminId;

    public async Task<List<int>> SendAsync(TgPost post)
    {
        var result = new List<int>();

        try
        {
            var titleMessages = await SendTitleAsync(post);
            result.AddRange(titleMessages.Select(x => x.MessageId));
            var audioMessages = await SendAudioAsync(post);
            result.AddRange(audioMessages.Select(x => x.MessageId));
            var audioArchiveMessages = await SendAudioArchiveAsync(post);
            result.AddRange(audioArchiveMessages.Select(x => x.MessageId));
        }
        catch
        {
            if (result.Count != 0)
                await DeleteMessagesAsync(result.ToArray());
            throw;
        }

        return result;
    }

    // TODO: send photo with the last textpart
    private async Task<List<Message>> SendTitleAsync(TgPost post)
    {
        var botClient = botPool.GetClient();
        var result = new List<Message>();

        var linkPreviewOptions = new LinkPreviewOptions()
        {
            IsDisabled = true
        };

        if (post.Photo is not null)
            result.Add(await botClient.SendPhoto(
                chatId: _chatId,
                photo: new InputFileUrl(post.Photo),
                caption: post.TextParts[0],
                showCaptionAboveMedia: true));
        else
            result.Add(await botClient.SendMessage(chatId: _chatId, text: post.TextParts[0], linkPreviewOptions: linkPreviewOptions));

        for (var i = 1; i < post.TextParts.Length; ++i)
            result.Add(await botClient.SendMessage(chatId: _chatId, text: post.TextParts[i], linkPreviewOptions: linkPreviewOptions));


        return result;
    }

    private async Task<List<Message>> SendAudioAsync(TgPost post)
    {
        if (post.AudioFiles.Count == 0)
            return [];

        var botClient = botPool.GetClient();
        var messages = new List<Message>();
        var chunks = post.AudioFiles.Chunk(10);
        foreach (var chunk in chunks)
        {
            var chunkMessages = await botClient.SendMediaGroup(
                chatId: _chatId,
                media: chunk.Select(x => new InputMediaAudio(new InputFileUrl($"file://{x.ServerFullName}"))
                {
                    Title = x.Title,
                    Performer = x.Artist,
                    Duration = x.DurationSeconds
                })
            );
            messages.AddRange(chunkMessages);
        }

        return messages;
    }

    private async Task<Message[]> SendAudioArchiveAsync(TgPost post)
    {
        var botClient = botPool.GetClient();

        var messages = post.AudioArchives.Count switch
        {
            0 => [],
            1 => [await botClient.SendDocument(chatId: _chatId, document: new InputFileUrl($"file://{post.AudioArchives[0].ServerFullName}"))],
            _ => await botClient.SendMediaGroup(
                chatId: _chatId,
                media: post.AudioArchives!.Select(x => new InputMediaDocument(new InputFileUrl($"file://{x.ServerFullName}")))
            )
        };

        return messages;
    }

    public async Task DeleteMessagesAsync(int[] messagesIds)
    {
        var botClient = botPool.GetClient();

        try
        {
            var chunks = messagesIds.Chunk(100);
            foreach (var chunk in chunks)
                await botClient.DeleteMessages(_chatId, chunk);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400)
        {
            logger.LogWarning("Failed to delete message: {Ids}, sending request...", messagesIds);
            var sb = new StringBuilder("Delete these:\n");
            foreach (var id in messagesIds)
                sb.Append($"https://t.me/{_chatId.TrimStart('@')}/{id}\n");
            var textParts = TextHelper.GetTgTextParts(sb.ToString()); // smart move lmao
            foreach (var textPart in textParts)
                await botClient.SendMessage(_chatAdminId, textPart);


            // TODO: ensure that the message was deleted
        }
    }
}