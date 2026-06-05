using Microsoft.AspNetCore.Mvc;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly VitorizeDbContext _dbContext;

        public HealthController(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("db")]
        public async Task<IActionResult> CheckDatabase()
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();

            return Ok(new
            {
                Database = "VitorizeDb",
                CanConnect = canConnect
            });
        }
    }
}