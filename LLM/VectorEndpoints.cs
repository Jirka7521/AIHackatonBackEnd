using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public class PdfRecord
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public string Content { get; set; }
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// Represents the status of an operation.
/// </summary>
public enum OperationStatus
{
    Success,
    Error
}

/// <summary>
/// Represents the result of an operation including its status and a corresponding message.
/// </summary>
public class OperationResult
{
    public OperationStatus Status { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// Represents a search result from the database vector search.
/// </summary>
public class DatabaseVectorSearchResult
{
    public int Id { get; set; }
    public string FileName { get; set; }
    public double Distance { get; set; }
}

/// <summary>
/// Provides public endpoints to interact with the vector store for PDF records, including initialization, 
/// vectorization of uploaded PDF files, and querying for relevant vectors using PostgreSQL database.
/// </summary>
public class VectorEndpoints
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly PostgresDatabase _database;
    private int _nextId;

    private VectorEndpoints(IEmbeddingGenerator<string, Embedding<float>> generator, PostgresDatabase database)
    {
        _generator = generator;
        _nextId = 0;
        _database = database;
    }

    public static async Task<VectorEndpoints> CreateAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string vectorEndpoint = configuration["AzureOpenAIVector:Endpoint"];
        string vectorModel = configuration["AzureOpenAIVector:Model"];
        string vectorApiKey = configuration["AzureOpenAIVector:ApiKey"];

        IEmbeddingGenerator<string, Embedding<float>> generator =
            new AzureOpenAIClient(new Uri(vectorEndpoint), new AzureKeyCredential(vectorApiKey))
                .GetEmbeddingClient(deploymentName: vectorModel)
                .AsIEmbeddingGenerator();

        string postgresConnectionString = configuration["Postgres:ConnectionString"];
        var database = new PostgresDatabase(postgresConnectionString);
        await database.EnsureTableExistsAsync();

        return new VectorEndpoints(generator, database);
    }

    public async Task<OperationResult> UploadPdfAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new ArgumentException("The file path is invalid or the file does not exist.", nameof(filePath));
        }

        StringBuilder contentBuilder = new StringBuilder();
        using (PdfDocument pdf = PdfDocument.Open(filePath))
        {
            foreach (Page page in pdf.GetPages())
            {
                contentBuilder.AppendLine(page.Text);
            }
        }
        string pdfContent = contentBuilder.ToString().Normalize(NormalizationForm.FormC);

        const int chunkSize = 2000;
        List<string> chunks = new List<string>();
        for (int i = 0; i < pdfContent.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, pdfContent.Length - i);
            chunks.Add(pdfContent.Substring(i, length));
        }

        int chunkId = _nextId;
        foreach (string chunk in chunks)
        {
            ReadOnlyMemory<float> vector = await _generator.GenerateVectorAsync(chunk);
            PdfRecord pdfRecord = new PdfRecord
            {
                Id = chunkId,
                FileName = filePath,
                Content = chunk,
                Vector = vector
            };

            await _database.InsertPdfRecordAsync(pdfRecord);
            chunkId++;
        }

        _nextId = chunkId;
        return new OperationResult
        {
            Status = OperationStatus.Success,
            Message = "File uploaded, vectorized, and persisted to database successfully."
        };
    }

    public async Task<OperationResult> GetHealthAsync()
    {
        try
        {
            // Check database connection by attempting a simple operation
            await _database.EnsureTableExistsAsync();
            return new OperationResult
            {
                Status = OperationStatus.Success,
                Message = "The database is accessible."
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Status = OperationStatus.Error,
                Message = $"Database error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Searches for the most similar vectors stored in the PostgreSQL database using the pgvector extension.
    /// </summary>
    /// <param name="prompt">The prompt to generate the query vector from.</param>
    /// <param name="count">The number of matching records to request.</param>
    /// <returns>A list of database search results including record Id, FileName, and computed distance.</returns>
    public async Task<List<DatabaseVectorSearchResult>> GetRelevantVectorsAsync(string prompt, int count)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        ReadOnlyMemory<float> promptVector = await _generator.GenerateVectorAsync(prompt);
        var dbResults = await _database.SearchSimilarVectorsAsync(promptVector, count);
        return dbResults.Select(r => new DatabaseVectorSearchResult
        {
            Id = r.Id,
            FileName = r.FilePath,
            Distance = 1 - r.Similarity // Converting similarity to distance
        }).ToList();
    }

    public async Task<string> GetVectorTextAsync(int id)
    {
        var record = await _database.GetPdfRecordByIdAsync(id);
        if (record is null)
        {
            throw new ArgumentException($"No vector found with id {id}", nameof(id));
        }
        return record.Content;
    }
}