using Microsoft.Data.Sqlite;
using Xunit;

public class SqliteSearchIndexTests
{
    // Repeated writes replace stable IDs; removed rows and old model vectors cannot survive re-indexing.
    [Fact]
    public void Reindex_is_idempotent_and_replaces_stale_documents_and_metadata()
    {
        string pathString = Path.Combine(Path.GetTempPath(), $"embedding-index-{Guid.NewGuid():N}.sqlite");
        try
        {
            var firstIndexSearchIndex = new SearchIndex(
                "Test",
                "model-one",
                2,
                [
                new(new("P1", "Original service", ["Docker"]), [1, 0]),
                new(new("P2", "Removed service", ["REST"]), [0, 1])
            ]);
            SearchIndexValidator.Validate(firstIndexSearchIndex);
            SqliteSearchIndex.Replace(pathString, firstIndexSearchIndex);
            SqliteSearchIndex.Replace(pathString, firstIndexSearchIndex);
            SearchIndex firstReadSearchIndex = SqliteSearchIndex.Read(pathString);
            SearchIndexValidator.Validate(firstReadSearchIndex);
            Assert.Equal(2, firstReadSearchIndex.IndexedProjects.Count);
            Assert.Equal(new[] { "P1", "P2" },
                firstReadSearchIndex.IndexedProjects.Select(indexedProject => indexedProject.ProjectDescription.IdString));

            var changedIndexSearchIndex = new SearchIndex("Test", "model-two", 3,
                [new(new("P1", "Changed service", ["ASP.NET Core"]), [0, 0, 1])]);
            SearchIndexValidator.Validate(changedIndexSearchIndex);
            SqliteSearchIndex.Replace(pathString, changedIndexSearchIndex);
            SearchIndex storedSearchIndex = SqliteSearchIndex.Read(pathString);
            SearchIndexValidator.Validate(storedSearchIndex);

            Assert.Equal("model-two", storedSearchIndex.ModelString);
            Assert.Equal(3, storedSearchIndex.DimensionsInt);
            IndexedProject storedIndexedProject = Assert.Single(storedSearchIndex.IndexedProjects);
            Assert.Equal("P1", storedIndexedProject.ProjectDescription.IdString);
            Assert.Equal("Changed service", storedIndexedProject.ProjectDescription.TextString);
            Assert.Equal(new[] { "ASP.NET Core" }, storedIndexedProject.ProjectDescription.SkillStrings);
            Assert.Equal(new[] { 0f, 0f, 1f }, storedIndexedProject.VectorFloats);
        }
        finally { File.Delete(pathString); }
    }

    // A storage failure partway through replacement must leave the previous index complete.
    [Fact]
    public void Failed_replacement_rolls_back_documents_and_metadata_together()
    {
        string pathString = Path.Combine(Path.GetTempPath(), $"embedding-index-{Guid.NewGuid():N}.sqlite");
        try
        {
            var originalSearchIndex = new SearchIndex("Test", "original-model", 2, [new(new("P1", "Original service", ["Docker"]), [1, 0])]);
            SearchIndexValidator.Validate(originalSearchIndex);
            SqliteSearchIndex.Replace(pathString, originalSearchIndex);
            using (var sqliteConnection = new SqliteConnection($"Data Source={pathString};Pooling=False"))
            {
                sqliteConnection.Open();
                using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
                sqliteCommand.CommandText = "CREATE TRIGGER RejectReplacement BEFORE INSERT ON Projects BEGIN SELECT RAISE(ABORT, 'Test write failure'); END;";
                sqliteCommand.ExecuteNonQuery();
            }
            var replacementSearchIndex = new SearchIndex("Test", "new-model", 3,
                [new(new("P2", "Replacement service", ["Docker"]), [0, 0, 1])]);
            SearchIndexValidator.Validate(replacementSearchIndex);

            Assert.Throws<SqliteException>(() => SqliteSearchIndex.Replace(pathString, replacementSearchIndex));

            SearchIndex storedSearchIndex = SqliteSearchIndex.Read(pathString);
            SearchIndexValidator.Validate(storedSearchIndex);
            Assert.Equal("original-model", storedSearchIndex.ModelString);
            Assert.Equal(2, storedSearchIndex.DimensionsInt);
            Assert.Equal("P1", Assert.Single(storedSearchIndex.IndexedProjects).ProjectDescription.IdString);
        }
        finally { File.Delete(pathString); }
    }
}
