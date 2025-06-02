using Npgsql;

namespace LLM.Services;

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

        // Create the pgvector extension
        string createExtension = "CREATE EXTENSION IF NOT EXISTS vector;";
        using (var extCommand = new NpgsqlCommand(createExtension, connection))
        {
            await extCommand.ExecuteNonQueryAsync();
        }

        // Create cosine similarity function
        string createCosineSimilarity = await File.ReadAllTextAsync("CosineSimilarity.sql");
        using (var csCommand = new NpgsqlCommand(createCosineSimilarity, connection))
        {
            await csCommand.ExecuteNonQueryAsync();
        }
    }
} 