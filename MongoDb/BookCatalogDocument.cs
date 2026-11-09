using MongoDB.Bson.Serialization.Attributes;

namespace LibraryManagementApp.MongoDb;

public sealed class BookCatalogDocument
{
    [BsonId]
    public int Id { get; set; }

    [BsonElement("isbn")]
    public string Isbn { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("publicationYear")]
    public int? PublicationYear { get; set; }

    [BsonElement("language")]
    public string? Language { get; set; }

    [BsonElement("genre")]
    public string Genre { get; set; } = string.Empty;

    [BsonElement("publisher")]
    public string Publisher { get; set; } = string.Empty;

    [BsonElement("authors")]
    public List<string> Authors { get; set; } = [];

    [BsonElement("copies")]
    public List<BookCopyDocument> Copies { get; set; } = [];

    [BsonElement("totalCopies")]
    public int TotalCopies { get; set; }

    [BsonElement("availableCopies")]
    public int AvailableCopies { get; set; }
}

public sealed class BookCopyDocument
{
    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [BsonElement("branchId")]
    public int BranchId { get; set; }

    [BsonElement("branchName")]
    public string BranchName { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;
}