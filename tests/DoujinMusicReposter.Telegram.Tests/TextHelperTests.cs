using DoujinMusicReposter.Telegram.Utils;
using FluentAssertions;

namespace DoujinMusicReposter.Telegram.Tests;

public class TextHelperTests
{
    [Theory]
    [InlineData("[#alias|boosty.to/dmadmin|https://boosty.to/dmadmin]", "boosty.to/dmadmin")]
    [InlineData("[id1989564|@id1989564]", "https://vk.ru/id1989564")]
    [InlineData("[id362953542|@shometsusha]", "https://vk.ru/id362953542")]
    public void ParseVkLinks_ShouldParseCorrectly(string before, string expected)
    {
        var result = TextHelper.ParseVkMarkdownLinks(before);

        result.Should().Be(expected);
    }
}