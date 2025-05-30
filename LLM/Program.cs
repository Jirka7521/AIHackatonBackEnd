using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

// Renamed from PdfDocument to PdfRecord to avoid conflict with PdfPig's PdfDocument.
internal class PdfRecord
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

internal class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Retrieve connection details and API key from configuration.
        string endpoint = configuration["AzureOpenAI:Endpoint"];
        string model = configuration["AzureOpenAI:Model"];
        string apiKey = configuration["AzureOpenAI:ApiKey"];

        // Create the embedding generator using AzureKeyCredential.
        IEmbeddingGenerator<string, Embedding<float>> generator =
            new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
                .GetEmbeddingClient(deploymentName: model)
                .AsIEmbeddingGenerator();

        // Path to the PDF file.
        string pdfFilePath = "C:\\Users\\jirim\\OneDrive - České vysoké učení technické v Praze\\School\\University\\1. semestr\\Kurs of electronics\\1Day leacture.pdf";
        StringBuilder contentBuilder = new StringBuilder();

        // Use PdfPig to extract text from the PDF.
        using (PdfDocument pdf = PdfDocument.Open(pdfFilePath))
        {
            foreach (Page page in pdf.GetPages())
            {
                contentBuilder.AppendLine(page.Text);
            }
        }
        string pdfContent = contentBuilder.ToString();

        // Split the PDF content into chunks of 8000 characters.
        const int chunkSize = 8000;
        List<string> chunks = new List<string>();
        for (int i = 0; i < pdfContent.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, pdfContent.Length - i);
            chunks.Add(pdfContent.Substring(i, length));
        }

        // Create and populate the in-memory vector store for PDFs.
        InMemoryVectorStore vectorStore = new InMemoryVectorStore();
        VectorStoreCollection<int, PdfRecord> pdfStore =
            vectorStore.GetCollection<int, PdfRecord>("pdfRecords");
        await pdfStore.EnsureCollectionExistsAsync();

        int chunkId = 0;
        foreach (string chunk in chunks)
        {
            // Generate vector for each chunk.
            ReadOnlyMemory<float> vector = await generator.GenerateVectorAsync(chunk);

            PdfRecord pdfRecord = new PdfRecord
            {
                Id = chunkId,
                FileName = pdfFilePath,
                Content = chunk,
                Vector = vector
            };

            await pdfStore.UpsertAsync(pdfRecord);
            chunkId++;
        }

        // Example: search the PDF content.
        string pdfQuery = "Can you tell me which unit has voltage";
        ReadOnlyMemory<float> pdfQueryEmbedding = await generator.GenerateVectorAsync(pdfQuery);

        List<VectorSearchResult<PdfRecord>> pdfResults = new List<VectorSearchResult<PdfRecord>>();
        await foreach (VectorSearchResult<PdfRecord> result in pdfStore.SearchAsync(pdfQueryEmbedding, top: 10))
        {
            pdfResults.Add(result);
        }

        Console.WriteLine("\nPDF Search Results:");
        foreach (VectorSearchResult<PdfRecord> result in pdfResults)
        {
            // Displaying the first 250 characters of the PDF chunk as a sample excerpt.
            string excerpt = result.Record.Content.Substring(0, Math.Min(250, result.Record.Content.Length));
            Console.WriteLine($"FileName: {result.Record.FileName}");
            Console.WriteLine($"Excerpt: {excerpt.Replace(Environment.NewLine, " ")}...");
            Console.WriteLine($"Vector match score: {result.Score}");
        }
    }
}