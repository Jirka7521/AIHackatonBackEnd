using LLM.Data;
using LLM.Services;
using LLM.Middleware;
using Microsoft.EntityFrameworkCore;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Customize model validation behavior
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => new { Field = x.Key, Message = e.ErrorMessage }))
                .ToList();
            
            return new BadRequestObjectResult(new { Success = false, Message = "Validation failed", Errors = errors });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "LLM API", 
        Version = "v1",
        Description = "API for managing workspaces, chats, messages, and attachments"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Entity Framework
builder.Services.AddDbContext<LLMDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Azure OpenAI services
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    string vectorEndpoint = config["AzureOpenAIVector:Endpoint"]!;
    string vectorModel = config["AzureOpenAIVector:Model"]!;
    string vectorApiKey = config["AzureOpenAIVector:ApiKey"]!;

    return new AzureOpenAIClient(new Uri(vectorEndpoint), new AzureKeyCredential(vectorApiKey))
        .GetEmbeddingClient(deploymentName: vectorModel)
        .AsIEmbeddingGenerator();
});

builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    string endpoint = config["AzureOpenAILLM:Endpoint"]!;
    string deployment = config["AzureOpenAILLM:Deployment"]!;
    string apiKey = config["AzureOpenAILLM:ApiKey"]!;

    return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
        .GetChatClient(deployment)
        .AsIChatClient();
});

// Add Services
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAiService, AiService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created and apply pending migrations (optional in development)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LLMDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Test connection first
        await context.Database.CanConnectAsync();
        logger.LogInformation("Database connection successful. Applying migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations completed successfully.");

        // Initialize vector tables and extensions
        try
        {
            var postgresService = new PostgresDatabase(context.Database.GetConnectionString()!);
            await postgresService.EnsureTableExistsAsync();
            logger.LogInformation("Vector database tables and extensions initialized successfully.");
        }
        catch (Exception vectorEx)
        {
            logger.LogWarning(vectorEx, "Could not initialize vector database extensions. Vector operations may not work properly.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not connect to database or apply migrations. The application will start but database operations will fail.");
        logger.LogInformation("To fix this:");
        logger.LogInformation("1. For local development: Start a local PostgreSQL server and update appsettings.Development.json");
        logger.LogInformation("2. For Azure: Configure firewall rules and verify connection string in appsettings.json");
        logger.LogInformation("3. Or use SQL Server LocalDB for development instead");
    }
}

app.Run();
