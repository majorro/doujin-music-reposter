using System.Text;
using System.Web;
using DoujinMusicReposter.Vk.Json.Dtos;
using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Vk.Http;

// TODO: refactor object creation spam for queryParams
public class VkApiClient(HttpClient httpClient) : IVkApiClient
{
    private const int GroupId = 60027733;
    private static readonly Uri ApiHost = new("https://api.vk.ru/method/");
    private static readonly KeyValuePair<string, string>[] CommonQueryParams =
    [
        new("v", "5.199")
    ];

    public async Task<Stream> GetPostsAsync(int offset = 0, int count = 100)
    {
        const string method = "wall.get";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-GroupId).ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),
        };

        return await httpClient.GetStreamAsync(GetQuery(method, queryParams));
    }

    public async Task<Stream> GetCommentsAsync(int postId, int offset = 0, int count = 100, int previewLength = 0)
    {
        const string method = "wall.getComments";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-GroupId).ToString()),
            new("post_id", postId.ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),
            new("preview_length", previewLength.ToString()),

            new("need_likes", "0"),
            new("sort", "asc"),
        };

        return await httpClient.GetStreamAsync(GetQuery(method, queryParams));
    }

    public async Task<Stream> GetLongPollServerAsync()
    {
        const string method = "groups.getLongPollServer";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("group_id", GroupId.ToString()),
        };

        var request = new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(GetQuery(method, queryParams)),
            Headers =
            {
                { "Authorization", $"Bearer " } // TODO: add community key from options
            }
        };
        var response = await httpClient.SendAsync(request);

        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<Stream> GetNewEvents(LongPollingServerConfigDto config)
    {
        const string method = "";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("act", "a_check"),
            new("key", config.Key),
            new("ts", config.LastEventNumber),
            new("wait", "25"),
        };

        return await httpClient.GetStreamAsync(GetQuery(method, queryParams, new Uri(config.Server)));
    }

    private static string GetQuery(string method, KeyValuePair<string, string>[]? additionalParams = null, Uri? apiHost = null) =>
        $"{apiHost ?? ApiHost}{method}{GetQueryString(additionalParams)}";

    private static string GetQueryString(KeyValuePair<string, string>[]? additionalParams = null)
    {
        var sb = new StringBuilder("?");
        foreach (var queryParam in CommonQueryParams)
            sb.Append($"{ToQueryString(queryParam)}&");
        if (additionalParams != null)
            foreach (var queryParam in additionalParams)
                sb.Append($"{ToQueryString(queryParam)}&");
        sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }

    private static string ToQueryString(KeyValuePair<string, string> queryParam) =>
        $"{queryParam.Key}={HttpUtility.UrlEncode(queryParam.Value)}";
}