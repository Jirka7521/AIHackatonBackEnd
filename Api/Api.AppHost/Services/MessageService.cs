using Microsoft.EntityFrameworkCore;
using LLM.Data;

namespace LLM.Services
{
    public class MessageService : IMessageService
    {
        private readonly LLMDbContext _context;
        private readonly ILogger<MessageService> _logger;

        public MessageService(LLMDbContext context, ILogger<MessageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Message>> GetMessagesByChatIdAsync(string chatId)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.Chat)
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving messages for chat {ChatId}", chatId);
                throw new InvalidOperationException($"Failed to retrieve messages for chat {chatId}", ex);
            }
        }

        public async Task<Message?> GetMessageByIdAsync(string id)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.Chat)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving message {MessageId}", id);
                throw new InvalidOperationException($"Failed to retrieve message {id}", ex);
            }
        }

        public async Task<Message> CreateMessageAsync(CreateMessagePayload payload)
        {
            try
            {
                // Verify chat exists
                var chatExists = await _context.Chats.AnyAsync(c => c.Id == payload.ChatId);
                if (!chatExists)
                {
                    throw new InvalidOperationException($"Chat with ID '{payload.ChatId}' does not exist");
                }

                var message = new Message
                {
                    Id = !string.IsNullOrEmpty(payload.Id) ? payload.Id : Guid.NewGuid().ToString(),
                    Content = payload.Content.Trim(),
                    Role = payload.Role,
                    ChatId = payload.ChatId,
                    CreatedAt = payload.CreatedAt ?? DateTime.UtcNow
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                // Update chat's UpdatedAt timestamp
                var chat = await _context.Chats.FirstOrDefaultAsync(c => c.Id == payload.ChatId);
                if (chat != null)
                {
                    chat.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Created message {MessageId} in chat {ChatId} with role {Role}", 
                    message.Id, message.ChatId, message.Role);
                return message;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message in chat {ChatId}", payload.ChatId);
                throw new InvalidOperationException($"Failed to create message in chat {payload.ChatId}", ex);
            }
        }

        public async Task<Message?> UpdateMessageAsync(string id, string content)
        {
            try
            {
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
                if (message == null)
                {
                    return null;
                }

                message.Content = content.Trim();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated message {MessageId} content", id);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message {MessageId}", id);
                throw new InvalidOperationException($"Failed to update message {id}", ex);
            }
        }

        public async Task<bool> DeleteMessageAsync(string id)
        {
            try
            {
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
                if (message == null)
                {
                    return false;
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted message {MessageId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId}", id);
                throw new InvalidOperationException($"Failed to delete message {id}", ex);
            }
        }
    }
} 