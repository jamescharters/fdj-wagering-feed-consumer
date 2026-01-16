using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Options;
using WageringStatsApi.Models;
using WageringStatsApi.Repositories;
using WageringStatsApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Strongly typed config with FluentValidation
builder.Services.AddSingleton<IValidator<WageringFeedConfig>, WageringFeedConfigValidator>();
builder.Services.AddSingleton<IValidateOptions<WageringFeedConfig>, FluentValidationOptionsAdapter<WageringFeedConfig>>();
builder.Services.AddOptions<WageringFeedConfig>()
    .Bind(builder.Configuration.GetSection(WageringFeedConfig.SectionName))
    .ValidateOnStart();

// Singleton of the core wagering data service so same in-memory structure serves all requests over service lifetime
builder.Services.AddSingleton<IWageringDataRepository, WageringDataRepository>();
builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();
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

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Wagering Stats API"));
}

app.Run();
