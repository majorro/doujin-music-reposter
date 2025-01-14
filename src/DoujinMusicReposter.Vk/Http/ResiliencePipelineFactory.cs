using DoujinMusicReposter.Vk.Http.Dtos;
using DoujinMusicReposter.Vk.Http.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;
using Polly.Retry;

namespace DoujinMusicReposter.Vk.Http;

internal static class ResiliencePipelineFactory
{
    private static readonly int[] NotThrowingErrorCodes = [1, 6, 9, 10, 29, 19, 212];
    private static readonly Dictionary<Type, object> Pipelines = new();

    public static ResiliencePipeline<VkResponse<T>> Get<T>(ILogger logger) where T : IResponseDto
    {
        if (Pipelines.TryGetValue(typeof(T), out var result))
            return (ResiliencePipeline<VkResponse<T>>)result;

        var pipeline = new ResiliencePipelineBuilder<VkResponse<T>>()
            .AddRetry(new RetryStrategyOptions<VkResponse<T>>
            {
                Name = "VkApiRetryShort",
                MaxRetryAttempts = 5,
                BackoffType = DelayBackoffType.Linear,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = new PredicateBuilder<VkResponse<T>>().HandleResult(x => x.ErrorCode is 6 or 9 or 29),
                OnRetry = response =>
                {
                    logger.LogWarning("Failed to get {Type}: {Error}, retrying...", typeof(T).Name, response.Outcome.Result);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions<VkResponse<T>>
            {
                Name = "VkApiRetryLong",
                MaxRetryAttempts = 10,
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<VkResponse<T>>().HandleResult(x => x.ErrorCode is 1 or 10),
                OnRetry = response =>
                {
                    logger.LogWarning("Failed to get {Type}: {Error}, retrying...", typeof(T).Name, response.Outcome.Result);
                    return default;
                }
            })
            .AddFallback(new FallbackStrategyOptions<VkResponse<T>> // using for pretty the opposite xD
            {
                Name = "VkApiThrow",
                ShouldHandle = new PredicateBuilder<VkResponse<T>>().HandleResult(
                    x => !x.IsSuccess && !NotThrowingErrorCodes.Contains(x.ErrorCode ?? int.MinValue)),
                FallbackAction = response =>
                    throw new VkApiException($"Failed to get {typeof(T).Name}: {response.Outcome.Result}"),
            })
            .Build();

        Pipelines[typeof(T)] = pipeline;
        return pipeline;
    }
}