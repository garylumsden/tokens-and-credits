using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TokensAndCredits.Web.Api;
using TokensAndCredits.Web.Services.CacheDemo;
using TokensAndCredits.Web.Services.Catalog;
using TokensAndCredits.Web.Services.Chat;
using TokensAndCredits.Web.Services.Credits;
using TokensAndCredits.Web.Services.Image;
using TokensAndCredits.Web.Services.Tokenize;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureFoundryOptions>(
    builder.Configuration.GetSection(AzureFoundryOptions.SectionName));

builder.Services.Configure<CreditRatesOptions>(
    builder.Configuration.GetSection(CreditRatesOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpClient();

// Local tokenization
builder.Services.AddSingleton<TiktokenFactory>();
builder.Services.AddSingleton<QwenBpeFactory>();
builder.Services.AddSingleton<ITokenizerResolver, TokenizerResolver>();
builder.Services.AddSingleton<TokenAnalyzer>();
builder.Services.AddSingleton<TokenExplainer>();
builder.Services.AddSingleton<MergeTracer>();

// Model catalog (Azure deployments + local discovery)
builder.Services.AddSingleton<AzureDeploymentSource>();
builder.Services.AddSingleton<FoundryLocalSource>();
builder.Services.AddSingleton<LmStudioSource>();
builder.Services.AddSingleton<OllamaSource>();
builder.Services.AddHttpClient(LmStudioSource.HttpClientName, client =>
{
    client.BaseAddress = new Uri($"{LmStudioSource.BaseEndpoint.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddHttpClient(OllamaSource.HttpClientName, client =>
{
    client.BaseAddress = new Uri($"{OllamaSource.BaseEndpoint.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddSingleton<IModelCatalog, ModelCatalog>();

// Chat + usage
builder.Services.AddSingleton<UsageExtractor>();
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddSingleton<IFoundryChatService, FoundryChatService>();
builder.Services.AddSingleton<IImageGenerationService, ImageGenerationService>();

// Prompt-cache demo
builder.Services.AddSingleton<CacheDemoService>();

var app = builder.Build();

ValidateConfiguration(app);

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapTokenEndpoints();

app.Run();

static void ValidateConfiguration(WebApplication app)
{
    var options = app.Services.GetRequiredService<IOptions<AzureFoundryOptions>>().Value;
    var logger = app.Logger;

    if (!string.IsNullOrWhiteSpace(options.Endpoint) && options.Deployments.Count == 0)
    {
        // Endpoint set but nothing to call: fail closed rather than start half-configured.
        throw new InvalidOperationException(
            "AzureFoundry:Endpoint is set but no deployments are configured. Add AzureFoundry:Deployments or clear the endpoint.");
    }

    if (options.IsConfigured)
    {
        logger.LogInformation("Azure Foundry configured with {Count} deployment(s).", options.Deployments.Count);
    }
    else
    {
        logger.LogInformation("Azure Foundry not configured; running with Foundry Local models only.");
    }
}
