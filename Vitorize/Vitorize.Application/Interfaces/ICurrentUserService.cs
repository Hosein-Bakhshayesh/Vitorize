namespace Vitorize.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }

        string? Mobile { get; }

        string? FullName { get; }

        bool IsAuthenticated { get; }

        string? IpAddress { get; }

        string? UserAgent { get; }
    }
}