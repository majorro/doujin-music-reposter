using System.Text;
using DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding.EncodingConstants;

namespace DoujinMusicReposter.Telegram.Tests.TextEncoding;

public class EncodingRepairServiceTests
{
    private readonly EncodingRepairingService _encodingRepairer;

    public EncodingRepairServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var mockLogger = new Mock<ILogger<EncodingRepairingService>>();
        _encodingRepairer = new EncodingRepairingService(mockLogger.Object);
    }

    [Theory]
    [InlineData("ザキヤマ - 夜光蝶")]
    [InlineData("нереальная - КИРИЛЛИЦА228")]
    [InlineData("asdf1234")]
    [InlineData("空色絵本 - 宇宙船")]
    [InlineData("04. 夕焼け [refio ver.].mp3")]
    [InlineData("高嶺 - The Escapist")]
    [InlineData("がきコ&すずしろ - MO・SHI・KA")]
    [InlineData("なゆ - Let`s☆Botti")]
    [InlineData("めらみぽっぷ & 藍咲みみ - メガストラクチャー feat. めらみぽっぷ & 藍咲みみ")]
    [InlineData("얼틀메이트_M3_31同人音楽_しちごさん。_夜を灯すランジェ_Vocal：_.rar")]
    [InlineData("削除 - [ ･A･]＜僕はクリーパー")]
    [InlineData("Nodon - Olá Meu nome é")]
    [InlineData("ginrei - Áed")]
    [InlineData("introspecção")]
    [InlineData("11. Distance (Jordi K-staña Remix).mp3")]
    [InlineData("DJ'TEKINA//SOMETHING feat. YUC'e")]
    [InlineData("Tanaken - Tình yêu đích thực")]
    public void TryFix_ShouldNotChangeString_WhenCorrect(string before)
    {
        var after = _encodingRepairer.TryFix(before);

        after.Should().NotBeNull();
        after.Should().Be(before);
    }

    [Theory]
    [InlineData("中島岬&&#22338;本千明", "中島岬&坂本千明")]
    public void TryFix_ShouldChangeString_WhenHtmlEncoded(string before, string after)
    {
        var afterActual = _encodingRepairer.TryFix(before);

        afterActual.Should().NotBeNull();
        afterActual.Should().Be(after);
    }

    [Theory]
    [InlineData("04 Г~ГЙБ[Г{Б[ГЛБEГ}ГWГbГNБEГKБ[ГЛ.mp3", Cp866)]
    [InlineData("04.ГCГOГМБ[ГУБiIgraineБj.mp3", Cp866)]
    [InlineData("ОWргГTГNГКГtГ@ГCГX.mp3", Cp866)]
    [InlineData("Xe - ÍéÉÆècÆÌe[}", Iso1)]
    [InlineData("Xe - ¢´ApðißI", Iso1)]
    [InlineData("Xe - veB[vYK[YÅ·", Iso1)]
    [InlineData("Xe - îTgEI[I", Iso1)]
    public void TryFix_ShouldChangeString_ToSjis_WhenBroken(string before, string fromEncoding) => AssertEncoding(before, fromEncoding, Sjis);

    [Theory]
    [InlineData("їХЙ«Ѕ}±ѕ - БчРЗ", Win1251)]
    [InlineData("01. ҐЁҐі©`.mp3", Win1251)]
    [InlineData("03. РЗКі.mp3", Win1251)]
    [InlineData("Ï«Ò¹¤ê¤ê - (C78)[Sentire] ¥»¥é¥Õ¤Î²Í - 02 ¥»¥é¥Õ¤Î²Í", Iso1)]
    [InlineData("(C78)[Sentire] ¥»¥é¥Õ¤Î²Í - 04 Ïû¤¨¤¿Ð¡øB¤òÌ½¤·¤Æ", Iso1)]
    [InlineData("lino (¤Ø¤Ã¤É¤Û¤ó¥È©`¥­¥ç©`) - ¤Þ¤¤¤Ê¤¹¡ú¤×¤é¤¹ (SequenceBodyGroove Mix)", Iso1)]
    [InlineData("MITSUNA - Precious Name ¡«ÊØ¤ë¤Ù¤­´æÔÚ¡«", Iso1)]
    public void TryFix_ShouldChangeString_ToGb_WhenBroken(string before, string fromEncoding) => AssertEncoding(before, fromEncoding, Gb);

    [Theory]
    [InlineData("«·«ã«ë«í", Iso15)]
    [InlineData("Þûð¶ - jaune d'or", Iso15)]
    [InlineData("«Ï«à - Coming home", Iso15)]
    [InlineData("ÊÈù» - Alfheim", Iso15)]
    public void TryFix_ShouldChangeString_ToKr_WhenBroken(string before, string fromEncoding) => AssertEncoding(before, fromEncoding, Kr);

    private void AssertEncoding(string before, string fromEncoding, string toEncoding)
    {
        var enc = Encoding.GetEncoding(fromEncoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var dec = Encoding.GetEncoding(toEncoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var afterCorrect = dec.GetString(enc.GetBytes(before));

        var after = _encodingRepairer.TryFix(before);

        after.Should().NotBeNull();
        after.Should().Be(afterCorrect);
    }
}

// unfixable...
// 12.е¦ененещед -еъете¦е-- - .mp3 // 12.スキキライ -リモコゲ- // probably to gb
// 03 +№д+еLй`епе¦едег & -vT-д+е¬езеще¬й` & ¦гды--д+=--~дид¬ШЄ].mp3 // 紅茜 [厄神様の通り道　~ Dark Road & 運命のダークサイド & 緑眼のジェラシー & 渡る者の途絶えた橋] // probably to gb
// 08. П+ВжВLВвЧ~(Instrumental).mp3 // 消えない欲(Instrumental) // unsupported MIK encoding or something
// 06 ВоВшБиВёВаВвВ¦ВрВёВ¬В-ВЯЦ¦.mp3 // ぐり→んあいどもんすたぁ娘 // broken cp866->sjis https://vk.ru/wall-60027733_1218