namespace DoujinMusicReposter.Telegram.Services.TgPostBuilding.TextEncoding;

public interface IEncodingRepairingService
{
    string? TryFix(string? brokenString);
}