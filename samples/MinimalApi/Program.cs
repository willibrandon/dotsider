var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Powered-By"] = "MinimalApi-Sample";
    await next();
});

app.MapGet("/", () => "Hello from MinimalApi!");

app.MapGet("/hello", (string? name) =>
    new GreetingResponse($"Hello, {name ?? "world"}!"));

app.MapPost("/echo", (EchoRequest request) =>
    Results.Ok(new EchoResponse(request.Message, DateTime.UtcNow)));

app.Run();

public record GreetingResponse(string Message);
public record EchoRequest(string Message);
public record EchoResponse(string Echo, DateTime ProcessedAt);
