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
/// Provides public endpoints to interact with the vector store for PDF records, including initialization, 
/// vectorization of uploaded PDF files, and querying for relevant vectors.
/// </summary>
public class VectorEndpoints
{
    /// <summary>
    /// The embedding generator used to convert text prompts into vectors.
    /// </summary>
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    /// <summary>
    /// The in-memory vector store collection that holds PdfRecord entries.
    /// </summary>
    private readonly VectorStoreCollection<int, PdfRecord> _pdfStore;

    /// <summary>
    /// Internal counter to generate unique IDs for new PDF records.
    /// </summary>
    private int _nextId;

    /// <summary>
    /// Private constructor to enforce asynchronous initialization via <see cref="CreateAsync"/>.
    /// </summary>
    /// <param name="generator">The embedding generator for converting text into vectors.</param>
    /// <param name="pdfStore">The vector store collection containing PDF records.</param>
    private VectorEndpoints(IEmbeddingGenerator<string, Embedding<float>> generator, VectorStoreCollection<int, PdfRecord> pdfStore)
    {
        _generator = generator;
        _pdfStore = pdfStore;
        _nextId = 0;
    }

    /// <summary>
    /// Asynchronously creates an instance of <see cref="VectorEndpoints"/> by loading configuration,
    /// initializing the connection to the Azure OpenAI service, and creating the vector store.
    /// PDF ingestion is not performed here.
    /// </summary>
    /// <returns>An initialized instance of <see cref="VectorEndpoints"/>.</returns>
    public static async Task<VectorEndpoints> CreateAsync()
    {
        // Load configuration from appsettings.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Retrieve connection details and API key for vector creation.
        string vectorEndpoint = configuration["AzureOpenAIVector:Endpoint"];
        string vectorModel = configuration["AzureOpenAIVector:Model"];
        string vectorApiKey = configuration["AzureOpenAIVector:ApiKey"];

        // Create the embedding generator using AzureKeyCredential.
        IEmbeddingGenerator<string, Embedding<float>> generator =
            new AzureOpenAIClient(new Uri(vectorEndpoint), new AzureKeyCredential(vectorApiKey))
                .GetEmbeddingClient(deploymentName: vectorModel)
                .AsIEmbeddingGenerator();

        // Create the in-memory vector store and get the PDF records collection.
        InMemoryVectorStore vectorStore = new InMemoryVectorStore();
        VectorStoreCollection<int, PdfRecord> pdfStore =
            vectorStore.GetCollection<int, PdfRecord>("pdfRecords");
        await pdfStore.EnsureCollectionExistsAsync();

        // Return the initialized instance.
        return new VectorEndpoints(generator, pdfStore);
    }

    /// <summary>
    /// Uploads a PDF file specified by <paramref name="filePath"/>, vectorizes its content, splits the data into chunks,
    /// and stores the resulting vectors in the vector store.
    /// </summary>
    /// <param name="filePath">The full path of the PDF file to upload and vectorize.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a success message upon successful upload,
    /// or an exception is thrown if the file cannot be processed.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the file path is null, empty, or the file does not exist.</exception>
    public async Task<string> UploadPdfAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new ArgumentException("The file path is invalid or the file does not exist.", nameof(filePath));
        }

        StringBuilder contentBuilder = new StringBuilder();

        // Use PdfPig to extract text from the PDF.
        using (PdfDocument pdf = PdfDocument.Open(filePath))
        {
            foreach (Page page in pdf.GetPages())
            {
                contentBuilder.AppendLine(page.Text);
            }
        }
        // Normalize the extracted content to ensure proper Unicode (e.g., Czech letters) is preserved.
        string pdfContent = contentBuilder.ToString().Normalize(NormalizationForm.FormC);

        // Split the PDF content into chunks (using a chunk size of 8000 characters).
        const int chunkSize = 8000;
        List<string> chunks = new List<string>();
        for (int i = 0; i < pdfContent.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, pdfContent.Length - i);
            chunks.Add(pdfContent.Substring(i, length));
        }

        // Generate embeddings for each chunk and populate the vector store.
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
            chunkId++;
        }

        _nextId = chunkId;
        return "File uploaded and vectorized successfully.";
    }

    /// <summary>
    /// Checks the health of the vector store by ensuring the PDF records collection exists.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a success message
    /// if the collection is accessible, or an error message if it is not.
    /// </returns>
    public async Task<string> GetHealthAsync()
    {
        try
        {
            // Ensure the PDF records collection exists in the vector store.
            await _pdfStore.EnsureCollectionExistsAsync();
            return "Success: The vector store is accessible.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Retrieves the top relevant PDF record vectors based on the specified prompt.
    /// </summary>
    /// <param name="prompt">The text prompt or topic used to generate a search vector.</param>
    /// <param name="count">The number of relevant vectors to retrieve.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a list of vector search results that include PDF records.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the prompt is null or whitespace, or when count is less than or equal to zero.
    /// </exception>
    public async Task<List<VectorSearchResult<PdfRecord>>> GetRelevantVectorsAsync(string prompt, int count)
    {
        // Validate input parameters.
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        // Generate a vector representation for the provided prompt.
        ReadOnlyMemory<float> promptVector = await _generator.GenerateVectorAsync(prompt);

        // Retrieve and collect the top 'count' vector search results matching the generated prompt vector.
        List<VectorSearchResult<PdfRecord>> results = new List<VectorSearchResult<PdfRecord>>();
        await foreach (var result in _pdfStore.SearchAsync(promptVector, top: count))
        {
            results.Add(result);
        }
        return results;
    }
}