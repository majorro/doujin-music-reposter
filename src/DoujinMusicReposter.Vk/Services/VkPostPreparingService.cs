using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http;
using DoujinMusicReposter.Vk.Http.Exceptions;
using DoujinMusicReposter.Vk.Utils;

namespace DoujinMusicReposter.Vk.Services;

public class VkPostPreparingService(IVkApiClient vkClient)
{
    public async Task<int> PrepareAsync(VkPostDto post, CancellationToken ctk = default)
    {
        var response = await vkClient.GetCommentsAsync(post.Id, count: 5, ctk: ctk);
        if (!response.IsSuccess || response.Data is null)
            throw new VkApiException($"Failed to get comments for PostId={post.Id}: {response}");

        var authorComments = response.Data.Comments.Where(x => x.IsFromAuthor).ToArray();
        var vkAudioArchives = authorComments.SelectMany(x => x.AudioArchives).ToArray();
        post.VkAudioArchives.AddRange(vkAudioArchives);

        post.PixelDrainAudioArchives.AddRange(LinkExtractor.GetPixeldrainLinks(post.Text)
            .Select(x => new PixelDrainAudioArchiveDto(new Uri(x))));
        post.PixelDrainAudioArchives.AddRange(authorComments
            .SelectMany(x => LinkExtractor.GetPixeldrainLinks(x.Text ?? ""))
            .Select(x => new PixelDrainAudioArchiveDto(new Uri(x))));

        return vkAudioArchives.Length;
    }
}