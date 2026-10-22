using Microsoft.Data.Sqlite;
using Xunit;

public class SqliteSearchIndexTests
{
    // Repeated writes replace stable IDs; removed rows and old model vectors cannot survive re-indexing.
    [Fact]
    public void Reindex_is_idempotent_and_replaces_stale_documents_and_metadata()
    {
        string path = Path.Combine(Path.GetTempPath(), $"embedding-index-{Guid.NewGuid():N}.sqlite");
        try
        {
            var firstIndex = new SearchIndex("Test", "model-one", 2,
            [
                new(new("P1", "Original service", ["Docker"]), [1, 0]),
                new(new("P2", "Removed service", ["REST"]), [0, 1])
            ]);
            SearchIndexValidator.Validate(firstIndex);
            SqliteSearchIndex.Replace(path, firstIndex);
            SqliteSearchIndex.Replace(path, firstIndex);
            SearchIndex firstRead = SqliteSearchIndex.Read(path);
            SearchIndexValidator.Validate(firstRead);
            Assert.Equal(2, firstRead.Projects.Count);
            Assert.Equal(new[] { "P1", "P2" }, firstRead.Projects.Select(project => project.Project.Id));

            var changedIndex = new SearchIndex("Test", "model-two", 3,
                [new(new("P1", "Changed service", ["ASP.NET Core"]), [0, 0, 1])]);
            SearchIndexValidator.Validate(changedIndex);
            SqliteSearchIndex.Replace(path, changedIndex);
            SearchIndex storedIndex = SqliteSearchIndex.Read(path);
            SearchIndexValidator.Validate(storedIndex);

            Assert.Equal("model-two", storedIndex.Model);
            Assert.Equal(3, storedIndex.Dimensions);
            IndexedProject storedProject = Assert.Single(storedIndex.Projects);
            Assert.Equal("P1", storedProject.Project.Id);
            Assert.Equal("Changed service", storedProject.Project.Text);
            Assert.Equal(new[] { "ASP.NET Core" }, storedProject.Project.Skills);
            Assert.Equal(new[] { 0f, 0f, 1f }, storedProject.Vector);
        }
        finally { File.Delete(path); }
    }

    // A storage failure partway through replacement must leave the previous index complete.
    [Fact]
    public void Failed_replacement_rolls_back_documents_and_metadata_together()
    {
        string path = Path.Combine(Path.GetTempPath(), $"embedding-index-{Guid.NewGuid():N}.sqlite");
        try
        {
            var original = new SearchIndex("Test", "original-model", 2,
                [new(new("P1", "Original service", ["Docker"]), [1, 0])]);
            SearchIndexValidator.Validate(original);
            SqliteSearchIndex.Replace(path, original);
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER RejectReplacement BEFORE INSERT ON Projects BEGIN SELECT RAISE(ABORT, 'Test write failure'); END;";
                command.ExecuteNonQuery();
            }
            var replacement = new SearchIndex("Test", "new-model", 3,
                [new(new("P2", "Replacement service", ["Docker"]), [0, 0, 1])]);
            SearchIndexValidator.Validate(replacement);

            Assert.Throws<SqliteException>(() => SqliteSearchIndex.Replace(path, replacement));

            SearchIndex storedIndex = SqliteSearchIndex.Read(path);
            SearchIndexValidator.Validate(storedIndex);
            Assert.Equal("original-model", storedIndex.Model);
            Assert.Equal(2, storedIndex.Dimensions);
            Assert.Equal("P1", Assert.Single(storedIndex.Projects).Project.Id);
        }
        finally { File.Delete(path); }
    }
}
