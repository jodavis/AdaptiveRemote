using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AdaptiveRemote.Contracts;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Minimal in-process HTTP stub that stands in for CompiledLayoutService during pipeline
/// integration tests. Accepts POST /layouts/compiled and echoes the submitted layout back
/// as if it were stored; this exercises the full LayoutProcessingService orchestration
/// pipeline without requiring a real CompiledLayoutService process.
///
/// Uses <see cref="HttpListener"/> so no additional SDK or packages are required.
/// </summary>
public sealed class StubCompiledLayoutService : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public string ServiceUrl { get; }

    public StubCompiledLayoutService()
    {
        int port = GetFreePort();
        // HttpListener requires a trailing slash on the prefix
        ServiceUrl = $"http://localhost:{port}";
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"{ServiceUrl}/");
        _listener.Start();

        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener!.GetContextAsync();
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            // Handle the request on a background thread to keep the loop responsive
            _ = Task.Run(() => HandleRequestAsync(context), ct);
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // POST /layouts/compiled — echo back the submitted CompiledLayout (simulates save)
            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && (request.Url?.AbsolutePath ?? string.Empty)
                    .Equals("/layouts/compiled", StringComparison.OrdinalIgnoreCase))
            {
                using StreamReader reader = new(request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync();

                CompiledLayout? layout = JsonSerializer.Deserialize(
                    body,
                    LayoutContractsJsonContext.Default.CompiledLayout);

                if (layout is null)
                {
                    response.StatusCode = 400;
                    response.Close();
                    return;
                }

                // Assign a new ID to simulate storage
                CompiledLayout stored = layout with { Id = Guid.NewGuid() };
                string responseJson = JsonSerializer.Serialize(
                    stored,
                    LayoutContractsJsonContext.Default.CompiledLayout);
                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);

                response.StatusCode = 201;
                response.ContentType = "application/json";
                response.ContentLength64 = responseBytes.Length;
                await response.OutputStream.WriteAsync(responseBytes);
            }
            else
            {
                response.StatusCode = 404;
            }

            response.Close();
        }
        catch
        {
            // Best effort — close the connection silently on error
            try
            {
                context.Response.Close();
            }
            catch
            {
                // Ignored
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // Ignored
        }

        try
        {
            _listener?.Close();
        }
        catch
        {
            // Ignored
        }

        _listenTask?.Wait(TimeSpan.FromSeconds(2));

        _cts?.Dispose();
        _listener?.Close();

        GC.SuppressFinalize(this);
    }

    private static int GetFreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
