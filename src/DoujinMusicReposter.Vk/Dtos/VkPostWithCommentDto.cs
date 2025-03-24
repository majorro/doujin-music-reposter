namespace DoujinMusicReposter.Vk.Dtos;

public record VkPostWithCommentDto : VkPostDto
{
    public VkPostWithCommentDto(VkPostDto vkPost)
    {
        Photo = vkPost.Photo;
        AudioArchives = vkPost.AudioArchives;
        Id = vkPost.Id;
        Text = vkPost.Text;
    }

    public VkCommentDto? AuthorComment { get; internal set; }
}