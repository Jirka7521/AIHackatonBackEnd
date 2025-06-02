using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLM
{
    public enum MessageRole
    {
        User,
        Assistant
    }

    public class Message
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        public MessageRole Role { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        [Required]
        public string ChatId { get; set; } = string.Empty;

        // Navigation property
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;
    }

    public class CreateMessagePayload
    {
        public string? Id { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        public MessageRole Role { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        
        [Required]
        public string ChatId { get; set; } = string.Empty;
    }
}