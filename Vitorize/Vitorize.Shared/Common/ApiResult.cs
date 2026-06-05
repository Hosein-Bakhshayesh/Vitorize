namespace Vitorize.Shared.Common
{
    public class ApiResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();

        public static ApiResult Success(string message = "عملیات با موفقیت انجام شد.")
        {
            return new ApiResult
            {
                IsSuccess = true,
                Message = message
            };
        }

        public static ApiResult Failure(string message, List<string>? errors = null)
        {
            return new ApiResult
            {
                IsSuccess = false,
                Message = message,
                Errors = errors ?? new()
            };
        }
    }

    public class ApiResult<T> : ApiResult
    {
        public T? Data { get; set; }

        public static ApiResult<T> Success(
            T data,
            string message = "عملیات با موفقیت انجام شد.")
        {
            return new ApiResult<T>
            {
                IsSuccess = true,
                Message = message,
                Data = data
            };
        }

        public new static ApiResult<T> Failure(
            string message,
            List<string>? errors = null)
        {
            return new ApiResult<T>
            {
                IsSuccess = false,
                Message = message,
                Errors = errors ?? new(),
                Data = default
            };
        }
    }
}