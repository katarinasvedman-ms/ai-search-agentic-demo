using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecureRagChat.Services;

public interface IDemoDocumentCatalog
{
    DemoDocumentEntry? GetById(string id);
}

public sealed class DemoDocumentCatalog : IDemoDocumentCatalog
{
    private readonly string _contentRootPath;
    private readonly object _sync = new();
    private Dictionary<string, DemoDocumentEntry>? _documents;

    public DemoDocumentCatalog(IHostEnvironment environment)
    {
        _contentRootPath = environment.ContentRootPath;
    }

    public DemoDocumentEntry? GetById(string id)
    {
        var documents = GetOrLoadDocuments();
        if (documents.TryGetValue(id, out var entry))
        {
            return entry;
        }

        // If files were repaired after an earlier bad load, refresh once on miss.
        lock (_sync)
        {
            _documents = LoadDocuments(_contentRootPath);
            return _documents.TryGetValue(id, out entry) ? entry : null;
        }
    }

    private Dictionary<string, DemoDocumentEntry> GetOrLoadDocuments()
    {
        lock (_sync)
        {
            _documents ??= LoadDocuments(_contentRootPath);
            return _documents;
        }
    }

    private static Dictionary<string, DemoDocumentEntry> LoadDocuments(string contentRootPath)
    {
        var result = new Dictionary<string, DemoDocumentEntry>(StringComparer.OrdinalIgnoreCase);

        LoadFile(Path.Combine(contentRootPath, "demo-data", "public-documents.json"), false, result);
        LoadFile(Path.Combine(contentRootPath, "demo-data", "entitled-documents.json"), true, result);

        return result;
    }

    private static void LoadFile(string path, bool requiresAuthentication, Dictionary<string, DemoDocumentEntry> destination)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        List<DemoDocumentFileEntry>? docs;
        try
        {
            docs = JsonSerializer.Deserialize(json, DemoDocumentJsonContext.Default.ListDemoDocumentFileEntry);
        }
        catch (JsonException)
        {
            return;
        }

        if (docs is null)
        {
            return;
        }

        foreach (var doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Id)
                || string.IsNullOrWhiteSpace(doc.Title)
                || string.IsNullOrWhiteSpace(doc.Url)
                || string.IsNullOrWhiteSpace(doc.Category)
                || string.IsNullOrWhiteSpace(doc.Snippet))
            {
                continue;
            }

            var entry = new DemoDocumentEntry
            {
                Id = doc.Id,
                Title = doc.Title,
                Url = doc.Url,
                Category = doc.Category,
                Snippet = doc.Snippet,
                Content = doc.Content,
                RequiresAuthentication = requiresAuthentication
            };

            destination[entry.Id] = entry;
        }
    }
}

public sealed class DemoDocumentFileEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class DemoDocumentEntry
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Category { get; set; }
    public required string Snippet { get; set; }
    public string? Content { get; set; }
    public bool RequiresAuthentication { get; set; }
}

[JsonSerializable(typeof(List<DemoDocumentFileEntry>))]
internal partial class DemoDocumentJsonContext : JsonSerializerContext;