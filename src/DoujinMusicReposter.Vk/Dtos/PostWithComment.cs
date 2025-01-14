namespace DoujinMusicReposter.Vk.Dtos;

public record PostWithComment : Post
{
    public PostWithComment(Post post)
    {
        Photo = post.Photo;
        AudioArchives = post.AudioArchives;
        Id = post.Id;
        Text = post.Text;
    }

    public Comment? AuthorComment { get; internal set; }
}