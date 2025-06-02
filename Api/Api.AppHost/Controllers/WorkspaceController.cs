using Microsoft.AspNetCore.Mvc;
using LLM.Services;
using System.ComponentModel.DataAnnotations;

namespace LLM.Controllers
{
    [ApiController]
    [Route("api/workspaces")]
    public class WorkspaceController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly ILogger<WorkspaceController> _logger;

        public WorkspaceController(IWorkspaceService workspaceService, ILogger<WorkspaceController> logger)
        {
            _workspaceService = workspaceService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Workspace>>> GetWorkspaces()
        {
            try
            {
                var workspaces = await _workspaceService.GetAllWorkspacesAsync();
                return Ok(workspaces);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving workspaces");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Workspace>> CreateWorkspace([FromBody] CreateWorkspacePayload payload)
        {
            try
            {
                var workspace = await _workspaceService.CreateWorkspaceAsync(payload);
                return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                return Conflict(new WorkspaceNameAlreadyExistsError { Name = payload.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workspace with name {Name}", payload.Name);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Workspace>> GetWorkspace(string id)
        {
            try
            {
                var workspace = await _workspaceService.GetWorkspaceByIdAsync(id);
                if (workspace == null)
                {
                    return NotFound();
                }

                return Ok(workspace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving workspace {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Workspace>> UpdateWorkspace(string id, [FromBody] UpdateWorkspacePayload payload)
        {
            try
            {
                var workspace = await _workspaceService.UpdateWorkspaceAsync(id, payload.Name);
                if (workspace == null)
                {
                    return NotFound();
                }

                return Ok(workspace);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
            {
                return Conflict(new WorkspaceNameAlreadyExistsError { Name = payload.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating workspace {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkspace(string id)
        {
            try
            {
                var deleted = await _workspaceService.DeleteWorkspaceAsync(id);
                if (!deleted)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting workspace {Id}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class UpdateWorkspacePayload
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }
} 