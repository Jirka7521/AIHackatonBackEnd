using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
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
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string FileName { get; set; }

    [VectorStoreData]
    public string Content { get; set; }

    [VectorStoreVector(Dimensions: 384, DistanceFunction = DistanceFunction.CosineSimilarity)]
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
/// Provides public endpoints to interact with the vector store for PDF records, including initialization, 
/// vectorization of uploaded PDF files, and querying for relevant vectors. It now also persists data to a PostgreSQL database.
/// </summary>
public class VectorEndpoints
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly VectorStoreCollection<int, PdfRecord> _pdfStore;
    private readonly PostgresDatabase _database;
    private int _nextId;
    private readonly float _minSimilarityThreshold;

    private VectorEndpoints(IEmbeddingGenerator<string, Embedding<float>> generator, VectorStoreCollection<int, PdfRecord> pdfStore, float minSimilarityThreshold, PostgresDatabase database)
    {
        _generator = generator;
        _pdfStore = pdfStore;
        _nextId = 0;
        _minSimilarityThreshold = minSimilarityThreshold;
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

        InMemoryVectorStore vectorStore = new InMemoryVectorStore();
        VectorStoreCollection<int, PdfRecord> pdfStore =
            vectorStore.GetCollection<int, PdfRecord>("pdfRecords");
        await pdfStore.EnsureCollectionExistsAsync();

        float minThreshold = 0.7f;
        string thresholdSetting = configuration["MinSimilarityThreshold"];
        if (!string.IsNullOrEmpty(thresholdSetting) &&
            float.TryParse(thresholdSetting, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedThreshold))
        {
            minThreshold = parsedThreshold;
        }

        string postgresConnectionString = configuration["Postgres:ConnectionString"];
        var database = new PostgresDatabase(postgresConnectionString);
        await database.EnsureTableExistsAsync();

        return new VectorEndpoints(generator, pdfStore, minThreshold, database);
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

            await _pdfStore.UpsertAsync(pdfRecord);
            await _database.InsertPdfRecordAsync(pdfRecord);
            chunkId++;
        }

        _nextId = chunkId;
        return new OperationResult
        {
            Status = OperationStatus.Success,
            Message = "File uploaded, vectorized, and persisted successfully."
        };
    }

    public async Task<OperationResult> GetHealthAsync()
    {
        try
        {
            await _pdfStore.EnsureCollectionExistsAsync();
            return new OperationResult
            {
                Status = OperationStatus.Success,
                Message = "The vector store is accessible."
            };
        }
        catch (Exception ex)
        {
            return new OperationResult
            {
                Status = OperationStatus.Error,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<List<VectorSearchResult<PdfRecord>>> GetRelevantVectorsAsync(string prompt, int count)
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
        List<VectorSearchResult<PdfRecord>> results = new List<VectorSearchResult<PdfRecord>>();
        await foreach (var result in _pdfStore.SearchAsync(promptVector, top: count))
        {
            results.Add(result);
        }
        results = results.Where(r => r.Score >= _minSimilarityThreshold).ToList();
        return results;
    }
}