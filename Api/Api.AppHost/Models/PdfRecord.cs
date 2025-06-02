namespace LLM.Models;

public class PdfRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ReadOnlyMemory<float> Vector { get; set; }
}

public enum OperationStatus
{
    Success,
    Error
}

public class OperationResult
{
    public OperationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DatabaseVectorSearchResult
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double Distance { get; set; }
}

public class QueryResult
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatHistoryItem> History { get; set; } = new();
}

public class ChatHistoryItem
{
    public string Role { get; set; } = string.Empty; // "user", "assistant", "system"
    public string Content { get; set; } = string.Empty;
}

public class AiChatResponse
{
    public string Response { get; set; } = string.Empty;
    public List<QueryResult> RetrievedContext { get; set; } = new();
} 