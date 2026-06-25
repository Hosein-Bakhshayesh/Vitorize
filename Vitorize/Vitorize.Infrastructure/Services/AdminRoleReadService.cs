using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Roles;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class AdminRoleReadService : IAdminRoleReadService
    {
        private readonly VitorizeDbContext _dbContext;
        public AdminRoleReadService(VitorizeDbContext dbContext) => _dbContext = dbContext;
        public async Task<List<AdminRoleDto>> GetAllAsync()
        {
            return await _dbContext.Roles.AsNoTracking().OrderBy(x => x.Name).Select(x => new AdminRoleDto
            {
                Id = x.Id, Name = x.Name, DisplayName = x.DisplayName, CreatedAt = x.CreatedAt, UsersCount = x.Users.Count()
            }).ToListAsync();
        }
        public async Task<AdminRoleDto> GetByIdAsync(Guid id)
        {
            var item = await _dbContext.Roles.AsNoTracking().Where(x => x.Id == id).Select(x => new AdminRoleDto
            {
                Id = x.Id, Name = x.Name, DisplayName = x.DisplayName, CreatedAt = x.CreatedAt, UsersCount = x.Users.Count()
            }).FirstOrDefaultAsync();
            return item ?? throw new KeyNotFoundException("نقش پیدا نشد.");
        }
    }
}
