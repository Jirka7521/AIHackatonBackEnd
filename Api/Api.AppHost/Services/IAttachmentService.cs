namespace LLM.Services
{
    public interface IAttachmentService
    {
        Task<IEnumerable<Attachment>> GetAttachmentsByWorkspaceIdAsync(string workspaceId);
        Task<Attachment?> GetAttachmentByIdAsync(string id);
        Task<Attachment> CreateAttachmentAsync(CreateAttachmentPayload payload);
        Task<Attachment?> UpdateAttachmentStatusAsync(string id, AttachmentStatus status);
        Task<Attachment?> UpdateAttachmentPreviewUrlAsync(string id, Uri? previewUrl);
        Task<bool> DeleteAttachmentAsync(string id);
        Task<bool> AttachmentExistsByNameInWorkspaceAsync(string workspaceId, string name, string? excludeId = null);
    }
} 