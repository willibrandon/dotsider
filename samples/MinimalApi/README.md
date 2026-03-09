# MinimalApi

ASP.NET Minimal APIs application. Used to test dotsider's analysis of Web SDK assemblies, which have a significantly larger dependency graph and different PE characteristics than standard libraries.

- Custom middleware, route endpoints (`/`, `/hello`, `/echo`)
- Public record types: `GreetingResponse`, `EchoRequest`, `EchoResponse`
- Web SDK project structure (different from standard `Microsoft.NET.Sdk`)
