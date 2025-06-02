namespace LLM.Models;

public class SourceFile
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public List<VectorRecord> Vectors { get; set; } = new();
} 