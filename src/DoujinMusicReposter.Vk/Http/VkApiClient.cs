using System.Net.Http.Headers;
using System.Text;
using System.Web;
using DoujinMusicReposter.Vk.Dtos;
using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Json;
using DoujinMusicReposter.Vk.Setup.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoujinMusicReposter.Vk.Http;

// TODO: try https://dev.vk.ru/ru/method/execute
// TODO: request logging
public class VkApiClient(
    ILogger<VkApiClient> logger,
    IOptions<VkConfig> vkConfig,
    HttpClient httpClient,
    IJsonSerializingService serializer) : IVkApiClient
{
    private static readonly Random Random = new();
    private static readonly KeyValuePair<string, string>[] CommonQueryParams =
    [
        new("v", "5.199")
    ];

    private readonly Uri _apiHost = vkConfig.Value.ApiHost;
    private readonly int _groupId = vkConfig.Value.GroupId;
    private readonly string _groupToken = vkConfig.Value.GroupToken;

    public async Task<VkResponse<GetPostsResponse>> GetPostsAsync(int offset = 0, int count = 100)
    {
        const string method = "wall.get";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-_groupId).ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),
        };

        var resiliencePipeline = ResiliencePipelineFactory.Get<GetPostsResponse>(logger);
        return await resiliencePipeline.ExecuteAsync(async ctk =>
        {
            RandomizeAuthToken();
            await using var stream = await httpClient.GetStreamAsync(GetQuery(method, queryParams), ctk);
            return serializer.ParseGetPostsResponse(stream);
        });
    }

    public async Task<VkResponse<GetCommentsResponse>> GetCommentsAsync(int postId, int offset = 0, int count = 100, int previewLength = 0)
    {
        const string method = "wall.getComments";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("owner_id", (-_groupId).ToString()),
            new("post_id", postId.ToString()),
            new("offset", offset.ToString()),
            new("count", count.ToString()),
            new("preview_length", previewLength.ToString()),

            new("need_likes", "0"),
            new("sort", "asc"),
        };

        var resiliencePipeline = ResiliencePipelineFactory.Get<GetCommentsResponse>(logger);
        return await resiliencePipeline.ExecuteAsync(async ctk =>
        {
            RandomizeAuthToken();
            await using var stream = await httpClient.GetStreamAsync(GetQuery(method, queryParams), ctk);
            return serializer.ParseGetCommentsResponse(stream);
        });
    }

    public async Task<VkResponse<GetLongPollServerResponse>> GetLongPollServerAsync()
    {
        const string method = "groups.getLongPollServer";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("group_id", _groupId.ToString()),
        };

        var request = new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(GetQuery(method, queryParams)),
            Headers =
            {
                { "Authorization", $"Bearer {_groupToken}" }
            }
        };

        var resiliencePipeline = ResiliencePipelineFactory.Get<GetLongPollServerResponse>(logger);
        return await resiliencePipeline.ExecuteAsync(async ctk =>
        {
            var response = await httpClient.SendAsync(request, ctk);
            await using var stream = await response.Content.ReadAsStreamAsync(ctk);
            return serializer.ParseGetLongPollServerResponse(stream);
        });
    }

    // errors 1,2,3 should be handled by caller
    public async Task<VkResponse<GetNewEventsResponse>> GetNewEvents(LongPollingServerConfigDto config, CancellationToken ctk = default)
    {
        const string method = "";
        var queryParams = new KeyValuePair<string, string>[]
        {
            new("act", "a_check"),
            new("key", config.Key),
            new("ts", config.Timestamp),
            new("wait", "25"),
        };

        await using var stream = await httpClient.GetStreamAsync(GetQuery(method, queryParams, new Uri(config.Server)), ctk);
        return serializer.ParseGetNewEventsResponse(stream);
    }

    // TODO: rewrite cringe
    private string GetQuery(string method, KeyValuePair<string, string>[]? additionalParams = null, Uri? apiHost = null) =>
        $"{apiHost ?? _apiHost}{method}{GetQueryString(additionalParams)}";

    private static string GetQueryString(KeyValuePair<string, string>[]? additionalParams = null)
    {
        var queryParams = additionalParams == null
            ? CommonQueryParams
            : CommonQueryParams.Concat(additionalParams);

        var sb = new StringBuilder("?");
        foreach (var queryParam in queryParams)
            sb.Append($"{ToQueryString(queryParam)}&");
        sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }

    private static string ToQueryString(KeyValuePair<string, string> queryParam) =>
        $"{queryParam.Key}={HttpUtility.UrlEncode(queryParam.Value)}";

    private void RandomizeAuthToken()
    {
        var tokenIndex = Random.Next(0, vkConfig.Value.AppTokens.Length);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vkConfig.Value.AppTokens[tokenIndex]);
    }
}