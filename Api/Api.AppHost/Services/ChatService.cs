using Microsoft.EntityFrameworkCore;
using LLM.Data;

namespace LLM.Services
{
    public class ChatService : IChatService
    {
        private readonly LLMDbContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(LLMDbContext context, ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Chat>> GetChatsByWorkspaceIdAsync(string workspaceId)
        {
            try
            {
                return await _context.Chats
                    .Include(c => c.Messages)
                    .Where(c => c.WorkspaceId == workspaceId)
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chats for workspace {WorkspaceId}", workspaceId);
                throw new InvalidOperationException($"Failed to retrieve chats for workspace {workspaceId}", ex);
            }
        }

        public async Task<Chat?> GetChatByIdAsync(string id)
        {
            try
            {
                return await _context.Chats
                    .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                    .Include(c => c.Workspace)
                    .FirstOrDefaultAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat {ChatId}", id);
                throw new InvalidOperationException($"Failed to retrieve chat {id}", ex);
            }
        }

        public async Task<Chat> CreateChatAsync(CreateChatPayload payload)
        {
            try
            {
                // Verify workspace exists
                var workspaceExists = await _context.Workspaces.AnyAsync(w => w.Id == payload.WorkspaceId);
                if (!workspaceExists)
                {
                    throw new InvalidOperationException($"Workspace with ID '{payload.WorkspaceId}' does not exist");
                }

                if (await ChatExistsByNameInWorkspaceAsync(payload.WorkspaceId, payload.Name))
                {
                    throw new InvalidOperationException($"Chat with name '{payload.Name}' already exists in this workspace");
                }

                var chat = new Chat
                {
                    Name = payload.Name.Trim(),
                    WorkspaceId = payload.WorkspaceId
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created chat {ChatId} with name '{Name}' in workspace {WorkspaceId}", 
                    chat.Id, chat.Name, chat.WorkspaceId);
                return chat;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat with name '{Name}' in workspace {WorkspaceId}", 
                    payload.Name, payload.WorkspaceId);
                throw new InvalidOperationException($"Failed to create chat '{payload.Name}'", ex);
            }
        }

        public async Task<Chat?> UpdateChatAsync(string id, string name)
        {
            try
            {
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == id);
                if (chat == null)
                {
                    return null;
                }

                var trimmedName = name.Trim();
                if (await ChatExistsByNameInWorkspaceAsync(chat.WorkspaceId, trimmedName, id))
                {
                    throw new InvalidOperationException($"Chat with name '{trimmedName}' already exists in this workspace");
                }

                chat.Name = trimmedName;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated chat {ChatId} name to '{Name}'", id, trimmedName);
                return chat;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chat {ChatId}", id);
                throw new InvalidOperationException($"Failed to update chat {id}", ex);
            }
        }

        public async Task<bool> DeleteChatAsync(string id)
        {
            try
            {
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == id);
                if (chat == null)
                {
                    return false;
                }

                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted chat {ChatId} with name '{Name}'", id, chat.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting chat {ChatId}", id);
                throw new InvalidOperationException($"Failed to delete chat {id}", ex);
            }
        }

        public async Task<bool> ChatExistsByNameInWorkspaceAsync(string workspaceId, string name, string? excludeId = null)
        {
            try
            {
                var query = _context.Chats.Where(c => c.WorkspaceId == workspaceId && c.Name == name.Trim());
                
                if (!string.IsNullOrEmpty(excludeId))
                {
                    query = query.Where(c => c.Id != excludeId);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking chat name existence for '{Name}' in workspace {WorkspaceId}", 
                    name, workspaceId);
                throw new InvalidOperationException($"Failed to check chat name existence for '{name}'", ex);
            }
        }
    }
} 