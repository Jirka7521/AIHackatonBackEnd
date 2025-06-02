using LLM.Data;
using LLM.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<LLMDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Services
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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
