namespace DoujinMusicReposter.Vk.Http.Dtos;

public record VkResponse<T>
    where T : IResponseDto
{
    public T? Data { get; init; }
    public int? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public bool IsSuccess => ErrorCode == null;

    public VkResponse(T data)
    {
        Data = data;
    }

    public VkResponse(int errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public override string? ToString() =>
        IsSuccess
            ? base.ToString()
            : $"{ErrorCode}: {ErrorMessage}";
}