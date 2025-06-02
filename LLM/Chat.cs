namespace LLM
{
    public class Chat
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateChatPayload
    {
        public string Name { get; set; } = default!;
    }

    public class ChatNameAlreadyExistsError
    {
        public string Name { get; set; } = default!;
        public string Message => $"Chat '{Name}' already exists";
    }

}
