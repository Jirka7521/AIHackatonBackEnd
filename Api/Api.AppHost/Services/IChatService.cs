namespace LLM.Services
{
    public interface IChatService
    {
        Task<IEnumerable<Chat>> GetChatsByWorkspaceIdAsync(string workspaceId);
        Task<Chat?> GetChatByIdAsync(string id);
        Task<Chat> CreateChatAsync(CreateChatPayload payload);
        Task<Chat?> UpdateChatAsync(string id, string name);
        Task<bool> DeleteChatAsync(string id);
        Task<bool> ChatExistsByNameInWorkspaceAsync(string workspaceId, string name, string? excludeId = null);
    }
} 