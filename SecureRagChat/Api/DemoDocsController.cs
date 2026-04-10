using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureRagChat.Configuration;
using SecureRagChat.Services;

namespace SecureRagChat.Api;

[ApiController]
[Route("api/demo-docs")]
[AllowAnonymous]
public sealed class DemoDocsController : ControllerBase
{
    private readonly IDemoDocumentCatalog _catalog;
  private readonly IHostEnvironment _hostEnvironment;
  private readonly AzureSearchOptions _azureSearchOptions;

  public DemoDocsController(
    IDemoDocumentCatalog catalog,
    IHostEnvironment hostEnvironment,
    IOptions<AzureSearchOptions> azureSearchOptions)
    {
        _catalog = catalog;
    _hostEnvironment = hostEnvironment;
    _azureSearchOptions = azureSearchOptions.Value;
    }

    [HttpGet("{id}")]
    public IActionResult GetDocument(string id)
    {
        var doc = _catalog.GetById(id);
        if (doc is null)
        {
            return NotFound("Document not found.");
        }

      var isDemoAuthenticated = IsDemoAuthenticated();

      if (doc.RequiresAuthentication && !isDemoAuthenticated)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Content("<html><body><h1>Sign-in required</h1><p>This document is available only in the authenticated demo flow.</p></body></html>", "text/html");
        }

      return Content(BuildHtml(doc, User, isDemoAuthenticated), "text/html", Encoding.UTF8);
    }

    [HttpGet("resolve")]
    public IActionResult ResolveDocument([FromQuery] string? title, [FromQuery] string? snippet, [FromQuery] int? sourceIndex)
    {
      if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(snippet))
      {
        return BadRequest("Either title or snippet is required.");
      }

      var doc = _catalog.FindBestMatch(title, snippet);

      if (doc is null)
      {
        var fallbackIndex = sourceIndex ?? TryParseDocumentOrdinal(title);
        if (fallbackIndex is > 0)
        {
          var preferEntitled = IsDemoAuthenticated();
          doc = ResolveByOrdinalFallback(fallbackIndex.Value, preferEntitled);
        }
      }

      if (doc is null)
      {
        return NotFound("Unable to resolve source to a known demo document.");
      }

      var isDemoAuthenticated = IsDemoAuthenticated();
      if (doc.RequiresAuthentication && !isDemoAuthenticated)
      {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Content("<html><body><h1>Sign-in required</h1><p>This document is available only in the authenticated demo flow.</p></body></html>", "text/html");
      }

      var targetPath = $"/api/demo-docs/{doc.Id}";
      if (string.Equals(HttpContext.Request.Query["demoAuth"], "1", StringComparison.Ordinal))
      {
        targetPath += "?demoAuth=1";
      }

      return Redirect(targetPath);
    }

    private DemoDocumentEntry? ResolveByOrdinalFallback(int sourceIndex, bool preferEntitled)
    {
      if (sourceIndex <= 0)
      {
        return null;
      }

      var entitledId = $"ent-{sourceIndex:000}";
      var publicId = $"pub-{sourceIndex:000}";

      if (preferEntitled)
      {
        return _catalog.GetById(entitledId) ?? _catalog.GetById(publicId);
      }

      return _catalog.GetById(publicId) ?? _catalog.GetById(entitledId);
    }

    private static int? TryParseDocumentOrdinal(string? title)
    {
      if (string.IsNullOrWhiteSpace(title))
      {
        return null;
      }

      var match = Regex.Match(title, @"\b(?:document|source|reference)\s*(\d+)\b", RegexOptions.IgnoreCase);
      if (!match.Success)
      {
        return null;
      }

      return int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private bool IsDemoAuthenticated()
    {
      if (User.Identity?.IsAuthenticated == true)
      {
        return true;
      }

      if (_hostEnvironment.IsDevelopment()
        && _azureSearchOptions.UseLoggedInDeveloperIdentityForUserToken
        && string.Equals(HttpContext.Request.Query["demoAuth"], "1", StringComparison.Ordinal))
      {
        return true;
      }

      return _hostEnvironment.IsDevelopment() && _azureSearchOptions.UseLoggedInDeveloperIdentityForUserToken;
    }

    private static string BuildHtml(DemoDocumentEntry doc, ClaimsPrincipal user, bool isDemoAuthenticated)
    {
        var body = WebUtility.HtmlEncode(doc.Content ?? doc.Snippet)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n\n", "</p><p>", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);

        var accessLabel = doc.RequiresAuthentication
        ? (isDemoAuthenticated ? "Authenticated document" : "Protected document")
            : "Public document";

        return $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{WebUtility.HtmlEncode(doc.Title)}</title>
  <style>
    body {{ font-family: Segoe UI, sans-serif; margin: 0; background: #f4f0e8; color: #1d1d1d; }}
    main {{ max-width: 860px; margin: 0 auto; padding: 48px 24px 72px; }}
    .eyebrow {{ display: inline-block; margin-bottom: 12px; padding: 6px 10px; background: #1d1d1d; color: #fff; font-size: 12px; letter-spacing: 0.08em; text-transform: uppercase; }}
    h1 {{ margin: 0 0 12px; font-size: 40px; line-height: 1.1; }}
    .meta {{ color: #5d5347; margin-bottom: 28px; }}
    .summary {{ padding: 18px 20px; background: #fffaf0; border-left: 4px solid #a65a3a; margin-bottom: 24px; }}
    article {{ background: #fff; padding: 24px; box-shadow: 0 12px 32px rgba(0,0,0,0.08); }}
    p {{ line-height: 1.65; margin: 0 0 16px; }}
  </style>
</head>
<body>
  <main>
    <span class=""eyebrow"">{WebUtility.HtmlEncode(accessLabel)}</span>
    <h1>{WebUtility.HtmlEncode(doc.Title)}</h1>
    <div class=""meta"">Category: {WebUtility.HtmlEncode(doc.Category)} · Document ID: {WebUtility.HtmlEncode(doc.Id)}</div>
    <section class=""summary""><strong>Summary.</strong> {WebUtility.HtmlEncode(doc.Snippet)}</section>
    <article><p>{body}</p></article>
  </main>
</body>
</html>";
    }
}