﻿using DoujinMusicReposter.Vk.Dtos;

namespace DoujinMusicReposter.Telegram.Utils;

public static class TextHelper // TODO: use it some wrapping client?
{
    private const int MAX_PHOTO_CAPTION_LENGTH = 1024;
    private const int MAX_TEXT_MESSAGE_LENGTH = 4096;
    private static readonly char[] ForbiddenFileNameChars = ['\u2400', '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\t', '\n', '\r'];

    public static string EnsureFilenameValidity(string text)
    {
        text = text.Trim(' ').TrimEnd('.');

        return text.IndexOfAny(ForbiddenFileNameChars) == -1
            ? text
            : string.Join("_", text.Split(ForbiddenFileNameChars));
    }

    public static string[] GetPreparedText(Post post, int vkGroupId)
    {
        var text = $"{post.Text}\n\n{GetVkPostLink(post, vkGroupId)}";
        return GetTgTextParts(text);
    }

    public static string[] GetTgTextParts(string text)
    {
        var result = new List<string>();
        var curLength = 0;
        var curStart = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                var maxLength = result.Count == 0 ? MAX_PHOTO_CAPTION_LENGTH : MAX_TEXT_MESSAGE_LENGTH; // first msg is probably with photo
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
        return result.ToArray();
    }

    private static string GetVkPostLink(Post post, int vkGroupId) => $"https://vk.com/wall-{vkGroupId}_{post.Id}";
}