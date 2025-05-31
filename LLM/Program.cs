using System.Text;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Configure console I/O to support Unicode characters (e.g., Czech letters).
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // Load application configuration from appsettings.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Initialize connection to the PostgreSQL database.
        string connectionString = configuration["Postgres:ConnectionString"];
        var database = new PostgresDatabase(connectionString);
        
        // Ensure that the required tables exist in the database.
        await database.EnsureTableExistsAsync();

        // Display the current state of the 'source_files' and 'vectors' tables.
        Console.WriteLine("Displaying contents of the 'source_files' table:");
        await database.PrintSourceFilesAsync();

        Console.WriteLine("\nDisplaying contents of the 'vectors' table:");
        await database.PrintVectorsAsync();

        // Prompt the user to determine if they want to upload a PDF file.
        Console.WriteLine("\nDo you want to upload a PDF file? (y/n):");
        string? uploadChoice = Console.ReadLine();
        if (uploadChoice?.ToLower() == "y")
        {
            // Ask for the full file path of the PDF.
            Console.WriteLine("Enter the full file path of the PDF to upload:");
            string? filePath = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    // Upload and vectorize the PDF file.
                    var uploadResult = await VectorEndpoints.UploadFileAsync(filePath);
                    Console.WriteLine(uploadResult.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception during file upload: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Invalid file path entered.");
            }
        }
        else
        {
            Console.WriteLine("Skipping PDF file upload.");
        }

        // Prompt the user for a content search query.
        Console.WriteLine("\nEnter a topic to search for relevant content:");
        string? topic = Console.ReadLine();
        Console.WriteLine("Enter the number of relevant vectors to return:");
        string? countStr = Console.ReadLine();

        // Validate input and perform the search if valid.
        if (int.TryParse(countStr, out int count) && !string.IsNullOrWhiteSpace(topic))
        {
            try
            {
                // Query the vector store using the provided topic and count.
                var results = await VectorEndpoints.QueryVectorsAsync(topic, count);
                Console.WriteLine($"\nTop {count} matching vectors for the topic: {topic}");

                // Iterate through each query result.
                foreach (var result in results)
                {
                    Console.WriteLine($"ID: {result.Id}");
                    Console.WriteLine($"Text: {result.Text}");

                    // Attempt to retrieve the file path corresponding to the vector id.
                    try
                    {
                        string filePath = await VectorEndpoints.GetFilePathAsync(result.Id);
                        Console.WriteLine($"File Path: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving file path: {ex.Message}");
                    }
                    Console.WriteLine("---");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during query: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Invalid input for topic or number of results.");
        }

        // Wait for user input before exiting.
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}