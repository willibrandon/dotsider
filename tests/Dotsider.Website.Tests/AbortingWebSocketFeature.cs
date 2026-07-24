using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net.WebSockets;

namespace Dotsider.Website.Tests;

internal sealed class AbortingWebSocketFeature : IHttpWebSocketFeature
{
    internal static AbortingWebSocketFeature Instance { get; } = new();

    private AbortingWebSocketFeature()
    {
    }

    bool IHttpWebSocketFeature.IsWebSocketRequest => true;

    Task<WebSocket> IHttpWebSocketFeature.AcceptAsync(WebSocketAcceptContext context)
    {
        return Task.FromException<WebSocket>(
            new ConnectionAbortedException("The client disconnected during the WebSocket handshake."));
    }
}
