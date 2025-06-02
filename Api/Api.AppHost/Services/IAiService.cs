using LLM.Models;

namespace LLM.Services;

public interface IAiService
{
    Task<OperationResult> UploadFileAsync(string filePath);
    Task<List<QueryResult>> QueryVectorsAsync(string query, int count);
    Task<string> GetFilePathAsync(int id);
    Task<AiChatResponse> ChatAsync(AiChatRequest request);
} 