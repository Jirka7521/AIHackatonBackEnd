using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using System.Linq;
using System.Numerics;
using System.Globalization;

public class PostgresDatabase
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the PostgresDatabase class with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to connect to the PostgreSQL database.</param>
    public PostgresDatabase(string connectionString)
    {
        // Store the connection string for later database connections.
        _connectionString = connectionString;
    }

    /// <summary>
    /// Ensures that the necessary PostgreSQL extensions and tables exist.
    /// This method creates the pgvector extension, the cosine_similarity function, and the source_files and vectors tables.
    /// </summary>
    public async Task EnsureTableExistsAsync()
    {
        // Open a connection to the database.
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create the pgvector extension, if permissions allow (may fail on Azure due to restrictions).
        string createExtension = "CREATE EXTENSION IF NOT EXISTS vector;";
        using (var extCommand = new NpgsqlCommand(createExtension, connection))
        {
            await extCommand.ExecuteNonQueryAsync();
        }

        // Execute the SQL script for creating the cosine_similarity function.
        string createCosineSimilarity = System.IO.File.ReadAllText("CosineSimilarity.sql");
        using (var csCommand = new NpgsqlCommand(createCosineSimilarity, connection))
        {
            await csCommand.ExecuteNonQueryAsync();
        }

        // SQL to create the source_files table with an identity column.
        string createSourceFiles = @"
            CREATE TABLE IF NOT EXISTS public.source_files
            (
                id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                file_path text COLLATE pg_catalog.""default"" NOT NULL,
                CONSTRAINT source_files_pkey PRIMARY KEY (id)
            );
        ";

        // SQL to create the vectors table with pgvector's vector type and a snippet column.
        string createVectors = @"
            CREATE TABLE IF NOT EXISTS public.vectors
            (
                id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                source_file_id integer NOT NULL,
                vector_data vector(1536) NOT NULL,
                snippet text NOT NULL,
                CONSTRAINT vectors_pkey PRIMARY KEY (id),
                CONSTRAINT fk_source FOREIGN KEY (source_file_id)
                    REFERENCES public.source_files (id) MATCH SIMPLE
                    ON UPDATE NO ACTION
                    ON DELETE CASCADE
            );
        ";

        // Create the source_files table.
        using (var command = new NpgsqlCommand(createSourceFiles, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Create the vectors table.
        using (var command = new NpgsqlCommand(createVectors, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Inserts a PDF record into the database.
    /// This method checks for an existing source file and inserts the record into the vectors table using the appropriate source_file id.
    /// </summary>
    /// <param name="record">The PdfRecord containing file path, content, and vector data to be stored.</param>
    public async Task InsertPdfRecordAsync(PdfRecord record)
    {
        // Open a new database connection.
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // SQL query to check if the file already exists in source_files.
        string selectSourceFiles = @"
            SELECT id FROM public.source_files
            WHERE file_path = @file_path;
        ";

        int sourceFileId;
        using (var command = new NpgsqlCommand(selectSourceFiles, connection))
        {
            // Pass the file name as a parameter.
            command.Parameters.AddWithValue("file_path", record.FileName);
            var result = await command.ExecuteScalarAsync();
            if (result != null)
            {
                // If the file exists, use its id.
                sourceFileId = (int)result;
            }
            else
            {
                // Otherwise, insert the file into source_files and get the new id.
                string insertSourceFiles = @"
                    INSERT INTO public.source_files (file_path)
                    VALUES (@file_path)
                    RETURNING id;
                ";
                using var insertCommand = new NpgsqlCommand(insertSourceFiles, connection);
                insertCommand.Parameters.AddWithValue("file_path", record.FileName);
                sourceFileId = (int)await insertCommand.ExecuteScalarAsync();
            }
        }

        // SQL to insert the vector record into the vectors table.
        string insertVectors = @"
            INSERT INTO public.vectors (source_file_id, vector_data, snippet)
            VALUES (@source_file_id, @vector_data, @snippet)
            ON CONFLICT DO NOTHING;
        ";

        using (var command = new NpgsqlCommand(insertVectors, connection))
        {
            // Add parameters for source_file_id, converting the vector to an array, and the snippet (PDF text chunk).
            command.Parameters.AddWithValue("source_file_id", sourceFileId);
            command.Parameters.AddWithValue("vector_data", record.Vector.ToArray());
            command.Parameters.AddWithValue("snippet", record.Content);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Prints the contents of the source_files table to the console.
    /// This method retrieves the id and file_path of each source file stored in the database.
    /// </summary>
    public async Task PrintSourceFilesAsync()
    {
        // Open connection and execute a query to get all source_files.
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        string query = "SELECT id, file_path FROM public.source_files;";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        Console.WriteLine("Contents of source_files:");
        // Iterate through the result set and print each record.
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string filePath = reader.GetString(1);
            Console.WriteLine($"  id: {id}, file_path: {filePath}");
        }
    }

    /// <summary>
    /// Prints the contents of the vectors table to the console.
    /// This method retrieves vector data, casts it to text, parses the vector values, and displays the record details.
    /// </summary>
    public async Task PrintVectorsAsync()
    {
        // Open a connection and execute a query to get all vector records.
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Execute a query that casts vector data to text for processing.
        string query = "SELECT id, source_file_id, vector_data::text, snippet FROM public.vectors;";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        Console.WriteLine("Contents of vectors:");
        // Process each record from the vectors table.
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            int sourceFileId = reader.GetInt32(1);
            // Read the vector as text and trim whitespace.
            string vectorText = reader.GetString(2).Trim();
            string snippet = reader.GetString(3);

            float[] vectorData;
            if (vectorText.StartsWith("[") && vectorText.EndsWith("]"))
            {
                // Parse vector data from the text representation when stored as an array.
                vectorData = vectorText.Trim('[', ']')
                                       .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(x => float.Parse(x.Trim(), CultureInfo.InvariantCulture))
                                       .ToArray();
            }
            else
            {
                // Handle the scenario if a single float value is returned.
                vectorData = new float[] { float.Parse(vectorText, CultureInfo.InvariantCulture) };
            }

            string vectorStr = string.Join(", ", vectorData);
            Console.WriteLine($"  id: {id}, source_file_id: {sourceFileId}, vector_data: [{vectorStr}], snippet: {snippet}");
        }
    }

    /// <summary>
    /// Searches for similar vectors in the database based on the provided query vector.
    /// This method uses a custom cosine_similarity function to compute similarity and returns the top matching records.
    /// </summary>
    /// <param name="queryVector">The vector representation of the query text.</param>
    /// <param name="limit">The maximum number of similar vectors to return.</param>
    /// <returns>A task representing the asynchronous operation, with a collection of tuples containing the vector id, file path, and similarity score.</returns>
    public async Task<IEnumerable<(int Id, string FilePath, double Similarity)>> SearchSimilarVectorsAsync(ReadOnlyMemory<float> queryVector, int limit)
    {
        // Prepare a list to hold the search results.
        var results = new List<(int Id, string FilePath, double Similarity)>();
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // SQL query using the cosine_similarity function to rank similar vectors.
        string sql = @"
            SELECT v.id, s.file_path, public.cosine_similarity(v.vector_data, @query) AS similarity
            FROM public.vectors v
            JOIN public.source_files s ON s.id = v.source_file_id
            ORDER BY public.cosine_similarity(v.vector_data, @query) DESC
            LIMIT @limit;
        ";

        using var command = new NpgsqlCommand(sql, connection);
        // Add parameters: the query vector (as an array) and the limit.
        command.Parameters.AddWithValue("query", queryVector.ToArray());
        command.Parameters.AddWithValue("limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        // Read through the results and add to the collection.
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string filePath = reader.GetString(1);
            double similarity = reader.GetDouble(2);
            results.Add((id, filePath, similarity));
        }
        return results;
    }

    /// <summary>
    /// Retrieves a PDF record from the database by its identifier.
    /// This method joins the vectors and source_files tables to extract the file path and text snippet for the specified record.
    /// </summary>
    /// <param name="id">The identifier of the vector record.</param>
    /// <returns>A task representing the asynchronous operation, with a PdfRecord if found; otherwise, null.</returns>
    public async Task<PdfRecord?> GetPdfRecordByIdAsync(int id)
    {
        // Open a connection to execute the query.
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // SQL query to join vectors and source_files and retrieve a specific record by its id.
        string query = @"
            SELECT v.id, s.file_path, v.snippet
            FROM public.vectors v
            JOIN public.source_files s ON s.id = v.source_file_id
            WHERE v.id = @id;
        ";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync();
        // If a record is found, create a PdfRecord object.
        if (await reader.ReadAsync())
        {
            var record = new PdfRecord
            {
                Id = reader.GetInt32(0),
                FileName = reader.GetString(1),
                Content = reader.GetString(2),
                // Note: The vector field is not materialized here.
            };
            return record;
        }
        return null;
    }
}