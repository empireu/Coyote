using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coyote.App.Integration;

/// <summary>
///     HTTP Server for wireless Coyote downloads.
/// </summary>
internal sealed class RobotServer : IHostedService
{
    public const int Port = 3141;
    public static readonly string[] EndPoints = new[] { "/coyote", "/coyote/" };

    private readonly ILogger<RobotServer> _logger;
    private readonly App _application;
    private readonly HttpServer _server;

    public RobotServer(IServiceProvider sp, ILogger<RobotServer> logger, App application)
    {
        _logger = logger;
        _application = application;
        _server = ActivatorUtilities.CreateInstance<HttpServer>(sp, new IPEndPoint(IPAddress.Any, Port), new HttpServer.HandlerDelegate(HandleAsync));
    }

    private async Task HandleAsync(HttpContext context, CancellationToken token)
    {
        if (!EndPoints.Contains(context.Request.Path))
        {
            _logger.LogError("Invalid request on {path} from {ep}", context.Request.Path, context.EndPoint);
            
            return;
        }

        if (context.Request.Method != HttpRequestMethod.GET)
        {
            _logger.LogError("Invalid request method {m} from {ep}", context.Request.Method, context.EndPoint);
            
            return;
        }

        var project = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_application.Project));
        
        var stream = context.Transport;

        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(
                $"HTTP/1.1 200 OK\r\n" +
                  $"Content-Length: {project.Length}\r\n" +
                  $"Content-Type: application/json\r\n" +
                  $"Accept-Ranges: none\r\n" +
                  $"Server: Coyote\r\n\r\n"
                ), 
            token
        );

        await stream.WriteAsync(project, token);

        _logger.LogInformation("Served project to {ep}!", context.EndPoint);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting server...");

        try
        {
            _server.Start();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to start server: {e}", e);
        }
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down server...");

        try
        {
            await _server.StopAsync();
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to stop server: {e}", e);
        }
    }
}