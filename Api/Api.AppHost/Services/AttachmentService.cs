using Microsoft.EntityFrameworkCore;
using LLM.Data;

namespace LLM.Services
{
    public class AttachmentService : IAttachmentService
    {
        private readonly LLMDbContext _context;
        private readonly ILogger<AttachmentService> _logger;

        public AttachmentService(LLMDbContext context, ILogger<AttachmentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Attachment>> GetAttachmentsByWorkspaceIdAsync(string workspaceId)
        {
            try
            {
                return await _context.Attachments
                    .Include(a => a.Workspace)
                    .Where(a => a.WorkspaceId == workspaceId)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attachments for workspace {WorkspaceId}", workspaceId);
                throw new InvalidOperationException($"Failed to retrieve attachments for workspace {workspaceId}", ex);
            }
        }

        public async Task<Attachment?> GetAttachmentByIdAsync(string id)
        {
            try
            {
                return await _context.Attachments
                    .Include(a => a.Workspace)
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attachment {AttachmentId}", id);
                throw new InvalidOperationException($"Failed to retrieve attachment {id}", ex);
            }
        }

        public async Task<Attachment> CreateAttachmentAsync(CreateAttachmentPayload payload)
        {
            try
            {
                // Verify workspace exists
                var workspaceExists = await _context.Workspaces.AnyAsync(w => w.Id == payload.WorkspaceId);
                if (!workspaceExists)
                {
                    throw new InvalidOperationException($"Workspace with ID '{payload.WorkspaceId}' does not exist");
                }

                if (await AttachmentExistsByNameInWorkspaceAsync(payload.WorkspaceId, payload.Name))
                {
                    throw new InvalidOperationException($"Attachment with name '{payload.Name}' already exists in this workspace");
                }

                var attachment = new Attachment
                {
                    Name = payload.Name.Trim(),
                    Type = payload.Type,
                    Status = AttachmentStatus.Uploaded,
                    WorkspaceId = payload.WorkspaceId
                };

                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created attachment {AttachmentId} with name '{Name}' in workspace {WorkspaceId}", 
                    attachment.Id, attachment.Name, attachment.WorkspaceId);
                return attachment;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attachment with name '{Name}' in workspace {WorkspaceId}", 
                    payload.Name, payload.WorkspaceId);
                throw new InvalidOperationException($"Failed to create attachment '{payload.Name}'", ex);
            }
        }

        public async Task<Attachment?> UpdateAttachmentStatusAsync(string id, AttachmentStatus status)
        {
            try
            {
                var attachment = await _context.Attachments.FirstOrDefaultAsync(a => a.Id == id);
                if (attachment == null)
                {
                    return null;
                }

                attachment.Status = status;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated attachment {AttachmentId} status to {Status}", id, status);
                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attachment {AttachmentId} status", id);
                throw new InvalidOperationException($"Failed to update attachment {id} status", ex);
            }
        }

        public async Task<Attachment?> UpdateAttachmentPreviewUrlAsync(string id, Uri? previewUrl)
        {
            try
            {
                var attachment = await _context.Attachments.FirstOrDefaultAsync(a => a.Id == id);
                if (attachment == null)
                {
                    return null;
                }

                attachment.PreviewUrl = previewUrl;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated attachment {AttachmentId} preview URL", id);
                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attachment {AttachmentId} preview URL", id);
                throw new InvalidOperationException($"Failed to update attachment {id} preview URL", ex);
            }
        }

        public async Task<bool> DeleteAttachmentAsync(string id)
        {
            try
            {
                var attachment = await _context.Attachments.FirstOrDefaultAsync(a => a.Id == id);
                if (attachment == null)
                {
                    return false;
                }

                _context.Attachments.Remove(attachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted attachment {AttachmentId} with name '{Name}'", id, attachment.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attachment {AttachmentId}", id);
                throw new InvalidOperationException($"Failed to delete attachment {id}", ex);
            }
        }

        public async Task<bool> AttachmentExistsByNameInWorkspaceAsync(string workspaceId, string name, string? excludeId = null)
        {
            try
            {
                var query = _context.Attachments.Where(a => a.WorkspaceId == workspaceId && a.Name == name.Trim());
                
                if (!string.IsNullOrEmpty(excludeId))
                {
                    query = query.Where(a => a.Id != excludeId);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking attachment name existence for '{Name}' in workspace {WorkspaceId}", 
                    name, workspaceId);
                throw new InvalidOperationException($"Failed to check attachment name existence for '{name}'", ex);
            }
        }
    }
} 