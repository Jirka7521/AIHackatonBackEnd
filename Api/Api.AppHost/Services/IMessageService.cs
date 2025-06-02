namespace LLM.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<Message>> GetMessagesByChatIdAsync(string chatId);
        Task<Message?> GetMessageByIdAsync(string id);
        Task<Message> CreateMessageAsync(CreateMessagePayload payload);
        Task<Message?> UpdateMessageAsync(string id, string content);
        Task<bool> DeleteMessageAsync(string id);
    }
} 