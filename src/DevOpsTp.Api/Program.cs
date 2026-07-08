using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DevOpsTp.Api.Quests;

var builder = WebApplication.CreateBuilder(args);

var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "devops-tp-api";
var appVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? ApiVersion.Current;
var gitCommit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "local";
var buildDate = Environment.GetEnvironmentVariable("BUILD_DATE") ?? "local";

var environmentName = builder.Environment.EnvironmentName;

var otlpEndpointConfigured = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: serviceName,
            serviceVersion: appVersion)
        .AddAttributes(new[]
        {
            new KeyValuePair<string, object>("deployment.environment", environmentName)
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation();

        if (otlpEndpointConfigured)
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (otlpEndpointConfigured)
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<QuestStore>();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            title = "An unexpected error occurred",
            status = StatusCodes.Status500InternalServerError,
            traceId = context.TraceIdentifier
        });
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", (IWebHostEnvironment environment) =>
{
    return Results.Ok(new
    {
        app = "DevOps TP API",
        description = "API skeleton for a DevOps practical project",
        status = "running",
        environment = environment.EnvironmentName,
        version = appVersion,
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("GetApiInfo")
.WithTags("General");

app.MapHealthChecks("/health")
.WithName("HealthCheck")
.WithTags("Diagnostics");

app.MapGet("/ready", () =>
{
    return Results.Ok(new
    {
        status = "ready",
        message = "The API is ready to receive traffic",
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("ReadinessCheck")
.WithTags("Diagnostics");

app.MapGet("/version", () =>
{
    return Results.Ok(new
    {
        version = appVersion,
        commit = gitCommit,
        buildDate = buildDate
    });
})
.WithName("GetVersion")
.WithTags("General");

app.MapGet("/diagnostics/ping", () =>
{
    return Results.Ok(new
    {
        message = "pong",
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("Ping")
.WithTags("Diagnostics");

app.MapGet("/diagnostics/error", () =>
{
    throw new InvalidOperationException("Controlled error for monitoring validation");
})
.WithName("SimulateError")
.WithTags("Diagnostics");

app.MapGet("/diagnostics/slow", async () =>
{
    await Task.Delay(2000);

    return Results.Ok(new
    {
        message = "Slow response completed",
        delayMilliseconds = 2000,
        timestamp = DateTimeOffset.UtcNow
    });
})
.WithName("SimulateSlowRequest")
.WithTags("Diagnostics");

app.MapGet("/demo/zap-reflected-input", (string input) =>
{
    return Results.Content($"<html><body><h1>{input}</h1></body></html>", "text/html");
})
.WithName("DemoZapReflectedInput")
.WithTags("Security Demo");

app.MapGet("/demo/codeql-command-injection", (string command) =>
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "/bin/sh",
        Arguments = "-c " + command,
        RedirectStandardOutput = true
    });

    var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;

    return Results.Text(output, "text/plain");
})
.WithName("DemoCodeQlCommandInjection")
.WithTags("Security Demo");

app.MapGet("/demo/codeql-path-traversal", (string path) =>
{
    var content = File.ReadAllText(path);

    return Results.Text(content, "text/plain");
})
.WithName("DemoCodeQlPathTraversal")
.WithTags("Security Demo");

app.MapGet("/demo/codeql-insecure-random", () =>
{
    var token = new Random().Next(100000, 999999);

    return Results.Ok(new
    {
        resetToken = token
    });
})
.WithName("DemoCodeQlInsecureRandom")
.WithTags("Security Demo");

app.MapGet("/demo/codeql-weak-hash", (string input) =>
{
    var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));

    return Results.Ok(new
    {
        hash = Convert.ToHexString(hash)
    });
})
.WithName("DemoCodeQlWeakHash")
.WithTags("Security Demo");

app.MapQuestEndpoints();

app.Run();

public static class ApiVersion
{
    public const string Current = "1.0.0";
}

public partial class Program { }
