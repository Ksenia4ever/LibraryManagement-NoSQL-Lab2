
using System.Text.Json;
using LibraryManagementApp.MongoDb;
using StackExchange.Redis;

namespace LibraryManagementApp.Redis;

public sealed class RedisBookCacheService : IAsyncDisposable
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly MongoBookCatalogService _mongoService;

    private static readonly TimeSpan CacheLifetime =
        TimeSpan.FromSeconds(60);

    public RedisBookCacheService(
        string redisConnectionString,
        MongoBookCatalogService mongoService)
    {
        _connection = ConnectionMultiplexer.Connect(
            redisConnectionString);

        _database = _connection.GetDatabase();
        _mongoService = mongoService;
    }

    public async Task<BookCatalogDocument?> GetBookAsync(int bookId)
    {
        string key = $"library:book:{bookId}";

        RedisValue cachedValue =
            await _database.StringGetAsync(key);

        if (cachedValue.HasValue)
        {
            Console.WriteLine(
                $"[Redis] HIT: книга {bookId} знайдена в кеші.");

            return JsonSerializer.Deserialize<BookCatalogDocument>(
                cachedValue.ToString());
        }

        Console.WriteLine(
            $"[Redis] MISS: книги {bookId} немає в кеші.");

        BookCatalogDocument? book =
            await _mongoService.GetBookByIdAsync(bookId);

        if (book is null)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(book);

        await _database.StringSetAsync(
            key,
            json,
            CacheLifetime);

        Console.WriteLine(
            "[Redis] Книгу завантажено з MongoDB та кешовано.");

        return book;
    }

    public async Task<long> IncrementViewsAsync(int bookId)
    {
        string key = $"library:views:{bookId}";

        return await _database.StringIncrementAsync(key);
    }

    public async Task<long> GetViewsAsync(int bookId)
    {
        RedisValue value = await _database.StringGetAsync(
            $"library:views:{bookId}");

        if (!value.HasValue)
        {
            return 0;
        }

        return long.TryParse(value.ToString(), out long count)
            ? count
            : 0;
    }

    public async Task<TimeSpan?> GetCacheTtlAsync(int bookId)
    {
        return await _database.KeyTimeToLiveAsync(
            $"library:book:{bookId}");
    }

    public async Task InvalidateBookAsync(int bookId)
    {
        await _database.KeyDeleteAsync(
            $"library:book:{bookId}");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
