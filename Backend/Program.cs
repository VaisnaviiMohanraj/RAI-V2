using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Backend.Services.Chat;
using Backend.Services.Document;
using Backend.Services.Auth;
using Backend.Services.Storage;
using Backend.Configuration;
// using Backend.Authentication; // Removed custom auth handler
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
                "https://www.rrrealty.ai" // Production domain
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Authentication - MSAL for all environments
try
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("EntraId"));
    Console.WriteLine("Authentication configured successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Authentication configuration failed: {ex.Message}");
    // Add basic authentication without Microsoft Identity to allow app to start
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();
}

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
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });
});

// Configure OpenAI settings
builder.Services.Configure<OpenAISettings>(options =>
{
    builder.Configuration.GetSection("OpenAI").Bind(options);
    
    // Use existing environment variables that are already deployed
    // Check production naming convention first (OPENAI_*), then Azure naming (AZURE_OPENAI_*)
    var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                   Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
    var envEndpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? 
                     Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                     "https://gto4o.openai.azure.com/"; // fallback
    var envDeployment = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME") ?? 
                       Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ??
                       Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ??
                       "gpt-4o"; // fallback
    var envApiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION") ?? 
                       Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ??
                       "2024-10-21"; // fallback updated to match production
    
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
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IStorageService, AzureStorageService>();
    
    // Register DocumentService as Scoped (was Singleton but depends on Scoped IFileValidationService)
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    
    // Register HttpClient for AzureFunctionService with timeout
    builder.Services.AddHttpClient<IAzureFunctionService, AzureFunctionService>()
        .ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // Prevent hanging on cold starts
        });
    
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
    throw; // Re-throw to prevent app from starting with broken services
}

builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

// CRITICAL: CORS must be applied BEFORE authentication
app.UseCors("AllowFrontend");

// Enable static files and default files for SPA
// Frontend files are deployed at root alongside Backend.dll
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Add session middleware
app.UseSession();

// Re-enable authentication for production
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add fallback for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
