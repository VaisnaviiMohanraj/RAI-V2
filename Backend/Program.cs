using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Backend.Services.Chat;
using Backend.Services.Document;
using Backend.Services.Auth;
using Backend.Services.Storage;
using Backend.Configuration;
using DotNetEnv;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

// Load environment variables from .env file
try
{
    DotNetEnv.Env.Load();
}
catch (FileNotFoundException)
{
    // .env file not found, continue with system environment variables
    Console.WriteLine("Warning: .env file not found. Using system environment variables only.");
}

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault for production
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUrl = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL");
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        var credential = new DefaultAzureCredential();
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
    }
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add session services with secure configuration
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30); // Session expires after 30 days of inactivity
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Force HTTPS in production
    options.Cookie.Name = "__RRRealty_Session";
});

// Configure CORS for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://testing.rrrealty.ai", // Production domain
                "https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net", // Old Azure Web App URL
                "https://site-net-rrai-stage-hhhhbzf4b2drcdc5.centralus-01.azurewebsites.net" // New rrai-stage slot URL
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Authentication - MSAL for all environments
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("EntraId"));

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuthenticatedUser", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
    
    options.AddPolicy("RequireDomainUser", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("tid"); // Require tenant ID claim
    });
    
    // Add role-based policies if needed
    options.AddPolicy("RequireAdminRole", policy =>
    {
        policy.RequireRole("Admin");
    });
});

// Configure MSAL options with environment variables
// DISABLED: Using Azure App Service Easy Auth instead
// builder.Services.PostConfigure<MicrosoftIdentityOptions>(options =>
// {
//     // Get values from environment variables first, then fall back to appsettings
//     var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? Environment.GetEnvironmentVariable("ENTRAID_TENANTID");
//     var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? Environment.GetEnvironmentVariable("ENTRAID_CLIENTID");
//     var clientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? Environment.GetEnvironmentVariable("ENTRAID_CLIENTSECRET");
//     var domain = Environment.GetEnvironmentVariable("AZURE_DOMAIN") ?? Environment.GetEnvironmentVariable("ENTRAID_DOMAIN");
//     
//     Console.WriteLine($"Environment Variables - TenantId: {(string.IsNullOrEmpty(tenantId) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Environment Variables - ClientId: {(string.IsNullOrEmpty(clientId) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Environment Variables - ClientSecret: {(string.IsNullOrEmpty(clientSecret) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Environment Variables - Domain: {(string.IsNullOrEmpty(domain) ? "NOT SET" : "***SET***")}");
//     
//     // Only override if environment variables are set, otherwise keep appsettings values
//     if (!string.IsNullOrEmpty(tenantId))
//     {
//         options.TenantId = tenantId;
//     }
//     
//     if (!string.IsNullOrEmpty(clientId))
//     {
//         options.ClientId = clientId;
//     }
//     
//     if (!string.IsNullOrEmpty(clientSecret))
//     {
//         options.ClientSecret = clientSecret;
//     }
//     
//     if (!string.IsNullOrEmpty(domain))
//     {
//         options.Domain = domain;
//     }
//     
//     // Debug: Show final configuration (without exposing secrets)
//     Console.WriteLine($"Final MSAL Config - TenantId: {(string.IsNullOrEmpty(options.TenantId) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Final MSAL Config - ClientId: {(string.IsNullOrEmpty(options.ClientId) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Final MSAL Config - ClientSecret: {(string.IsNullOrEmpty(options.ClientSecret) ? "NOT SET" : "***SET***")}");
//     Console.WriteLine($"Final MSAL Config - Domain: {(string.IsNullOrEmpty(options.Domain) ? "NOT SET" : "***SET***")}");
// });

// Configure JWT Bearer options
// DISABLED: Using Azure App Service Easy Auth instead
// builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
// {
//     var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? Environment.GetEnvironmentVariable("ENTRAID_TENANTID");
//     var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? Environment.GetEnvironmentVariable("ENTRAID_CLIENTID");
//     
//     if (!string.IsNullOrEmpty(tenantId))
//     {
//         options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
//     }
//     
//     if (!string.IsNullOrEmpty(clientId))
//     {
//         options.Audience = clientId;
//     }
// });

// Configure OpenAI settings
builder.Services.Configure<OpenAISettings>(options =>
{
    builder.Configuration.GetSection("OpenAI").Bind(options);
    
    // Override with environment variables for security
    var envApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
    var envEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    // Prefer documented AZURE_OPENAI_DEPLOYMENT_NAME, fallback to AZURE_OPENAI_DEPLOYMENT
    var envDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
                        ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
    // Optional API version support per docs
    var envApiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION");
    
    if (!string.IsNullOrEmpty(envApiKey))
    {
        options.ApiKey = envApiKey;
        Console.WriteLine("OpenAI API Key loaded from environment variable");
    }
    else
    {
        Console.WriteLine("Warning: AZURE_OPENAI_API_KEY environment variable not found - AI features may not work");
    }
    
    if (!string.IsNullOrEmpty(envEndpoint))
    {
        options.Endpoint = envEndpoint;
        Console.WriteLine("OpenAI Endpoint loaded from environment variable");
    }
    else
    {
        Console.WriteLine("Warning: AZURE_OPENAI_ENDPOINT environment variable not found - AI features may not work");
    }
    
    if (!string.IsNullOrEmpty(envDeployment))
    {
        options.DeploymentName = envDeployment;
        Console.WriteLine("OpenAI Deployment loaded from environment variable (DeploymentName)");
    }
    else
    {
        Console.WriteLine("Warning: AZURE_OPENAI_DEPLOYMENT_NAME/AZURE_OPENAI_DEPLOYMENT environment variable not found - AI features may not work");
    }

    // Bind API version if provided
    if (!string.IsNullOrEmpty(envApiVersion))
    {
        options.ApiVersion = envApiVersion;
        Console.WriteLine("OpenAI API Version loaded from environment variable");
    }
    
    // Don't throw exception in production - let the app start but log warnings
    if (!builder.Environment.IsDevelopment())
    {
        if (string.IsNullOrEmpty(options.ApiKey) || string.IsNullOrEmpty(options.Endpoint) || string.IsNullOrEmpty(options.DeploymentName))
        {
            Console.WriteLine("WARNING: OpenAI configuration is incomplete. AI features will not work until environment variables are configured.");
        }
    }
});

// Register application services in correct dependency order
try
{
    // Register services with no dependencies first
    builder.Services.AddSingleton<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();
    builder.Services.AddScoped<IAzureFunctionService, AzureFunctionService>();
    builder.Services.AddScoped<IStorageService, AzureStorageService>();
    
    // Register chat-related services
    builder.Services.AddScoped<IConversationManager, ConversationManager>();
    builder.Services.AddScoped<IDocumentContextService, DocumentContextService>();
    
    // Register ChatService last (depends on other services)
    builder.Services.AddScoped<IChatService, ChatService>();
    
    Console.WriteLine("All services registered successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error registering services: {ex.Message}");
    // Register minimal services to allow app to start
    builder.Services.AddSingleton<IDocumentService, DocumentService>();
}

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

// CRITICAL: CORS must be applied BEFORE authentication
app.UseCors("AllowFrontend");

// Enable static files and default files for SPA
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Add session middleware
app.UseSession();

// Re-enable authentication for production deployment
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add fallback for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
