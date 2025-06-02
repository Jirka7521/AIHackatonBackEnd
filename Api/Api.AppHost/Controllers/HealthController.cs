using Microsoft.AspNetCore.Mvc;
using LLM.Models;
using LLM.Data;
using Microsoft.EntityFrameworkCore;

namespace LLM.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly LLMDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(LLMDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<HealthStatus>>> GetHealth()
        {
            var healthStatus = new HealthStatus();

            try
            {
                // Check database connectivity
                await _context.Database.CanConnectAsync();
                healthStatus.Database = "Healthy";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                healthStatus.Database = "Unhealthy";
                healthStatus.Status = "Degraded";
            }

            return Ok(ApiResponse<HealthStatus>.SuccessResponse(healthStatus));
        }

        [HttpGet("detailed")]
        public async Task<ActionResult<ApiResponse<DetailedHealthStatus>>> GetDetailedHealth()
        {
            var healthStatus = new DetailedHealthStatus();

            try
            {
                // Check database connectivity and get counts
                var canConnect = await _context.Database.CanConnectAsync();
                if (canConnect)
                {
                    healthStatus.Database = "Healthy";
                    healthStatus.WorkspaceCount = await _context.Workspaces.CountAsync();
                    healthStatus.ChatCount = await _context.Chats.CountAsync();
                    healthStatus.MessageCount = await _context.Messages.CountAsync();
                    healthStatus.AttachmentCount = await _context.Attachments.CountAsync();
                }
                else
                {
                    healthStatus.Database = "Unhealthy";
                    healthStatus.Status = "Degraded";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detailed health check failed");
                healthStatus.Database = "Unhealthy";
                healthStatus.Status = "Degraded";
            }

            return Ok(ApiResponse<DetailedHealthStatus>.SuccessResponse(healthStatus));
        }
    }

    public class HealthStatus
    {
        public string Status { get; set; } = "Healthy";
        public string Database { get; set; } = "Unknown";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DetailedHealthStatus : HealthStatus
    {
        public int WorkspaceCount { get; set; }
        public int ChatCount { get; set; }
        public int MessageCount { get; set; }
        public int AttachmentCount { get; set; }
    }
} 