using System;
using System.Threading.Tasks;
using Npgsql;
using System.Linq;

public class PostgresDatabase
{
    private readonly string _connectionString;

    public PostgresDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task EnsureTableExistsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Create source_files table with identity column.
        string createSourceFiles = @"
            CREATE TABLE IF NOT EXISTS public.source_files
            (
                id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                file_path text COLLATE pg_catalog.""default"" NOT NULL,
                CONSTRAINT source_files_pkey PRIMARY KEY (id)
            );
        ";

        // Create vectors table with identity column.
        string createVectors = @"
            CREATE TABLE IF NOT EXISTS public.vectors
            (
                id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                source_file_id integer NOT NULL,
                vector_data real[] NOT NULL,
                CONSTRAINT vectors_pkey PRIMARY KEY (id),
                CONSTRAINT fk_source FOREIGN KEY (source_file_id)
                    REFERENCES public.source_files (id) MATCH SIMPLE
                    ON UPDATE NO ACTION
                    ON DELETE CASCADE
            );
        ";

        using (var command = new NpgsqlCommand(createSourceFiles, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        using (var command = new NpgsqlCommand(createVectors, connection))
        {
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task InsertPdfRecordAsync(PdfRecord record)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Check if the file already exists in source_files.
        string selectSourceFiles = @"
            SELECT id FROM public.source_files
            WHERE file_path = @file_path;
        ";

        int sourceFileId;
        using (var command = new NpgsqlCommand(selectSourceFiles, connection))
        {
            command.Parameters.AddWithValue("file_path", record.FileName);
            var result = await command.ExecuteScalarAsync();
            if (result != null)
            {
                sourceFileId = (int)result;
            }
            else
            {
                // Insert into source_files table to obtain a new id if not exists.
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

        // Insert into vectors table using the obtained source_file id.
        string insertVectors = @"
            INSERT INTO public.vectors (source_file_id, vector_data)
            VALUES (@source_file_id, @vector_data)
            ON CONFLICT DO NOTHING;
        ";

        using (var command = new NpgsqlCommand(insertVectors, connection))
        {
            command.Parameters.AddWithValue("source_file_id", sourceFileId);
            // Convert ReadOnlyMemory<float> to float[] since Npgsql expects an array of floats.
            command.Parameters.AddWithValue("vector_data", record.Vector.ToArray());
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task PrintSourceFilesAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        string query = "SELECT id, file_path FROM public.source_files;";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        Console.WriteLine("Contents of source_files:");
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string filePath = reader.GetString(1);
            Console.WriteLine($"  id: {id}, file_path: {filePath}");
        }
    }

    public async Task PrintVectorsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        string query = "SELECT id, source_file_id, vector_data FROM public.vectors;";
        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        Console.WriteLine("Contents of vectors:");
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            int sourceFileId = reader.GetInt32(1);
            float[] vectorData = reader.GetFieldValue<float[]>(2);
            string vectorStr = string.Join(", ", vectorData);
            Console.WriteLine($"  id: {id}, source_file_id: {sourceFileId}, vector_data: [{vectorStr}]");
        }
    }
}