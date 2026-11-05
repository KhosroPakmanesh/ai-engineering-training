using System.Text.Json;
using Microsoft.Data.Sqlite;

public static class SqliteSearchIndex
{
    // Replace documents and metadata together; a failed replacement keeps the previous complete index.
    public static void Replace(
        string pathString,
        SearchIndex searchIndex)
    {
        using var sqliteConnection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = pathString,
                Pooling = false
            }.ToString());

        sqliteConnection.Open();
        using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
        using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
        sqliteCommand.Transaction = sqliteTransaction;
        sqliteCommand.CommandText = """
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
        sqliteCommand.Parameters.AddWithValue(
            "$provider",
            searchIndex.ProviderString);
        sqliteCommand.Parameters.AddWithValue(
            "$model",
            searchIndex.ModelString);
        sqliteCommand.Parameters.AddWithValue(
            "$dimensions",
            searchIndex.DimensionsInt);
        sqliteCommand.ExecuteNonQuery();

        for (int positionInt = 0; positionInt < searchIndex.IndexedProjects.Count; positionInt++)
        {
            IndexedProject indexedProject = searchIndex.IndexedProjects[positionInt];
            using SqliteCommand insertSqliteCommand = sqliteConnection.CreateCommand();
            insertSqliteCommand.Transaction = sqliteTransaction;
            insertSqliteCommand.CommandText = "INSERT INTO Projects VALUES ($id, $position, $text, $skills, $vector);";
            insertSqliteCommand.Parameters.AddWithValue(
                "$id",
                indexedProject.ProjectDescription.IdString);
            insertSqliteCommand.Parameters.AddWithValue(
                "$position",
                positionInt);
            insertSqliteCommand.Parameters.AddWithValue(
                "$text",
                indexedProject.ProjectDescription.TextString);
            insertSqliteCommand.Parameters.AddWithValue(
                "$skills",
                JsonSerializer.Serialize(indexedProject.ProjectDescription.SkillStrings));
            insertSqliteCommand.Parameters.AddWithValue(
                "$vector",
                JsonSerializer.Serialize(indexedProject.VectorFloats));
            insertSqliteCommand.ExecuteNonQuery();
        }

        sqliteTransaction.Commit();
    }

    // Read a consistent snapshot in source order; validation is a separate visible stage for the caller.
    public static SearchIndex Read(string pathString)
    {
        using var sqliteConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = pathString,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());

        sqliteConnection.Open();
        using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction(deferred: true);
        using SqliteCommand metadataSqliteCommand = sqliteConnection.CreateCommand();
        metadataSqliteCommand.Transaction = sqliteTransaction;
        metadataSqliteCommand.CommandText = "SELECT Provider, Model, Dimensions FROM IndexMetadata WHERE Id = 1;";
        string providerString;
        string modelString;
        int dimensionsInt;
        using (SqliteDataReader sqliteDataReader = metadataSqliteCommand.ExecuteReader())
        {
            if (!sqliteDataReader.Read())
            {
                throw new InvalidDataException("The SQLite index has no model metadata.");
            }

            providerString = sqliteDataReader.GetString(0);
            modelString = sqliteDataReader.GetString(1);
            dimensionsInt = sqliteDataReader.GetInt32(2);
        }

        using SqliteCommand documentsSqliteCommand = sqliteConnection.CreateCommand();
        documentsSqliteCommand.Transaction = sqliteTransaction;
        documentsSqliteCommand.CommandText = "SELECT Id, SourceText, Skills, Vector FROM Projects ORDER BY Position;";
        var indexedProjects = new List<IndexedProject>();

        using (SqliteDataReader sqliteDataReader = documentsSqliteCommand.ExecuteReader())
        {
            while (sqliteDataReader.Read())
            {
                List<string> skillStrings = JsonSerializer.Deserialize<List<string>>(sqliteDataReader.GetString(2))
                    ?? throw new InvalidDataException("An indexed project's skills must be a JSON list.");
                List<float> vectorFloats = JsonSerializer.Deserialize<List<float>>(sqliteDataReader.GetString(3))
                    ?? throw new InvalidDataException("An indexed project's vector must be a JSON list.");
                indexedProjects.Add(new(
                    new(
                        sqliteDataReader.GetString(0),
                        sqliteDataReader.GetString(1),
                        skillStrings),
                    vectorFloats));
            }
        }

        sqliteTransaction.Commit();

        return new(
            providerString,
            modelString,
            dimensionsInt,
            indexedProjects);
    }
}
