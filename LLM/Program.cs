using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;

internal class CloudService
{
    [VectorStoreKey]
    public int Key { get; set; }

    [VectorStoreData]
    public string Name { get; set; }

    [VectorStoreData]
    public string Description { get; set; }

    [VectorStoreVector(Dimensions: 384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

internal class Program
{
    static async Task Main(string[] args)
    {
        // Prepare the list of cloud services.
        List<CloudService> cloudServices = new List<CloudService>
        {
            new CloudService {
                Key = 0,
                Name = "Azure App Service",
                Description = "Host .NET, Java, Node.js, and Python web applications and APIs in a fully managed Azure service. You only need to deploy your code to Azure. Azure takes care of all the infrastructure management like high availability, load balancing, and autoscaling."
            },
            new CloudService {
                Key = 1,
                Name = "Azure Service Bus",
                Description = "A fully managed enterprise message broker supporting both point to point and publish-subscribe integrations. It's ideal for building decoupled applications, queue-based load leveling, or facilitating communication between microservices."
            },
            new CloudService {
                Key = 2,
                Name = "Azure Blob Storage",
                Description = "Azure Blob Storage allows your applications to store and retrieve files in the cloud. Azure Storage is highly scalable to store massive amounts of data and data is stored redundantly to ensure high availability."
            },
            new CloudService {
                Key = 3,
                Name = "Microsoft Entra ID",
                Description = "Manage user identities and control access to your apps, data, and resources."
            },
            new CloudService {
                Key = 4,
                Name = "Azure Key Vault",
                Description = "Store and access application secrets like connection strings and API keys in an encrypted vault with restricted access to make sure your secrets and your application aren't compromised."
            },
            new CloudService {
                Key = 5,
                Name = "Azure AI Search",
                Description = "Information retrieval at scale for traditional and conversational search applications, with security and options for AI enrichment and vectorization."
            }
        };

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

        // Create and populate the in-memory vector store.
        var vectorStore = new InMemoryVectorStore();
        VectorStoreCollection<int, CloudService> cloudServicesStore =
            vectorStore.GetCollection<int, CloudService>("cloudServices");
        await cloudServicesStore.EnsureCollectionExistsAsync();

        foreach (CloudService service in cloudServices)
        {
            service.Vector = await generator.GenerateVectorAsync(service.Description);
            await cloudServicesStore.UpsertAsync(service);
        }

        // Convert a search query to a vector and search the vector store.
        string query = "Can you recomend me Azure service to store large documets.";
        ReadOnlyMemory<float> queryEmbedding = await generator.GenerateVectorAsync(query);

        var results = new List<VectorSearchResult<CloudService>>();
        await foreach (VectorSearchResult<CloudService> result in cloudServicesStore.SearchAsync(queryEmbedding, top: 1))
        {
            results.Add(result);
        }

        foreach (VectorSearchResult<CloudService> result in results)
        {
            Console.WriteLine($"Name: {result.Record.Name}");
            Console.WriteLine($"Description: {result.Record.Description}");
            Console.WriteLine($"Vector match score: {result.Score}");
        }
        
    }
}