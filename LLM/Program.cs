using System.Text;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Set the console input and output encoding to support Unicode characters (e.g., Czech letters).
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // Build configuration from appsettings.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Retrieve PostgreSQL connection string.
        string connectionString = configuration["Postgres:ConnectionString"];
        var database = new PostgresDatabase(connectionString);

        // Ensure tables exist.
        await database.EnsureTableExistsAsync();

        // Display contents of both tables at startup.
        Console.WriteLine("Displaying contents of the 'source_files' table:");
        await database.PrintSourceFilesAsync();

        Console.WriteLine("\nDisplaying contents of the 'vectors' table:");
        await database.PrintVectorsAsync();

        // Create an instance of VectorEndpoints with full initialization.
        VectorEndpoints endpoints = await VectorEndpoints.CreateAsync();

        // Ask user whether to upload a PDF file.
        Console.WriteLine("Do you want to upload a PDF file? (y/n):");
        string? uploadChoice = Console.ReadLine();
        if (uploadChoice?.ToLower() == "y")
        {
            Console.WriteLine("Enter the full file path of the PDF to upload:");
            string? filePath = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    var uploadResult = await endpoints.UploadPdfAsync(filePath);
                    if (uploadResult.Status == OperationStatus.Success)
                    {
                        Console.WriteLine(uploadResult.Message);
                    }
                    else
                    {
                        Console.WriteLine($"Error during file upload: {uploadResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during file upload: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Invalid file path.");
            }
        }
        else
        {
            Console.WriteLine("Skipping file upload.");
        }

        // Ask user for search query.
        Console.WriteLine("Enter a topic to search for relevant content:");
        string? topic = Console.ReadLine();
        Console.WriteLine("Enter the number of relevant vectors to return:");
        string? countStr = Console.ReadLine();

        if (int.TryParse(countStr, out int count) && !string.IsNullOrWhiteSpace(topic))
        {
            var results = await endpoints.GetRelevantVectorsAsync(topic, count);
            Console.WriteLine($"\nTop {count} matching vectors for the topic: {topic} (Database search)");
            foreach (var result in results)
            {
                // Retrieve text for each vector record.
                string text = await endpoints.GetVectorTextAsync(result.Id);

                Console.WriteLine($"ID: {result.Id}");
                Console.WriteLine($"Distance: {result.Distance:F3}");
                Console.WriteLine($"Path: {result.FileName}");
                Console.WriteLine($"Text: {text}");
                Console.WriteLine("---");
            }
        }
        else
        {
            Console.WriteLine("Invalid input for topic or count.");
        }

        // Continue or exit.
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}