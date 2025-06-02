namespace LLM
{
    public enum MessageRole
    {
        User,
        Assistant
    }

    public class Message
    {
        public string Id { get; set; };
        public string Content { get; set; };
        public MessageRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateMessagePayload
    {
        public string? Id { get; set; }
        public string Content { get; set; };
        public MessageRole Role { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
