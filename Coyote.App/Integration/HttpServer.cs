using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coyote.App.Integration;

internal readonly struct HttpRequest
{
    public string Path { get; }

    public HttpRequestMethod Method { get; }

    public HttpRequest(string path, HttpRequestMethod method)
    {
        Path = path;
        Method = method;
    }

    public static HttpRequest? Parse(ReadOnlySpan<char> data)
    {
        if (data.Length < 14) 
        {
            return null; // it cannot be smaller than that.
        }

        HttpRequestMethod method;

        if (data.StartsWith("GET"))
        {
            method = HttpRequestMethod.GET;
        }
        else if (data.StartsWith("HEAD"))
        {
            method = HttpRequestMethod.HEAD;
        }

        else return null; // unsupported method

        // remove "HEAD " or "GET "
        data = data[(method == HttpRequestMethod.HEAD ? 5 : 4)..];

        var index = data.IndexOf(' ');
        if (index == -1)
        {
            return null; // no version specified, invalid request.
        }

        // select path
        data = data[..index];

        return new HttpRequest(new string(data), method);
    }
}

internal enum HttpRequestMethod
{
    GET,
    HEAD
}

internal readonly struct HttpContext
{
    public HttpRequest Request { get; }

    public Stream Transport { get; }

    public EndPoint EndPoint { get; }

    public HttpContext(HttpRequest request, Stream transport, EndPoint endPoint)
    {
        Request = request;
        Transport = transport;
        EndPoint = endPoint;
    }
}

/// <summary>
///     Minimal HTTP Server. Better then <see cref="HttpListener"/> (no administrative rights needed) and doesn't depend on ASP.
/// </summary>
internal sealed class HttpServer
{
    public delegate Task HandlerDelegate(HttpContext context, CancellationToken token);

    private readonly CancellationTokenSource _cts = new();

    private readonly TcpListener _listener;
    private readonly ILogger<HttpServer> _logger;
    private readonly HandlerDelegate _handler;

    private Task? _listenTask;

    [ActivatorUtilitiesConstructor]
    public HttpServer(ILogger<HttpServer> logger, IPEndPoint hostEp, HandlerDelegate handler)
    {
        _listener = new TcpListener(hostEp);
        _logger = logger;
        _handler = handler;
    }

    public void Start()
    {
        _listener.Start();
        _listenTask = AcceptAsync();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();

        if (_listenTask != null)
        {
            await _listenTask;
        }
    }

    private async Task AcceptAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);

            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to accept client {e}", ex);
                return;
            }

            var ep = client.Client.RemoteEndPoint;
           
            using (client)
            {
                try
                {
                    await using var ns = client.GetStream();

                    var data = await ns.ReadLineSized(4096, _cts.Token);

                    if (data == null)
                    {
                        _logger.LogError("Invalid connection from {c}", client.Client.RemoteEndPoint);

                        continue;
                    }

                    var request = HttpRequest.Parse(data);

                    if (request == null)
                    {
                        _logger.LogError("Invalid request from {c}", client.Client.RemoteEndPoint);

                        continue;
                    }

                    await _handler.Invoke(new HttpContext(request.Value, ns, ep), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to handle client {c}", ep);
                }
            }
        }
    }

}