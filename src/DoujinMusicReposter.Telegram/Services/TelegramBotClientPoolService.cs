using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace DoujinMusicReposter.Telegram.Services;

// TODO: use queue (in vk too)
public class TelegramBotClientPoolService
{
    private readonly List<ITelegramBotClient> _botClients;
    private readonly Random _random;

    public TelegramBotClientPoolService(IServiceProvider serviceProvider)
    {
        _botClients = [];
        _random = new Random();

        var clients = serviceProvider.GetServices<ITelegramBotClient>();
        foreach (var client in clients)
            _botClients.Add(client);
    }

    public ITelegramBotClient GetClient()
    {
        var index = _random.Next(_botClients.Count);
        return _botClients[index];
    }
}