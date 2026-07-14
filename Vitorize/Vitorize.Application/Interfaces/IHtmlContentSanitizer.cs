namespace Vitorize.Application.Interfaces;

public interface IHtmlContentSanitizer
{
    string? Sanitize(string? html);
}
