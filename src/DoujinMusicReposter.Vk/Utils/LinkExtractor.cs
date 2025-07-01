using System.Text.RegularExpressions;

namespace DoujinMusicReposter.Vk.Utils;

public static partial class LinkExtractor
{
    [GeneratedRegex(@"https://pixeldrain\.com/u/[a-zA-Z0-9]{8}", RegexOptions.Compiled)]
    private static partial Regex PixeldrainLinkRegex();
    public static string[] GetPixeldrainLinks(string text) =>
        PixeldrainLinkRegex().Matches(text).Select(x => x.Value).ToArray();
}