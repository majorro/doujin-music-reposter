using System.Diagnostics;
using System.Threading.Channels;
using DoujinMusicReposter.Persistence.Repositories.Interfaces;
using DoujinMusicReposter.Telegram.Services.Interfaces;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.Models;
using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.App.Workers;

internal class PostProcessingWorker(
    ILogger<PostProcessingWorker> logger,
    ChannelReader<VkPostDto> postBuildingQueueReader,
    TgPostBuildingService postBuilder,
    IPostsManagingService poster,
    IPostsRepository postsDb) : BackgroundService
{
    private const int POST_PREBUILD_LIMIT = 10; // TODO: to config

    protected override async Task ExecuteAsync(CancellationToken ctk)
    {
        var postingQueue = Channel.CreateBounded<(int Id, TgPost TgPost)>(new BoundedChannelOptions(POST_PREBUILD_LIMIT)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var firstTask = await Task.WhenAny(
            StartPostingAsync(postingQueue.Reader, ctk),
            StartPostBuildingAsync(postingQueue.Writer, ctk)
        );
        if (firstTask.Exception is not null) // huh?
            throw firstTask.Exception;
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
                var post = await postingQueueReader.ReadAsync(ctk);
                var postId = post.Id;
                using var tgPost = post.TgPost;
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