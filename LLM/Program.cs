using System.Text;

internal class Program
{
    static async Task Main(string[] args)
    {
        // Set the console input and output encoding to support Unicode characters (e.g., Czech letters).
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // Create an instance of VectorEndpoints with full initialization.
        VectorEndpoints endpoints = await VectorEndpoints.CreateAsync();

        // Endpoint to upload a PDF file and vectorize its content.
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

        // Query the vector store for relevant PDF vectors.
        Console.WriteLine("Enter a topic to search for relevant content:");
        string? topic = Console.ReadLine();
        Console.WriteLine("Enter the number of relevant vectors to return:");
        string? countStr = Console.ReadLine();

        if (int.TryParse(countStr, out int count) && !string.IsNullOrWhiteSpace(topic))
        {
            var results = await endpoints.GetRelevantVectorsAsync(topic, count);
            Console.WriteLine($"\nTop {count} matching vectors for the topic: {topic}");
            foreach (var result in results)
            {
                Console.WriteLine($"ID: {result.Record.Id}, Score: {result.Score:F3}");
                string snippet = result.Record.Content.Length > 100 
                    ? result.Record.Content.Substring(0, 100) + "..." 
                    : result.Record.Content;
                Console.WriteLine($"Snippet: {snippet}");
                Console.WriteLine("---");
            }
        }
        else
        {
            Console.WriteLine("Invalid input for topic or count.");
        }
    }
}