namespace FlipShop.Application.Common;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data = default, IReadOnlyList<string>? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string message = "Success") => new(true, message, data);
    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) => new(false, message, default, errors);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
