using Microsoft.EntityFrameworkCore;
using LLM.Data;

namespace LLM.Services
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly LLMDbContext _context;
        private readonly ILogger<WorkspaceService> _logger;

        public WorkspaceService(LLMDbContext context, ILogger<WorkspaceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Workspace>> GetAllWorkspacesAsync()
        {
            try
            {
                return await _context.Workspaces
                    .Include(w => w.Chats)
                    .Include(w => w.Attachments)
                    .OrderByDescending(w => w.UpdatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all workspaces");
                throw new InvalidOperationException("Failed to retrieve workspaces", ex);
            }
        }

        public async Task<Workspace?> GetWorkspaceByIdAsync(string id)
        {
            try
            {
                return await _context.Workspaces
                    .Include(w => w.Chats)
                    .Include(w => w.Attachments)
                    .FirstOrDefaultAsync(w => w.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving workspace {WorkspaceId}", id);
                throw new InvalidOperationException($"Failed to retrieve workspace {id}", ex);
            }
        }

        public async Task<Workspace> CreateWorkspaceAsync(CreateWorkspacePayload payload)
        {
            try
            {
                if (await WorkspaceExistsByNameAsync(payload.Name))
                {
                    throw new InvalidOperationException($"Workspace with name '{payload.Name}' already exists");
                }

                var workspace = new Workspace
                {
                    Name = payload.Name.Trim()
                };

                _context.Workspaces.Add(workspace);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created workspace {WorkspaceId} with name '{Name}'", workspace.Id, workspace.Name);
                return workspace;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workspace with name '{Name}'", payload.Name);
                throw new InvalidOperationException($"Failed to create workspace '{payload.Name}'", ex);
            }
        }

        public async Task<Workspace?> UpdateWorkspaceAsync(string id, string name)
        {
            try
            {
                var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id);
                if (workspace == null)
                {
                    return null;
                }

                var trimmedName = name.Trim();
                if (await WorkspaceExistsByNameAsync(trimmedName, id))
                {
                    throw new InvalidOperationException($"Workspace with name '{trimmedName}' already exists");
                }

                workspace.Name = trimmedName;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated workspace {WorkspaceId} name to '{Name}'", id, trimmedName);
                return workspace;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating workspace {WorkspaceId}", id);
                throw new InvalidOperationException($"Failed to update workspace {id}", ex);
            }
        }

        public async Task<bool> DeleteWorkspaceAsync(string id)
        {
            try
            {
                var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id);
                if (workspace == null)
                {
                    return false;
                }

                _context.Workspaces.Remove(workspace);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted workspace {WorkspaceId} with name '{Name}'", id, workspace.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting workspace {WorkspaceId}", id);
                throw new InvalidOperationException($"Failed to delete workspace {id}", ex);
            }
        }

        public async Task<bool> WorkspaceExistsByNameAsync(string name, string? excludeId = null)
        {
            try
            {
                var query = _context.Workspaces.Where(w => w.Name == name.Trim());
                
                if (!string.IsNullOrEmpty(excludeId))
                {
                    query = query.Where(w => w.Id != excludeId);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking workspace name existence for '{Name}'", name);
                throw new InvalidOperationException($"Failed to check workspace name existence for '{name}'", ex);
            }
        }
    }
} 