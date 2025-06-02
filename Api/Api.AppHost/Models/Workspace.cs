using System.ComponentModel.DataAnnotations;

namespace LLM
{
    public class Workspace
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }

    public class CreateWorkspacePayload
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }

    public class WorkspaceNameAlreadyExistsError
    {
        public string Name { get; set; } = string.Empty;
        public string Message => $"Workspace '{Name}' already exists";
    }
}