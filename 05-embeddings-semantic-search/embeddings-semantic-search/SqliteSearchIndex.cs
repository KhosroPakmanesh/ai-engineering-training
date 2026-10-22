using System.Text.Json;
using Microsoft.Data.Sqlite;

public static class SqliteSearchIndex
{
    // Replace documents and metadata together; a failed replacement keeps the previous complete index.
    public static void Replace(string path, SearchIndex searchIndex)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());

        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS IndexMetadata (
                Id INTEGER PRIMARY KEY CHECK (Id = 1), Provider TEXT NOT NULL,
                Model TEXT NOT NULL, Dimensions INTEGER NOT NULL CHECK (Dimensions > 0));
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT PRIMARY KEY, Position INTEGER NOT NULL UNIQUE,
                SourceText TEXT NOT NULL, Skills TEXT NOT NULL, Vector TEXT NOT NULL);
            DELETE FROM Projects;
            DELETE FROM IndexMetadata;
            INSERT INTO IndexMetadata VALUES (1, $provider, $model, $dimensions);
            """;
        command.Parameters.AddWithValue("$provider", searchIndex.Provider);
        command.Parameters.AddWithValue("$model", searchIndex.Model);
        command.Parameters.AddWithValue("$dimensions", searchIndex.Dimensions);
        command.ExecuteNonQuery();

        for (int position = 0; position < searchIndex.Projects.Count; position++)
        {
            IndexedProject indexedProject = searchIndex.Projects[position];
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Projects VALUES ($id, $position, $text, $skills, $vector);";
            insert.Parameters.AddWithValue("$id", indexedProject.Project.Id);
            insert.Parameters.AddWithValue("$position", position);
            insert.Parameters.AddWithValue("$text", indexedProject.Project.Text);
            insert.Parameters.AddWithValue("$skills", JsonSerializer.Serialize(indexedProject.Project.Skills));
            insert.Parameters.AddWithValue("$vector", JsonSerializer.Serialize(indexedProject.Vector));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // Read a consistent snapshot in source order; validation is a separate visible stage for the caller.
    public static SearchIndex Read(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());

        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction(deferred: true);
        using SqliteCommand metadata = connection.CreateCommand();
        metadata.Transaction = transaction;
        metadata.CommandText = "SELECT Provider, Model, Dimensions FROM IndexMetadata WHERE Id = 1;";
        string provider;
        string model;
        int dimensions;
        using (SqliteDataReader reader = metadata.ExecuteReader())
        {
            if (!reader.Read()) throw new InvalidDataException("The SQLite index has no model metadata.");
            provider = reader.GetString(0);
            model = reader.GetString(1);
            dimensions = reader.GetInt32(2);
        }

        using SqliteCommand documents = connection.CreateCommand();
        documents.Transaction = transaction;
        documents.CommandText = "SELECT Id, SourceText, Skills, Vector FROM Projects ORDER BY Position;";
        var projects = new List<IndexedProject>();

        using (SqliteDataReader reader = documents.ExecuteReader())
        {
            while (reader.Read())
            {
                List<string> skills = JsonSerializer.Deserialize<List<string>>(reader.GetString(2))
                    ?? throw new InvalidDataException("An indexed project's skills must be a JSON list.");
                List<float> vector = JsonSerializer.Deserialize<List<float>>(reader.GetString(3))
                    ?? throw new InvalidDataException("An indexed project's vector must be a JSON list.");
                projects.Add(new(new(reader.GetString(0), reader.GetString(1), skills), vector));
            }
        }

        transaction.Commit();

        return new(provider, model, dimensions, projects);
    }
}
