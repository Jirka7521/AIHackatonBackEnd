using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using LLM.Data;
using LLM.Models;

namespace LLM.Services;

public class AiService : IAiService
{
    private readonly LLMDbContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatClient _chatClient;
    private readonly ILogger<AiService> _logger;

    public AiService(
        LLMDbContext context,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IChatClient chatClient,
        ILogger<AiService> logger)
    {
        _context = context;
        _embeddingGenerator = embeddingGenerator;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<OperationResult> UploadFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new OperationResult
                {
                    Status = OperationStatus.Error,
                    Message = "The file path is invalid or the file does not exist."
                };
            }

            // Read PDF content
            StringBuilder contentBuilder = new StringBuilder();
            using (PdfDocument pdf = PdfDocument.Open(filePath))
            {
                foreach (Page page in pdf.GetPages())
                {
                    contentBuilder.AppendLine(page.Text);
                }
            }
            string pdfContent = contentBuilder.ToString().Normalize(NormalizationForm.FormC);

            // Get or create source file
            var sourceFile = await _context.SourceFiles
                .FirstOrDefaultAsync(sf => sf.FilePath == filePath);

            if (sourceFile == null)
            {
                sourceFile = new SourceFile { FilePath = filePath };
                _context.SourceFiles.Add(sourceFile);
                await _context.SaveChangesAsync();
            }

            // Split content into chunks
            const int chunkSize = 2000;
            List<string> chunks = new List<string>();
            for (int i = 0; i < pdfContent.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, pdfContent.Length - i);
                chunks.Add(pdfContent.Substring(i, length));
            }

            // Process each chunk
            foreach (string chunk in chunks)
            {
                // Check if this chunk already exists for this file
                var existingVector = await _context.VectorRecords
                    .FirstOrDefaultAsync(v => v.SourceFileId == sourceFile.Id && v.Snippet == chunk);

                if (existingVector == null)
                {
                    // Generate vector for current chunk
                    ReadOnlyMemory<float> vector = await _embeddingGenerator.GenerateVectorAsync(chunk);

                    var vectorRecord = new VectorRecord
                    {
                        SourceFileId = sourceFile.Id,
                        Snippet = chunk,
                        VectorData = vector.ToArray()
                    };

                    _context.VectorRecords.Add(vectorRecord);
                }
            }

            await _context.SaveChangesAsync();

            return new OperationResult
            {
                Status = OperationStatus.Success,
                Message = "File uploaded, vectorized, and persisted to database successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FilePath}", filePath);
            return new OperationResult
            {
                Status = OperationStatus.Error,
                Message = $"Error uploading file: {ex.Message}"
            };
        }
    }

    public async Task<List<QueryResult>> QueryVectorsAsync(string query, int count)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        }
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        try
        {
            // Generate vector for the query
            ReadOnlyMemory<float> queryVector = await _embeddingGenerator.GenerateVectorAsync(query);

            // Use raw SQL for vector similarity search since EF doesn't support pgvector operations directly
            var results = await _context.VectorRecords
                .FromSqlRaw(@"
                    SELECT v.""Id"", v.""SourceFileId"", v.""VectorData"", v.""Snippet""
                    FROM public.vectors v
                    ORDER BY public.cosine_similarity(v.vector_data, @p0) DESC
                    LIMIT @p1", 
                    queryVector.ToArray(), count)
                .Include(v => v.SourceFile)
                .ToListAsync();

            return results.Select(r => new QueryResult
            {
                Id = r.Id,
                Text = r.Snippet
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying vectors for: {Query}", query);
            return new List<QueryResult>();
        }
    }

    public async Task<string> GetFilePathAsync(int id)
    {
        var vectorRecord = await _context.VectorRecords
            .Include(v => v.SourceFile)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vectorRecord == null)
        {
            throw new ArgumentException($"No vector found with id {id}", nameof(id));
        }

        return vectorRecord.SourceFile.FilePath;
    }

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request)
    {
        try
        {
            // Build chat history
            var chatHistory = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, @"
                You are a friendly learning assistant dedicated to helping users understand complex topics and acquire new skills.
                When interacting with users, you should:
                
                1. Ask clarifying questions to better understand the user's learning goals.
                2. Provide clear, step-by-step explanations and practical examples.
                3. Reference additional resources or data (e.g., information from related databases or processing of vector data) when helpful.
                4. Prefer to use provided context and data to enhance your responses.
                5. Conclude your responses by asking if there is anything else you can explain or help with.
                ")
            };

            // Add conversation history
            foreach (var historyItem in request.History)
            {
                var role = historyItem.Role.ToLower() switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(role, historyItem.Content));
            }

            // Add current user message
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, request.Message));

            // Perform RAG: Query the vector store
            var queryResults = await QueryVectorsAsync(request.Message, 3);
            string retrievedInfo = "Additional context retrieved from vector store:\n";
            foreach (var result in queryResults)
            {
                retrievedInfo += $"ID: {result.Id}, Content Snippet: {result.Text}\n";
            }

            // Add retrieved info as system context
            if (queryResults.Any())
            {
                chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, retrievedInfo));
            }

            // Get AI response
            var response = await _chatClient.CompleteAsync(chatHistory);

            return new AiChatResponse
            {
                Response = response.Message.Text ?? string.Empty,
                RetrievedContext = queryResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI chat for message: {Message}", request.Message);
            return new AiChatResponse
            {
                Response = "I apologize, but I encountered an error processing your request. Please try again.",
                RetrievedContext = new List<QueryResult>()
            };
        }
    }
} 