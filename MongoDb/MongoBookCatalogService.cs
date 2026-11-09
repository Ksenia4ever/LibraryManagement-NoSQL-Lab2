
using MongoDB.Driver;

namespace LibraryManagementApp.MongoDb;

public sealed class MongoBookCatalogService
{
    private readonly IMongoCollection<BookCatalogDocument> _collection;

    public MongoBookCatalogService(string connectionString)
    {
        var client = new MongoClient(connectionString);

        var database = client.GetDatabase("LibraryNoSqlDb");

        _collection = database.GetCollection<BookCatalogDocument>(
            "book_catalog");
    }

    public async Task UpsertBookAsync(
        BookCatalogDocument book,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BookCatalogDocument>.Filter
            .Eq(x => x.Id, book.Id);

        await _collection.ReplaceOneAsync(
            filter,
            book,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<List<BookCatalogDocument>> GetAllBooksAsync(
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(Builders<BookCatalogDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task<BookCatalogDocument?> GetBookByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> CountBooksAsync(
        CancellationToken cancellationToken = default)
    {
        return await _collection.CountDocumentsAsync(
            Builders<BookCatalogDocument>.Filter.Empty,
            cancellationToken: cancellationToken);
    }
}
