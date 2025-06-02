namespace LLM.Models;

public class VectorRecord
{
    public int Id { get; set; }
    public int SourceFileId { get; set; }
    public float[] VectorData { get; set; } = Array.Empty<float>();
    public string Snippet { get; set; } = string.Empty;
    public SourceFile SourceFile { get; set; } = null!;
} 