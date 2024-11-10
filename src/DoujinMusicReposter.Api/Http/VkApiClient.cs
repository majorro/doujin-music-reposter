using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;

namespace DoujinMusicReposter.Api.Http;

// TODO: refactor object creation spam for queryParams
internal class VkApiClient
{
    private const int GroupId = 60027733;
    private static readonly Uri ApiHost = new("https://api.vk.ru/method/");
    private static readonly KeyValuePair<string, string>[] CommonQueryParams =
    [
        new("v", "5.199")
    ];

    private readonly HttpClient _httpClient;

    public VkApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Stream> GetPostsAsync(int offset = 0, int count = 100)
    {
        const string method = "wall.get";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-GroupId).ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),
        };

        return await _httpClient.GetStreamAsync(GetQuery(method, queryParams));
    }

    public async Task<Stream> GetCommentsAsync(int postId, int offset = 0, int count = 100)
    {
        const string method = "wall.getComments";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-GroupId).ToString()),
            new("post_id", postId.ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),

            new("need_likes", "0"),
            new("sort", "asc"),
            new("preview_length", "1"),
        };

        return await _httpClient.GetStreamAsync(GetQuery(method, queryParams));
    }

    internal async Task<Stream> GetLongPollServerAsync()
    {
        const string method = "groups.getLongPollServer";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("group_id", GroupId.ToString()),
        };

        return await _httpClient.GetStreamAsync(GetQuery(method, queryParams));
    }

    internal async Task<Stream> GetNewEvents(string server, string key, int lastEventNumber)
    {
        const string method = "";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("act", "a_check"),
            new("key", key),
            new("ts", lastEventNumber.ToString()),
            new("wait", "25"),
        };

        return await _httpClient.GetStreamAsync(GetQuery(method, queryParams, new Uri(server)));
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