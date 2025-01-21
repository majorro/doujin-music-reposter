using System.Diagnostics;
using System.Threading.Channels;
using DoujinMusicReposter.Persistence;
using DoujinMusicReposter.Telegram.Services;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;
using DoujinMusicReposter.Telegram.Setup.Configuration;
using DoujinMusicReposter.Telegram.Utils;
using DoujinMusicReposter.Vk.Dtos;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace DoujinMusicReposter.App.Workers;

internal class PostProcessingWorker(
    ILogger<PostProcessingWorker> logger,
    ChannelReader<Post> postBuildingQueueReader,
    TgPostBuildingService postBuilder,
    PostsManagingService poster,
    PostsRepository postsDb,
    TelegramBotClientPoolService botPool,
    IOptions<TgConfig> tgConfig) : BackgroundService
{
    private const int PostPrebuildLimit = 10; // TODO: to config

    protected override async Task ExecuteAsync(CancellationToken ctk)
    {
        // var poost = await postBuilder.BuildAsync(new Post(
        //     id: 67009,
        //     text: "[Touhou Kouroumu 8] #TK8@doujinmusic \n#electronic #touhou\nSULFURIC ACID PRODUCT - 狂乱音楽室 -LUNATIC JUKEBOX-",
        //     photo: new Uri("https://sun9-56.userapi.com/s/v1/ig2/xi-KjY4qinMvQhzwTJNathyS8xH_RSXAlkFeg2FOaHIAyp5eJ4kVe37vo1rsemFvV2osfGf90fvsvm1buknmx74b.jpg?quality\u003d96\u0026as\u003d32x32,48x48,72x72,108x108,160x160,240x240,360x360,480x480,540x540,640x640,720x720,800x800\u0026from\u003dbu"),
        //     audioArchives: new List<AudioArchive>()
        //     {
        //         // orig
        //         // new(link: new Uri("https://vk.com/doc15149684_683218339?hash\u003djrwR50iBJ0dMzdfNlYToFG2zSDf1dhbx5vbU0NxCez8\u0026dl\u003diAfg94SskyU1g4Y2LWeyXCO54XAVpHwOP0mNzRYz834\u0026api\u003d1\u0026no_preview\u003d1"),
        //         //     fileName: "SULFURIC ACID PRODUCT - 狂乱音楽室 -Lunatic Jukebox- [AAC].zip",
        //         //     sizeBytes: 119142525),
        //
        //         // happypills
        //         // new(link: new Uri("https://vk.com/doc15149684_667215269?hash=mOz96wC4Ycizg6b6Eq8KNPz0GFbNzsQJYSdz00l2lQ4&dl=eTHGCRV5k9LAxFhubvB1harkreqpXeB50itNGWqvckL\u0026api\u003d1\u0026no_preview\u003d1"),
        //         //     sizeBytes: 119142525),
        //
        //         // rar
        //         // new(link: new Uri("https://vk.com/doc15149684_232453494?hash=B4qUwhRvMpFz4kZkDb7EpJ12Eu2Zb0kaQINYOdl4nA0&dl=HNFyE8jQHO8EIcA4Xoo1mgC9vW5swgFTgAPpvbOlOSc\u0026api\u003d1\u0026no_preview\u003d1"),
        //         //     fileName: "【C84】[六弦アリス]不思議の国の音哲樂\u3000まやかし篇(v0+bk).rar",
        //         //     sizeBytes: 119142525),
        //
        //         // big
        //         new(link: new Uri("https://vk.com/doc15149684_682942046?hash=26MMjVWsj7AXapZPa9LLByRAW2zi2erYSeNrjpVRhgD&dl=lQdmvENiqPizcOGpKJAzl5cJuA6rwuJ3Xo8jRKBzsOL\u0026api\u003d1\u0026no_preview\u003d1"),
        //             fileName: "axsword__nexuz_-_onoken_vocal_collection_FLAC.zip",
        //             sizeBytes: 2662953315),
        //     }
        // ));
        // await poster.SendAsync(poost);

        var postingQueue = Channel.CreateBounded<(int Id, TgPost TgPost)>(new BoundedChannelOptions(PostPrebuildLimit)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        try
        {
            var firstTask = await Task.WhenAny(
                StartPostingAsync(postingQueue.Reader, ctk),
                StartPostBuildingAsync(postingQueue.Writer, ctk)
            );
            if (firstTask.Exception is not null) // huh?
                throw firstTask.Exception;
        }
        catch (Exception e)
        {
            // TODO: use serilog with tg sink
            var tgClient = botPool.GetClient();
            var textParts = TextHelper.GetTgTextParts($"ВСЁ В ДЕРЬМЕ:\n{e}");
            foreach (var textPart in textParts)
                await tgClient.SendMessage(tgConfig.Value.ChatAdminId, textPart, cancellationToken: CancellationToken.None);
            throw;
        }
    }

    private async Task StartPostBuildingAsync(ChannelWriter<(int Id, TgPost TgPost)> postingQueueWriter, CancellationToken ctk)
    {
        while (!ctk.IsCancellationRequested)
        {
            var post = await postBuildingQueueReader.ReadAsync(ctk);
            if (postsDb.GetByVkId(post.Id) is not null)
                continue;

            var tgPost = await postBuilder.BuildAsync(post); // TODO: continuewith to reduce blocking?
            await postingQueueWriter.WriteAsync((post.Id, tgPost), ctk);
        }
    }

    private async Task StartPostingAsync(ChannelReader<(int Id, TgPost TgPost)> postingQueueReader, CancellationToken ctk)
    {
        var timer = new Stopwatch();
        try
        {
            while (!ctk.IsCancellationRequested)
            {
                var (postId, tgPost) = await postingQueueReader.ReadAsync(ctk);
                if (postsDb.GetByVkId(postId) is not null)
                    continue;

                logger.LogInformation("Posting PostId={PostId}", postId);
                timer.Restart();
                var messageIds = await poster.SendAsync(tgPost);
                postsDb.Put(postId, messageIds);
                timer.Stop();
                logger.LogInformation("Posted PostId={PostId} in {Elapsed}", postId, timer.Elapsed);
            }
        }
        finally
        {
            postsDb.ForceSaveChanges(); // TODO: needed?
        }
    }
}