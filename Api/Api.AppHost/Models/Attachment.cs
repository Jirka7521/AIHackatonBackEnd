using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLM
{
    public enum AttachmentType
    {
        Pdf,
        Image,
        Text
    }

    public enum AttachmentStatus
    {
        Uploaded,
        Processing,
        Ready
    }

    public class Attachment
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        public Uri? PreviewUrl { get; set; }
        
        [Required]
        public AttachmentType Type { get; set; }
        
        [Required]
        public AttachmentStatus Status { get; set; } = AttachmentStatus.Uploaded;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        [Required]
        public string WorkspaceId { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("WorkspaceId")]
        public virtual Workspace Workspace { get; set; } = null!;
    }

    public class CreateAttachmentPayload
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public AttachmentType Type { get; set; }
        
        [Required]
        public string WorkspaceId { get; set; } = string.Empty;
    }

    public class AttachmentNameAlreadyExistsError
    {
        public string Name { get; set; } = string.Empty;
        public string Message => $"Attachment '{Name}' already exists";
    }
}