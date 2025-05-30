using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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

internal class ChatRecord
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Role { get; set; }

    [VectorStoreData]
    public string Message { get; set; }

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

        // Retrieve connection details and API key for vector creation.
        string vectorEndpoint = configuration["AzureOpenAIVector:Endpoint"];
        string vectorModel = configuration["AzureOpenAIVector:Model"];
        string vectorApiKey = configuration["AzureOpenAIVector:ApiKey"];

        // Create the embedding generator using AzureKeyCredential.
        IEmbeddingGenerator<string, Embedding<float>> generator =
            new AzureOpenAIClient(new Uri(vectorEndpoint), new AzureKeyCredential(vectorApiKey))
                .GetEmbeddingClient(deploymentName: vectorModel)
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

        //---------------Chat Only--------------------

        // Retrieve connection details for chat completions.
        string chatEndpoint = configuration["AzureOpenAIChat:Endpoint"];
        string chatModel = configuration["AzureOpenAIChat:Model"];
        string chatApiKey = configuration["AzureOpenAIChat:ApiKey"];

        IChatClient chatClient =
            new AzureOpenAIClient(new Uri(chatEndpoint), new AzureKeyCredential(chatApiKey))
            .GetChatClient(deploymentName: chatModel)
            .AsIChatClient();

        // Create and populate the in-memory vector store for chat messages.
        var chatStore = vectorStore.GetCollection<int, ChatRecord>("chatRecords");
        await chatStore.EnsureCollectionExistsAsync();

        // Initialize chat history with a system message.
        List<Microsoft.Extensions.AI.ChatMessage> chatHistory = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, "You are an AI assistant that answers questions based on conversation context.")
        };

        int chatMessageId = 0;
        while (true)
        {
            Console.WriteLine("Your prompt:");
            string? userPrompt = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                break;
            }

            // Vectorize user prompt.
            ReadOnlyMemory<float> userVector = await generator.GenerateVectorAsync(userPrompt);

            // Search for relevant previous chat messages with top 10 vectors.
            List<VectorSearchResult<ChatRecord>> searchResults = new List<VectorSearchResult<ChatRecord>>();
            await foreach (VectorSearchResult<ChatRecord> result in chatStore.SearchAsync(userVector, top: 10))
            {
                searchResults.Add(result);
            }

            // Print the top 10 matching conversation vectors.
            Console.WriteLine("\nTop 10 matching conversation vectors:");
            foreach (var result in searchResults)
            {
                // Convert the vector to an array for preview (showing first 3 components)
                float[] vecArray = result.Record.Vector.ToArray();
                string vecPreview = string.Join(", ", vecArray.Take(Math.Min(3, vecArray.Length)));
                Console.WriteLine($"{result.Record.Role}: {result.Record.Message} (Score: {result.Score:F3}). Vector[0..3]: [{vecPreview}]");
                Console.WriteLine("---");
            }

            // Build the augmented prompt.
            StringBuilder contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("Relevant previous conversation context:");
            foreach (var result in searchResults)
            {
                contextBuilder.AppendLine($"{result.Record.Role}: {result.Record.Message}");
                contextBuilder.AppendLine("---");
            }

            string augmentedPrompt = userPrompt;
            if (searchResults.Count > 0)
            {
                augmentedPrompt += "\n\nContext:\n" + contextBuilder.ToString();
            }

            // Add augmented user prompt to chat history.
            var userMessage = new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, augmentedPrompt);
            chatHistory.Add(userMessage);

            // Store the user message vector.
            ChatRecord userRecord = new ChatRecord
            {
                Id = chatMessageId++,
                Role = "User",
                Message = augmentedPrompt,
                Vector = await generator.GenerateVectorAsync(augmentedPrompt)
            };
            await chatStore.UpsertAsync(userRecord);

            Console.WriteLine("AI Response:");
            string response = "";
            await foreach (ChatResponseUpdate item in chatClient.GetStreamingResponseAsync(chatHistory))
            {
                Console.Write(item.Text);
                response += item.Text;
            }
            // Add the assistant response to history.
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, response));

            // Store the assistant response vector.
            ChatRecord aiRecord = new ChatRecord
            {
                Id = chatMessageId++,
                Role = "Assistant",
                Message = response,
                Vector = await generator.GenerateVectorAsync(response)
            };
            await chatStore.UpsertAsync(aiRecord);

            Console.WriteLine();
        }
    }
}