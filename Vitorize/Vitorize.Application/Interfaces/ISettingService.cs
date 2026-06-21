using Vitorize.Application.DTOs.Settings;

namespace Vitorize.Application.Interfaces
{
    public interface ISettingService
    {
        Task<List<SettingGroupDto>> GetAllGroupedAsync();

        Task<List<SettingDto>> GetPublicSettingsAsync();

        Task<SettingDto?> GetByKeyAsync(string key);

        Task<string?> GetValueAsync(string key);

        Task<T?> GetValueAsync<T>(string key);

        Task<SettingDto> UpsertAsync(UpdateSettingDto request);

        Task DeleteAsync(string key);
    }
}