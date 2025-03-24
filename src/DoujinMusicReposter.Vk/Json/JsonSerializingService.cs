using System.Text.Json;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http.Dtos;
using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Vk.Json;

// TODO: throw instead of log.error
public class JsonSerializingService(ILogger<JsonSerializingService> logger) : IJsonSerializingService
{
    public VkResponse<GetPostsResponse> ParseGetPostsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var error = TryGetError<GetPostsResponse>(root);
        if (error is not null) return error;

        var response = root.GetProperty("response");
        var items = response.GetProperty("items");

        var totalCount = response.GetProperty("count").GetInt32();
        var posts = new List<VkPostDto>();
        foreach (var post in items.EnumerateArray())
        {
            var postDto = DeserializePost(post);
            if (postDto is not null) posts.Add(postDto);
            else
            {
                post.TryGetProperty("id", out var id);
                logger.LogWarning("Failed to deserialize post: PostId={PostId}", id);
            }
        }

        return new VkResponse<GetPostsResponse>(new GetPostsResponse(totalCount, posts));
    }

    // TODO: add audiofiles
    public VkResponse<GetCommentsResponse> ParseGetCommentsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var error = TryGetError<GetCommentsResponse>(root);
        if (error is not null) return error;

        var response = root.GetProperty("response");
        var items = response.GetProperty("items");

        var comments = new List<VkCommentDto>();
        foreach (var comment in items.EnumerateArray())
            comments.Add(DeserializeComment(comment));

        return new VkResponse<GetCommentsResponse>(new GetCommentsResponse(comments));
    }

    public VkResponse<GetLongPollServerResponse> ParseGetLongPollServerResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var error = TryGetError<GetLongPollServerResponse>(root);
        if (error is not null) return error;

        var response = root.GetProperty("response");

        var props = GetProperties(response, "key", "server", "ts").Select(p => p.GetString()!).ToList();
        var config = new LongPollingServerConfigDto
        {
            Key = props[0],
            Server = props[1],
            Timestamp = props[2],
        };

        return new VkResponse<GetLongPollServerResponse>(new GetLongPollServerResponse(config));
    }

    public VkResponse<GetNewEventsResponse> ParseGetNewEventsResponse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (root.TryGetProperty("failed", out var failed))
        {
            var code = failed.GetInt32();
            switch (code)
            {
                case 1:
                    var response = new GetNewEventsResponse(root.GetProperty("ts").GetString()!, []);
                    return new VkResponse<GetNewEventsResponse>(response);
                case 2:
                    return new VkResponse<GetNewEventsResponse>(code, "The key has expired, you need to re-receive key.");
                case 3:
                    return new VkResponse<GetNewEventsResponse>(code, "Information is lost, you need to request new key and ts.");
                default:
                    return new VkResponse<GetNewEventsResponse>(code, "Unknown error");
            }
        }

        var updates = root.GetProperty("updates");

        var timestamp = root.GetProperty("ts").GetString()!;
        var posts = new List<VkPostDto>();
        foreach (var update in updates.EnumerateArray())
        {
            var type = update.GetProperty("type").GetString();
            if (type != "wall_post_new")
                continue;

            var post = update.GetProperty("object");
            var postDto = DeserializePost(post);
            if (postDto is not null) posts.Add(postDto);
            else
            {
                post.TryGetProperty("id", out var id);
                logger.LogWarning("Failed to deserialize post: PostId={PostId}", id);
            }
        }

        return new VkResponse<GetNewEventsResponse>(new GetNewEventsResponse(timestamp, posts));
    }

    private static VkResponse<T>? TryGetError<T>(JsonElement root) where T : IResponseDto
    {
        if (!root.TryGetProperty("error", out var error)) return null;
        var code = error.GetProperty("error_code").GetInt32();
        var message = error.GetProperty("error_msg").GetString()!;
        return new VkResponse<T>(code, message);
    }

    private static VkCommentDto DeserializeComment(JsonElement comment)
    {
        var result = new VkCommentDto();
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
            }
            else if (prop.NameEquals("is_from_post_author"))
                result.IsFromAuthor = true;
        }

        return result;
    }

    private static VkPostDto? DeserializePost(JsonElement post)
    {
        var result = new VkPostDto();
        foreach (var prop in post.EnumerateObject())
        {
            if ((prop.NameEquals("type") && prop.Value.GetString() != "post") ||
                (prop.NameEquals("inner_type") && prop.Value.GetString() != "wall_wallpost"))
                return null;

            if (prop.NameEquals("donut"))
                result.IsDonut = prop.Value.TryGetProperty("is_donut", out var isDonut) && isDonut.GetBoolean();
            else if (prop.NameEquals("attachments"))
            {
                foreach (var attachment in prop.Value.EnumerateArray())
                {
                    if (attachment.TryGetProperty("photo", out var photo))
                        result.Photo = new Uri(photo.GetProperty("orig_photo").GetProperty("url").GetString()!);
                    else if (TryDeserializeAudioArchive(attachment, out var audioArchive))
                        result.AudioArchives.Add(audioArchive!);
                    else if (TryDeserializeAudio(attachment, out var audio))
                        result.Audios.Add(audio!);
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

        if (string.IsNullOrEmpty(result.Text) && result.Photo is null &&
            result.AudioArchives.Count == 0 && result.Audios.Count == 0) // TODO: return something else? for logging purposes
            return null; // repost

        return result;
    }

    private static bool TryDeserializeAudioArchive(JsonElement attachment, out VkAudioArchiveDto? audioArchive)
    {
        audioArchive = null;
        if (!attachment.TryGetProperty("doc", out var doc))
            return false;

        var props = GetProperties(doc, "title", "size", "ext", "type", "url");

        var type = props[3].GetInt32();
        if (!IsAudioArchive(type))
            return false;

        var ext = props[2].GetString()!;
        var fileName = props[0].GetString()!;
        if (!fileName.EndsWith(ext))
            fileName = $"{fileName}.{ext}";
        audioArchive = new VkAudioArchiveDto()
        {
            SizeBytes = props[1].GetInt64(),
            FileName = fileName,
            Link = new Uri(props[4].GetString()!),
        };

        audioArchive.FileName = !audioArchive.FileName.EndsWith('1') ? audioArchive.FileName : audioArchive.FileName[..^1];

        return true;
    }

    private static bool TryDeserializeAudio(JsonElement attachment, out VkAudioDto? audio)
    {
        audio = null;
        if (!attachment.TryGetProperty("audio", out var audioElement))
            return false;

        var props = GetProperties(audioElement, "artist", "title", "duration", "url");
        if (string.IsNullOrWhiteSpace(props[3].GetString())) // restricted
            return false;

        audio = new VkAudioDto()
        {
            Artist = props[0].GetString()!,
            Title = props[1].GetString()!,
            DurationSeconds = props[2].GetInt32(),
            Link = new Uri(props[3].GetString()!),
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