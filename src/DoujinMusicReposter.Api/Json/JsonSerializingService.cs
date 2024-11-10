using System.Text.Json;
using DoujinMusicReposter.Api.Json.Dtos;

namespace DoujinMusicReposter.Api.Json;

internal class JsonSerializingService
{
    public PostDto DeserializePost(JsonElement post)
    {
        var result = new PostDto();
        foreach (var prop in post.EnumerateObject())
        {
            if (prop.NameEquals("attachments"))
            {
                foreach (var attachment in prop.Value.EnumerateArray())
                {
                    if (attachment.TryGetProperty("photo", out var photo))
                        result.Photo = new Uri(photo.GetProperty("orig_photo").GetProperty("url").GetString()!);
                    else if (attachment.TryGetProperty("doc", out var doc))
                    {
                        var props = GetProperties(doc, "size", "url");
                        result.AudioArchives.Add(new AudioArchiveDto()
                        {
                            SizeBytes = props[0].GetInt64(),
                            Link = new Uri(props[1].GetString()!),
                        });
                    }
                }
            }
            else if (prop.NameEquals("id")) result.Id = prop.Value.GetInt32();
            else if (prop.NameEquals("text"))
            {
                result.Text = prop.Value.GetString()!;
                break;
            }
        }

        // TODO: verify data integrity

        return result;
    }

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
            throw new KeyNotFoundException($"Cannot find properties {string.Join(' ', propNames)} in {element.GetRawText()}");

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