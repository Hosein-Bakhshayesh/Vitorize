namespace Vitorize.Shared.Common
{
    public class ApiResult
    {
        public bool IsSuccess { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Set by the web ApiClient when the failure was an authentication boundary rather than a
        /// business rule. Callers previously had to match Persian words inside Message to tell the
        /// difference, which mistook any message containing those words for a session problem.
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Machine-readable outcome, when the endpoint defines one (see
        /// <c>AuthOutcomeCodes</c>). Callers branch on this instead of matching message text.
        /// </summary>
        public string? ErrorCode { get; set; }

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