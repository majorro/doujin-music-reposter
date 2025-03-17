using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using UtfUnknown;
using static DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding.EncodingConstants;

namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;

public class EncodingRepairingService : IEncodingRepairingService
{
    private readonly ILogger<EncodingRepairingService> _logger;
    // sometimes unrecoverable from cp866
    private static readonly (string, string)[] Japanese =
    [
        (Iso1, Sjis),
        (Cp866, Sjis)
    ];
    // unrecoverable from cp866
    private static readonly (string, string)[] Chinese =
    [
        (Iso1, Gb),
        (Win1251, Gb)
    ];
    private static readonly (string, string)[] Korean =
    [
        (Iso15, Kr),
    ];

    private static readonly Dictionary<string, string> Unrecoverable = new()
    {
        { "Én†Dë~", "蒼咲雫" }
    };

    public EncodingRepairingService(ILogger<EncodingRepairingService> logger)
    {
        _logger = logger;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public string? TryFix(string? brokenString)
    {
        if (brokenString is null)
            return null;

        if (Unrecoverable.TryGetValue(brokenString, out var fixedString))
            return fixedString;

        // CharsetDetector perfectly detects japanese, so we can start with it
        foreach (var (from, to) in Japanese)
        {
            var (recoded, enc, dec) = TryRecode(brokenString, from, to);
            if (recoded is null)
                continue;

            var detected = CharsetDetector.DetectFromBytes(enc.GetBytes(brokenString)).Detected;
            if (detected is not null && detected.Encoding?.WebName == dec.WebName && detected.Confidence > 0.6)
                return recoded;
        }

        // all about chinese iso and korean iso is identical but there was no chinese iso15 so far
        foreach (var (from, to) in Korean)
        {
            var (recoded, enc, _) = TryRecode(brokenString, from, to);
            if (recoded is null)
                continue;

            var detected = CharsetDetector.DetectFromBytes(enc.GetBytes(brokenString)).Detected;
            if (detected is null || detected.Encoding?.WebName != enc.WebName) // not sure
            {
                var test = Encoding.GetEncoding(Eur).GetString(Encoding.GetEncoding(Eur).GetBytes(brokenString)); // unholy heuristics
                if (test != brokenString)
                    return recoded;
            }
        }

        // i hate chinese and latin 😭
        foreach (var (from, to) in Chinese)
        {
            var (recoded, enc, _) = TryRecode(brokenString, from, to);
            if (recoded is null)
                continue;

            var detected = CharsetDetector.DetectFromBytes(enc.GetBytes(brokenString)).Detected;
            if (detected is null || detected.Encoding?.WebName != enc.WebName) // not sure
            {
                var test = Encoding.GetEncoding(Eur).GetString(Encoding.GetEncoding(Eur).GetBytes(brokenString)); // unholy heuristics
                if (test != brokenString)
                    return recoded;
            }
        }

        brokenString = WebUtility.HtmlDecode(brokenString);

        return brokenString; // isn't it?
    }

    private static (string?, Encoding, Encoding) TryRecode(string brokenString, string from, string to)
    {
        var enc = Encoding.GetEncoding(from, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var dec = Encoding.GetEncoding(to, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        try { return (dec.GetString(enc.GetBytes(brokenString)), enc, dec); }
        catch { return (null, enc, dec); }
    }

    public static void PrintPossibleEncodings()
    {
        const string brokenString = "Én†Dë~";

        var encodings = Japanese.Concat(Chinese).Concat(Korean);
        foreach (var (from, to) in encodings)
        {
            var (recoded, _, _) = TryRecode(brokenString, from, to);
            if (recoded is not null)
                Console.WriteLine($"{from} -> {to}: {recoded}");
        }
    }
}