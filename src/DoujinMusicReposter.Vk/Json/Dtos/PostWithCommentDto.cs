namespace DoujinMusicReposter.Vk.Json.Dtos;

public record PostWithCommentDto : PostDto
{
    public PostWithCommentDto(PostDto post)
    {
        Photo = post.Photo;
        AudioArchives = post.AudioArchives;
        Id = post.Id;
        Text = post.Text;
    }

    public CommentDto? AuthorComment { get; internal set; }
}