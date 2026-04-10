using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SecureRagChat.Services;

public interface IDemoDocumentCatalog
{
    DemoDocumentEntry? GetById(string id);
    DemoDocumentEntry? FindBestMatch(string? title, string? snippet);
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

    public DemoDocumentEntry? FindBestMatch(string? title, string? snippet)
    {
        var documents = GetOrLoadDocuments();
        var candidates = documents.Values;

        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.Trim();
            var titlePrefix = NormalizeSearchText(normalizedTitle);

            if (Regex.IsMatch(normalizedTitle, @"^\s*(document|source|reference)\s+\d+\s*$", RegexOptions.IgnoreCase))
            {
                normalizedTitle = string.Empty;
                titlePrefix = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                var exact = candidates.FirstOrDefault(doc =>
                    string.Equals(doc.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact;
                }

                var byContains = candidates.FirstOrDefault(doc =>
                    normalizedTitle.Contains(doc.Title, StringComparison.OrdinalIgnoreCase)
                    || doc.Title.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase));
                if (byContains is not null)
                {
                    return byContains;
                }
            }

            if (!string.IsNullOrWhiteSpace(titlePrefix))
            {
                var byTitlePrefix = candidates.FirstOrDefault(doc =>
                    doc.Title.Contains(titlePrefix, StringComparison.OrdinalIgnoreCase)
                    || doc.Snippet.Contains(titlePrefix, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(doc.Content)
                        && doc.Content.Contains(titlePrefix, StringComparison.OrdinalIgnoreCase)));
                if (byTitlePrefix is not null)
                {
                    return byTitlePrefix;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(snippet))
        {
            var normalizedSnippet = snippet.Trim();
            var snippetPrefix = normalizedSnippet
                .TrimEnd('.')
                .Replace("...", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (snippetPrefix.Length > 140)
            {
                snippetPrefix = snippetPrefix[..140].TrimEnd();
            }

            var bySnippet = candidates.FirstOrDefault(doc =>
                doc.Snippet.Contains(normalizedSnippet, StringComparison.OrdinalIgnoreCase)
                || normalizedSnippet.Contains(doc.Snippet, StringComparison.OrdinalIgnoreCase));
            if (bySnippet is not null)
            {
                return bySnippet;
            }

            if (!string.IsNullOrWhiteSpace(snippetPrefix))
            {
                var bySnippetPrefix = candidates.FirstOrDefault(doc =>
                    doc.Snippet.Contains(snippetPrefix, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(doc.Content)
                        && doc.Content.Contains(snippetPrefix, StringComparison.OrdinalIgnoreCase)));
                if (bySnippetPrefix is not null)
                {
                    return bySnippetPrefix;
                }
            }
        }

        var fuzzyNeedle = !string.IsNullOrWhiteSpace(title) ? title : snippet;
        var fuzzyMatch = FindBestTokenOverlapMatch(candidates, fuzzyNeedle);
        if (fuzzyMatch is not null)
        {
            return fuzzyMatch;
        }

        return null;
    }

    private static DemoDocumentEntry? FindBestTokenOverlapMatch(IEnumerable<DemoDocumentEntry> candidates, string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
        {
            return null;
        }

        var needleTokens = ExtractMeaningfulTokens(needle);
        if (needleTokens.Count == 0)
        {
            return null;
        }

        DemoDocumentEntry? best = null;
        var bestScore = 0;

        foreach (var doc in candidates)
        {
            var haystack = $"{doc.Title} {doc.Snippet} {doc.Content}";
            var haystackTokens = ExtractMeaningfulTokens(haystack);
            if (haystackTokens.Count == 0)
            {
                continue;
            }

            var score = needleTokens.Count(token => haystackTokens.Contains(token));
            if (score > bestScore)
            {
                best = doc;
                bestScore = score;
            }
        }

        // Require a minimum overlap to avoid accidental unrelated matches.
        return bestScore >= 3 ? best : null;
    }

    private static HashSet<string> ExtractMeaningfulTokens(string text)
    {
        var normalized = NormalizeSearchText(text).ToLowerInvariant();

        var stopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "the", "and", "for", "with", "that", "this", "from", "into", "about", "when", "where",
            "what", "have", "been", "were", "will", "shall", "your", "their", "they", "them", "into",
            "designed", "supports", "support", "used", "using", "only", "more", "than", "does", "not"
        };

        var tokens = Regex.Split(normalized, "[^a-z0-9-]+")
            .Where(token => token.Length >= 4 && !stopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);

        return tokens;
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = value
            .Replace("...", string.Empty, StringComparison.Ordinal)
            .Trim();

        normalized = normalized.TrimEnd('.', ',', ';', ':');

        if (normalized.Length > 180)
        {
            normalized = normalized[..180].TrimEnd();
        }

        return normalized;
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