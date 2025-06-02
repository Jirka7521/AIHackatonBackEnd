using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLM
{
    public class Chat
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        [Required]
        public string WorkspaceId { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("WorkspaceId")]
        public virtual Workspace Workspace { get; set; } = null!;
        
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }

    public class CreateChatPayload
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string WorkspaceId { get; set; } = string.Empty;
    }

    public class ChatNameAlreadyExistsError
    {
        public string Name { get; set; } = string.Empty;
        public string Message => $"Chat '{Name}' already exists";
    }
}