using System.Text.Json;
using Microsoft.Extensions.Options;
using WageringStatsApi.Models;
using WageringStatsApi.Repositories;
using WageringStatsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Strongly typed config variables
builder.Services.AddOptions<WageringFeedConfig>()
    .Bind(builder.Configuration.GetSection(WageringFeedConfig.SectionName))
    .ValidateOnStart();

// DEVNOTE: we register services using interfaces where appropriate

// Singleton of the core wagering data service so same in-memory structure serves all requests over service lifetime
builder.Services.AddSingleton<IWageringDataRepository, WageringDataRepository>();
builder.Services.AddSingleton<IMessageProcessor, MessageProcessor>();
builder.Services.AddHttpClient<ICustomerService, CustomerService>();
builder.Services.AddHostedService<WebSocketWorker>();
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

try
{
    var wageringConfig = app.Services.GetRequiredService<IOptions<WageringFeedConfig>>().Value;
    
    // Execute sanity check logic for config variables, internally this could use something like FluentValidation
    wageringConfig.Validate();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Configuration validation failed");
    throw;
}

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Wagering Feed API"));
}

app.Run();
