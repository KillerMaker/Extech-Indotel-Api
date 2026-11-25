using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Exatech_Indotel_API.Utilities.Extensions;

public static class TelemetryExtensions
{
    public const string SourceName = "Exatech.Indotel.Api";
    public static readonly ActivitySource Activities = new(SourceName);

    public static async Task<T> RunStage<T>(string stage, Func<Task<T>> action, ILogger logger)
    {
        using var activity = Activities.StartActivity(stage);
        var timer = Stopwatch.StartNew();
        try
        {
            var result = await action();
            logger.LogInformation("Stage {Stage} completed in {ElapsedMs} ms", stage, timer.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", exception.GetType().FullName);
            logger.LogError("Stage {Stage} failed in {ElapsedMs} ms with {ExceptionType}",
                stage, timer.Elapsed.TotalMilliseconds, exception.GetType().FullName);
            throw;
        }
    }

    public static Task RunStage(string stage, Func<Task> action, ILogger logger) =>
        RunStage(stage, async () => { await action(); return true; }, logger);

    public static WebApplicationBuilder AddApiTelemetry(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "exatech-indotel-api";
        var resource = ResourceBuilder.CreateDefault().AddService(serviceName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString())
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)]);
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var exporting = !string.IsNullOrWhiteSpace(endpoint);
        var protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ?? "http/protobuf";
        if (protocol is not ("http/protobuf" or "grpc"))
            throw new InvalidOperationException("OTEL_EXPORTER_OTLP_PROTOCOL must be http/protobuf or grpc.");
        if (exporting && (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
            throw new InvalidOperationException("OTEL_EXPORTER_OTLP_ENDPOINT must be an HTTP(S) base URL without credentials, query or fragment.");

        void Export(OtlpExporterOptions options, string signal)
        {
            options.Protocol = protocol == "grpc" ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
            options.Endpoint = new Uri(protocol == "grpc" ? endpoint! : endpoint!.TrimEnd('/') + "/v1/" + signal);
            options.Headers = configuration["OTEL_EXPORTER_OTLP_HEADERS"];
            options.TimeoutMilliseconds = 5000;
        }

        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        // HttpClient's default text logs include URL query values such as cedulas.
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
            if (exporting) options.AddOtlpExporter(exporter => Export(exporter, "logs"));
        });
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resource).AddSource(SourceName)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => context.Request.Path != "/health")
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new PrivateUrlProcessor());
                if (exporting) tracing.AddOtlpExporter(options => Export(options, "traces"));
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resource).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
                    .AddMeter("System.Runtime");
                if (exporting) metrics.AddOtlpExporter(options => Export(options, "metrics"));
            });
        return builder;
    }
}

// Dependency URLs may put client identifiers in either the path or the query.
public sealed class PrivateUrlProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var key in new[] { "url.full", "http.url" })
        {
            if (Uri.TryCreate(activity.GetTagItem(key)?.ToString(), UriKind.Absolute, out var uri))
                activity.SetTag(key, uri.GetLeftPart(UriPartial.Authority) + "/");
        }
        activity.SetTag("url.query", null);
        activity.SetTag("url.path", null);
        activity.SetTag("http.target", null);
        // Keep stack/type information without exporting exception text containing client input.
        activity.SetTag("exception.message", null);
    }
}
