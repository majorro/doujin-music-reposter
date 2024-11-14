using System.Text.Json;
using DoujinMusicReposter.Vk.Json.Dtos;
using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Vk.Json;

// TODO: add tests, handle errors
public class JsonSerializingService(ILogger<JsonSerializingService> logger) : IJsonSerializingService
{
    public (int TotalCount, List<PostDto> Posts) ParseGetPostsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var response = doc.RootElement.GetProperty("response");
        var items = response.GetProperty("items");

        var totalCount = response.GetProperty("count").GetInt32();
        var posts = new List<PostDto>();
        foreach (var post in items.EnumerateArray())
        {
            var postDto = DeserializePost(post);
            if (postDto is not null) posts.Add(postDto);
            else logger.LogWarning("Failed to deserialize post: {Post}", post.GetRawText());
        }

        return (totalCount, posts);
    }

    public List<CommentDto> ParseGetCommentsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var response = doc.RootElement.GetProperty("response");
        var items = response.GetProperty("items");

        var comments = new List<CommentDto>();
        foreach (var comment in items.EnumerateArray())
            comments.Add(DeserializeComment(comment));

        return comments;
    }

    public LongPollingServerConfigDto ParseGetLongPollServerResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var response = doc.RootElement.GetProperty("response");

        var props = GetProperties(response, "key", "server", "ts").Select(p => p.GetString()!).ToList();
        return new LongPollingServerConfigDto
        {
            Key = props[0],
            Server = props[1],
            LastEventNumber = props[2],
        };
    }

    public (string LastEventNumber, List<PostDto> Posts)? ParseGetNewEventsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.TryGetProperty("failed", out var failed))
        {
            var code = failed.GetInt32();
            if (code == 1)
                return (doc.RootElement.GetProperty("ts").GetString()!, []);

            return null; // outdated server config
        }

        var updates = doc.RootElement.GetProperty("updates");

        var lastEventNumber = doc.RootElement.GetProperty("ts").GetString()!;
        var posts = new List<PostDto>();
        foreach (var update in updates.EnumerateArray())
        {
            var post = update.GetProperty("object");
            var postDto = DeserializePost(post);
            if (postDto is not null) posts.Add(postDto);
            else logger.LogWarning("Failed to deserialize post: {Post}", post.GetRawText());
        }

        return (lastEventNumber, posts);
    }

    private static CommentDto DeserializeComment(JsonElement comment)
    {
        var result = new CommentDto();
        foreach (var prop in comment.EnumerateObject())
        {
            if (prop.NameEquals("id"))
                result.Id = prop.Value.GetInt32();
            else if (prop.NameEquals("text"))
                result.Text = prop.Value.GetString()!;
            else if (prop.NameEquals("attachments"))
            {
                foreach (var attachment in prop.Value.EnumerateArray())
                {
                    if (TryDeserializeAudioArchive(attachment, out var audioArchive))
                        result.AudioArchives.Add(audioArchive!);
                }

                break;
            }
            else if (prop.NameEquals("is_from_post_author"))
                result.IsFromAuthor = true;
        }

        return result;
    }

    private static PostDto? DeserializePost(JsonElement post)
    {
        var result = new PostDto();
        foreach (var prop in post.EnumerateObject())
        {
            if (prop.NameEquals("post") && prop.Value.GetString() != "post")
                return null;

            if (prop.NameEquals("attachments"))
            {
                foreach (var attachment in prop.Value.EnumerateArray())
                {
                    if (attachment.TryGetProperty("photo", out var photo))
                        result.Photo = new Uri(photo.GetProperty("orig_photo").GetProperty("url").GetString()!);
                    else if (TryDeserializeAudioArchive(attachment, out var audioArchive))
                        result.AudioArchives.Add(audioArchive!);
                }
            }
            else if (prop.NameEquals("id"))
                result.Id = prop.Value.GetInt32();
            else if (prop.NameEquals("text"))
            {
                result.Text = prop.Value.GetString()!;
                break;
            }
        }

        // TODO: verify data integrity?

        return result;
    }

    private static bool TryDeserializeAudioArchive(JsonElement attachment, out AudioArchiveDto? audioArchive)
    {
        audioArchive = null;
        if (!attachment.TryGetProperty("doc", out var doc))
            return false;

        var props = GetProperties(doc, "size", "type", "url");

        var type = props[1].GetInt32();
        if (!IsAudioArchive(type))
            return false;

        audioArchive = new AudioArchiveDto()
        {
            SizeBytes = props[0].GetInt64(),
            Link = new Uri(props[2].GetString()!),
        };
        return true;
    }

    private static bool IsAudioArchive(int docType) => docType is 2 or 8;

    private static List<JsonElement> GetProperties(JsonElement element, params string[] propNames)
    {
        var result = new List<JsonElement>(propNames.Length);

        var i = 0;
        foreach (var prop in element.EnumerateObject())
        {
            if (!prop.NameEquals(propNames[i])) continue;

            result.Add(prop.Value);
            if (++i == propNames.Length) break;
        }

        if (result.Count != propNames.Length)
            throw new KeyNotFoundException(
                $"Cannot find properties {string.Join(' ', propNames)} in {element.GetRawText()}");

        return result;
    }

    private static JsonElement? TryEnumerateToProperty(JsonElement.ObjectEnumerator enumerator, string propName)
    {
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.NameEquals(propName))
                return enumerator.Current.Value;
        }

        return null;
    }
}