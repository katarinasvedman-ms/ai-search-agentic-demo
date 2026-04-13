using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using SecureRagChat.Auth;
using SecureRagChat.Configuration;
using SecureRagChat.Orchestration;
using SecureRagChat.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Authentication ---
// Supports both anonymous and authenticated requests.
// Authenticated users get entitled retrieval; anonymous users get public retrieval.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// --- Configuration (strongly typed) ---
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
builder.Services.Configure<AgenticRetrievalOptions>(
    builder.Configuration.GetSection(AgenticRetrievalOptions.SectionName));

// --- Azure credential (singleton, shared across services) ---
var azureTenantId = builder.Configuration["AzureAd:TenantId"];

builder.Services.AddSingleton<TokenCredential>(
    _ => CreateTokenCredential(azureTenantId));

// --- HTTP clients ---
builder.Services.AddHttpClient("AzureSearch");
builder.Services.AddHttpClient("AzureOpenAI");
builder.Services.AddHttpClient("AgenticRetrieval");

// --- Core services ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IDevelopmentUserTokenProvider, DevelopmentAzureCliUserTokenProvider>();
builder.Services.AddScoped<UserTokenAccessor>();
builder.Services.AddSingleton<IRetrievalService, AzureSearchRetrievalService>();
builder.Services.AddSingleton<IAgenticRetrievalService, AgenticRetrievalService>();
builder.Services.AddSingleton<IResponsesApiService, ResponsesApiService>();
builder.Services.AddSingleton<IDemoDocumentCatalog, DemoDocumentCatalog>();

// --- Orchestrator ---
builder.Services.AddScoped<ChatOrchestrator>();

// --- Controllers ---
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static TokenCredential CreateTokenCredential(string? tenantId)
{
    var credentialOptions = new DefaultAzureCredentialOptions();

    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        credentialOptions.TenantId = tenantId;
        credentialOptions.SharedTokenCacheTenantId = tenantId;
        credentialOptions.VisualStudioTenantId = tenantId;
    }

    return new DefaultAzureCredential(credentialOptions);
}
