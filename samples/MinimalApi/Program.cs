using MinimalApi;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XPoweredBy = "MinimalApi-Sample";
    await next();
});

app.MapGet("/", () => "Hello from MinimalApi!");

app.MapGet("/hello", (string? name) =>
    new GreetingResponse($"Hello, {name ?? "world"}!"));

app.MapPost("/echo", (EchoRequest request) =>
    Results.Ok(new EchoResponse(request.Message, DateTime.UtcNow)));

app.Run();

namespace MinimalApi
{
    /// <summary>
    /// Response containing a greeting message.
    /// </summary>
    /// <param name="Message">The greeting text.</param>
    public record GreetingResponse(string Message);

    /// <summary>
    /// Request containing a message to echo back.
    /// </summary>
    /// <param name="Message">The message to echo.</param>
    public record EchoRequest(string Message);

    /// <summary>
    /// Response containing the echoed message and a timestamp.
    /// </summary>
    /// <param name="Echo">The echoed message text.</param>
    /// <param name="ProcessedAt">The UTC time the message was processed.</param>
    public record EchoResponse(string Echo, DateTime ProcessedAt);
}
