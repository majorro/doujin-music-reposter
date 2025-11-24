using System.Text.RegularExpressions;
using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Telegram.Utils;

public static partial class TextHelper
{
    private const int MaxPhotoCaptionLength = 1024;
    private const int MaxTextMessageLength = 4096;
    private const int MaxFilenameLength = 157 - 40 - 7; // approx 40-47 full path
    private static readonly char[] ForbiddenFileNameChars = ['\u0005', '\u0000', '\u001F', '\u007F', '\u2400', '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\t', '\n', '\r', '\v'];

    [GeneratedRegex(@"\[#alias\|([^|]+)\|([^]]+)\]")]
    private static partial Regex VkMarkdownLinkRegex();
    [GeneratedRegex(@"\[(id\d+)\|@([^]]+)\]")]
    private static partial Regex VkUserMarkdownLinkRegex();

    public static string EnsureFilenameValidity(string text)
    {
        text = text.Trim(' ').TrimEnd('.');

        text = text.IndexOfAny(ForbiddenFileNameChars) == -1
            ? text
            : string.Join("_", text.Split(ForbiddenFileNameChars));

        var nameWithoutExtension  = Path.GetFileNameWithoutExtension(text);
        var extension = Path.GetExtension(text);
        var maxLength = MaxFilenameLength - extension.Length;
        return nameWithoutExtension.Length <= maxLength
            ? text
            : $"{nameWithoutExtension[..maxLength]}{extension}";
    }

    public static string[] GetPreparedText(VkPostDto vkPost, int vkGroupId)
    {
        var text = $"{vkPost.Text}\n\n{GetVkPostLink(vkPost, vkGroupId)}";
        return GetTgTextParts(text, vkPost.Photo is not null);
    }

    public static string ParseVkMarkdownLinks(string text) // TODO: add alias with post formatting
    {
        text = VkMarkdownLinkRegex().Replace(text, m => $"{m.Groups[1].Value}");
        text = VkUserMarkdownLinkRegex().Replace(text, m => $"https://vk.ru/{m.Groups[1].Value}");

        return text;
    }

    public static string[] GetTgTextParts(string text, bool hasPhoto)
    {
        var result = new List<string>();
        var curLength = 0;
        var curStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                var maxLength = result.Count == 0 && hasPhoto
                    ? MaxPhotoCaptionLength
                    : MaxTextMessageLength;
                if (curLength + i - curStart > maxLength)
                {
                    result.Add(text.Substring(curStart, i - curStart));
                    curStart = i;
                    curLength = 0;
                }
            }
            curLength++;
        }
        result.Add(text[curStart..]);
        return result.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private static string GetVkPostLink(VkPostDto vkPost, int vkGroupId) => $"https://vk.ru/wall-{vkGroupId}_{vkPost.Id}";
}