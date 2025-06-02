namespace LLM.Services
{
    public interface IWorkspaceService
    {
        Task<IEnumerable<Workspace>> GetAllWorkspacesAsync();
        Task<Workspace?> GetWorkspaceByIdAsync(string id);
        Task<Workspace> CreateWorkspaceAsync(CreateWorkspacePayload payload);
        Task<Workspace?> UpdateWorkspaceAsync(string id, string name);
        Task<bool> DeleteWorkspaceAsync(string id);
        Task<bool> WorkspaceExistsByNameAsync(string name, string? excludeId = null);
    }
} 