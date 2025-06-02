using Microsoft.AspNetCore.Mvc;

namespace LLM.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WorkspaceController : ControllerBase
    {
        private static readonly List<Workspace> _workspaces = new();

        [HttpGet]
        public ActionResult<IEnumerable<Workspace>> GetWorkspaces()
        {
            return Ok(_workspaces);
        }

        [HttpPost]
        public ActionResult<Workspace> CreateWorkspace([FromBody] CreateWorkspacePayload payload)
        {
            if (_workspaces.Any(w => w.Name == payload.Name))
            {
                return Conflict(new WorkspaceNameAlreadyExistsError { Name = payload.Name });
            }

            var workspace = new Workspace
            {
                Id = Guid.NewGuid().ToString(),
                Name = payload.Name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _workspaces.Add(workspace);
            return Ok(workspace);
        }

        [HttpGet("{id}")]
        public ActionResult<Workspace> GetWorkspace(string id)
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
            if (workspace == null)
            {
                return NotFound();
            }

            return Ok(workspace);
        }

        [HttpPut("{id}")]
        public ActionResult<Workspace> UpdateWorkspace(string id, [FromBody] UpdateWorkspacePayload payload)
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
            if (workspace == null)
            {
                return NotFound();
            }

            if (_workspaces.Any(w => w.Name == payload.Name && w.Id != id))
            {
                return Conflict(new WorkspaceNameAlreadyExistsError { Name = payload.Name });
            }

            workspace.Name = payload.Name;
            workspace.UpdatedAt = DateTime.UtcNow;

            return Ok(workspace);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteWorkspace(string id)
        {
            var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
            if (workspace == null)
            {
                return NotFound();
            }

            _workspaces.Remove(workspace);
            return NoContent();
        }
    }

    public class UpdateWorkspacePayload
    {
        public string Name { get; set; }
    }
} 