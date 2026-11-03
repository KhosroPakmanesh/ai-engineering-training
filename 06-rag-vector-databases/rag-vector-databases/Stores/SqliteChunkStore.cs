using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ResumeRag.Stores;

public sealed class SqliteChunkStore(string pathString) : IChunkStore
{
    public string DescriptionString => $"SQLite: {pathString}";

    // Read a single consistent snapshot. A missing database means the first ingestion.
    public async Task<ChunkIndex?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pathString))
        {
            return null;
        }

        using var sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pathString,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await sqliteConnection.OpenAsync(cancellationToken);
        using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
        using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();

        sqliteCommand.Transaction = sqliteTransaction;

        sqliteCommand.CommandText = "SELECT Json FROM Metadata WHERE Id = 1";
        string jsonString = (string?)await sqliteCommand.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidDataException("The SQLite index has no metadata.");
        IndexMetadata indexMetadata = JsonSerializer.Deserialize<IndexMetadata>(jsonString)
            ?? throw new InvalidDataException("The SQLite metadata is invalid.");
        sqliteCommand.CommandText = "SELECT ChunkJson, VectorJson FROM Chunks ORDER BY Id";

        var indexedChunks = new List<IndexedChunk>();
        using (var sqliteDataReader = await sqliteCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await sqliteDataReader.ReadAsync(cancellationToken))
            {
                indexedChunks.Add(new(
                    JsonSerializer.Deserialize<DocumentChunk>(sqliteDataReader.GetString(0))
                                ?? throw new InvalidDataException("Invalid stored chunk."),
                    JsonSerializer.Deserialize<List<float>>(sqliteDataReader.GetString(1))
                                ?? throw new InvalidDataException("Invalid stored vector.")));
            }
        }

        sqliteTransaction.Commit();

        return new(indexMetadata, indexedChunks);
    }

    // Upsert stable IDs and delete stale IDs atomically; a failed write preserves the previous snapshot.
    public async Task SynchronizeAsync(ChunkIndex chunkIndex, CancellationToken cancellationToken = default)
    {
        using var sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pathString,
            Pooling = false
        }.ToString());

        await sqliteConnection.OpenAsync(cancellationToken);
        using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
        using SqliteCommand setupSqliteCommand = sqliteConnection.CreateCommand();
        setupSqliteCommand.Transaction = sqliteTransaction;
        setupSqliteCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS Metadata (Id INTEGER PRIMARY KEY CHECK(Id = 1), Json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Chunks (Id TEXT PRIMARY KEY, ChunkJson TEXT NOT NULL, VectorJson TEXT NOT NULL);
            CREATE TEMP TABLE CurrentIds (Id TEXT PRIMARY KEY);
            INSERT INTO Metadata VALUES (1, $metadata) ON CONFLICT(Id) DO UPDATE SET Json = excluded.Json;
            """;
        setupSqliteCommand.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(chunkIndex.IndexMetadata));
        await setupSqliteCommand.ExecuteNonQueryAsync(cancellationToken);
        foreach (IndexedChunk indexedChunk in chunkIndex.IndexedChunks)
        {
            using SqliteCommand upsertSqliteCommand = sqliteConnection.CreateCommand();
            upsertSqliteCommand.Transaction = sqliteTransaction;
            upsertSqliteCommand.CommandText = """
                INSERT INTO CurrentIds VALUES ($id);
                INSERT INTO Chunks VALUES ($id, $chunk, $vector)
                ON CONFLICT(Id) DO UPDATE SET ChunkJson = excluded.ChunkJson, VectorJson = excluded.VectorJson;
                """;
            upsertSqliteCommand.Parameters.AddWithValue("$id", indexedChunk.DocumentChunk.ChunkIdString);
            upsertSqliteCommand.Parameters.AddWithValue("$chunk", JsonSerializer.Serialize(indexedChunk.DocumentChunk));
            upsertSqliteCommand.Parameters.AddWithValue("$vector", JsonSerializer.Serialize(indexedChunk.VectorFloats));
            await upsertSqliteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        using SqliteCommand deleteSqliteCommand = sqliteConnection.CreateCommand();
        deleteSqliteCommand.Transaction = sqliteTransaction;
        deleteSqliteCommand.CommandText = "DELETE FROM Chunks WHERE Id NOT IN (SELECT Id FROM CurrentIds)";
        await deleteSqliteCommand.ExecuteNonQueryAsync(cancellationToken);

        sqliteTransaction.Commit();
    }

    // SQLite persists the vectors; this small collection is scanned exactly in C#.
    public async Task<List<RetrievedChunk>> SearchAsync(List<float> queryFloats, int topKInt, CancellationToken cancellationToken = default)
    {
        ChunkIndex chunkIndex = await ReadAsync(cancellationToken)
            ?? throw new InvalidDataException("Ingest documents before searching.");

        IndexValidator.ValidateVector(queryFloats, chunkIndex.IndexMetadata.DimensionsInt);

        return chunkIndex.IndexedChunks
            .Select(indexedChunk => new RetrievedChunk(indexedChunk.DocumentChunk, VectorMath.Cosine(indexedChunk.VectorFloats, queryFloats)))
            .OrderByDescending(retrievedChunk => retrievedChunk.ScoreDouble)
            .ThenBy(retrievedChunk => retrievedChunk.DocumentChunk.ChunkIdString, StringComparer.Ordinal)
            .Take(topKInt).ToList();
    }

    // Connections belong to individual operations and are closed before the next stage.
    public void Dispose()
    {
    }
}
