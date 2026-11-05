using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace ResumeRag.Stores;

public sealed class QdrantChunkStore(
    QdrantClient qdrantClient,
    string collectionNameString) : IChunkStore
{
    public string DescriptionString => $"Qdrant collection: {collectionNameString} (local gRPC server)";

    // Scroll all lesson records so unchanged vectors can be reused on the next ingestion.
    public async Task<ChunkIndex?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!await qdrantClient.CollectionExistsAsync(
            collectionNameString,
            cancellationToken))
        {
            return null;
        }

        var indexedChunks = new List<IndexedChunk>();
        IndexMetadata? indexMetadata = null;
        PointId? offsetPointId = null;
        do
        {
            ScrollResponse pageScrollResponse = await qdrantClient.ScrollAsync(
                collectionNameString,
                limit: 100,
                offset: offsetPointId,
                payloadSelector: true,
                vectorsSelector: true,
                cancellationToken: cancellationToken);
            foreach (RetrievedPoint retrievedPoint in pageScrollResponse.Result)
            {
                IndexMetadata currentIndexMetadata =
                    JsonSerializer.Deserialize<IndexMetadata>(retrievedPoint.Payload["metadata"].StringValue)
                    ?? throw new InvalidDataException("Invalid Qdrant index metadata.");

                if (indexMetadata is not null && indexMetadata != currentIndexMetadata)
                {
                    throw new InvalidDataException(
                        "Qdrant contains mixed index metadata. Use a new lesson collection.");
                }

                indexMetadata = currentIndexMetadata;
                DocumentChunk documentChunk =
                    JsonSerializer.Deserialize<DocumentChunk>(retrievedPoint.Payload["chunk"].StringValue)
                    ?? throw new InvalidDataException("Invalid Qdrant chunk.");
                indexedChunks.Add(new(
                    documentChunk,
                    retrievedPoint.Vectors.Vector.Dense.Data.ToList()));
            }
            if (indexedChunks.Count > 500)
            {
                throw new InvalidDataException("The lesson collection exceeds 500 chunks.");
            }

            offsetPointId = pageScrollResponse.NextPageOffset;
        } while (offsetPointId is not null);

        if (indexMetadata is null)
        {
            return null;
        }

        CollectionInfo collectionInfo = await qdrantClient.GetCollectionInfoAsync(
            collectionNameString,
            cancellationToken);
        if (collectionInfo.Config.Params.VectorsConfig.Params.Size != (ulong)indexMetadata.DimensionsInt ||
            collectionInfo.Config.Params.VectorsConfig.Params.Distance != Distance.Cosine)
        {
            throw new InvalidDataException(
                "Qdrant collection dimension or distance does not match the index metadata.");
        }

        return new(
            indexMetadata,
            indexedChunks
            .OrderBy(
                indexedChunk => indexedChunk.DocumentChunk.ChunkIdString,
                StringComparer.Ordinal)
            .ToList());
    }

    // Upsert the desired collection, then remove stale points. These server operations are not a transaction.
    public async Task SynchronizeAsync(
        ChunkIndex chunkIndex,
        CancellationToken cancellationToken = default)
    {
        ChunkIndex? previousChunkIndex = await ReadAsync(cancellationToken);
        if (!await qdrantClient.CollectionExistsAsync(
            collectionNameString,
            cancellationToken))
        {
            await qdrantClient.CreateCollectionAsync(
                collectionNameString,
                new VectorParams
                {
                    Size = (ulong)chunkIndex.IndexMetadata.DimensionsInt,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);
        }

        CollectionInfo collectionInfo = await qdrantClient.GetCollectionInfoAsync(
            collectionNameString,
            cancellationToken);

        if (collectionInfo.Config.Params.VectorsConfig.Params.Size != (ulong)chunkIndex.IndexMetadata.DimensionsInt ||
            collectionInfo.Config.Params.VectorsConfig.Params.Distance != Distance.Cosine)
        {
            throw new InvalidDataException(
                "Qdrant collection dimension or distance mismatch. Use a new lesson collection.");
        }

        var pointStructs = new List<PointStruct>();
        foreach (IndexedChunk indexedChunk in chunkIndex.IndexedChunks)
        {
            pointStructs.Add(new PointStruct
            {
                Id = PointIdFor(indexedChunk.DocumentChunk.ChunkIdString),
                Vectors = indexedChunk.VectorFloats.ToArray(),
                Payload =
                {
                    ["chunk"] = JsonSerializer.Serialize(indexedChunk.DocumentChunk),
                    ["metadata"] = JsonSerializer.Serialize(chunkIndex.IndexMetadata)
                }
            });
        }

        await qdrantClient.UpsertAsync(
            collectionNameString,
            pointStructs,
            wait: true,
            cancellationToken: cancellationToken);

        var currentChunkIdStringsHashSet = chunkIndex.IndexedChunks
            .Select(indexedChunk => indexedChunk.DocumentChunk.ChunkIdString)
            .ToHashSet(StringComparer.Ordinal);
        List<Guid> stalePointGuids = previousChunkIndex?.IndexedChunks
            .Where(indexedChunk => !currentChunkIdStringsHashSet.Contains(indexedChunk.DocumentChunk.ChunkIdString))
            .Select(indexedChunk => PointIdFor(indexedChunk.DocumentChunk.ChunkIdString))
            .ToList() ?? [];

        if (stalePointGuids.Count > 0)
        {
            await qdrantClient.DeleteAsync(
                collectionNameString,
                stalePointGuids,
                wait: true,
                cancellationToken: cancellationToken);
        }
    }

    // Qdrant uses UUID point IDs. Hashing the source ID makes upserts stable across processes and runs.
    private static Guid PointIdFor(string chunkIdString)
    {
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(chunkIdString)).AsSpan(
            0,
            16));
    }

    // Ask the server for exact cosine search. A final ID sort makes returned ties easier to inspect.
    public async Task<List<RetrievedChunk>> SearchAsync(
        List<float> queryFloats,
        int topKInt,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScoredPoint> retrievedScoredPoints =
            await qdrantClient.QueryAsync(
                collectionNameString,
                query: queryFloats.ToArray(),
                searchParams: new SearchParams
                {
                    Exact = true
                },
                limit: (ulong)topKInt,
                payloadSelector: true,
                cancellationToken: cancellationToken);

        return retrievedScoredPoints
            .Select(scoredPoint => new RetrievedChunk(
                JsonSerializer.Deserialize<DocumentChunk>(scoredPoint.Payload["chunk"].StringValue)
                    ?? throw new InvalidDataException("Invalid Qdrant search result."),
                scoredPoint.Score)
            )
            .OrderByDescending(retrievedChunk => retrievedChunk.ScoreDouble)
            .ThenBy(
                retrievedChunk => retrievedChunk.DocumentChunk.ChunkIdString,
                StringComparer.Ordinal)
            .ToList();
    }

    // The selected store owns its SDK connection.
    public void Dispose()
    {
        qdrantClient.Dispose();
    }
}
