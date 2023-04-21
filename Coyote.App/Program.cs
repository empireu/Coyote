using Coyote.App;
using Coyote.App.Movement;
using GameFramework;
using GameFramework.ImGui;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(LogEventLevel.Verbose)
    .Enrich.FromLogContext()
    .WriteTo.File("logs/logs.txt", LogEventLevel.Debug, rollingInterval: RollingInterval.Hour)
    .WriteTo.Console()
    .CreateLogger();

using var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(Directory.GetCurrentDirectory())
    .UseSerilog()
    .UseConsoleLifetime()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ImGuiLayer>();
        services.AddSingleton<App>();
        services.AddSingleton<GameApplication>(s => s.GetRequiredService<App>());

        services.AddTransient<MotionEditorLayer>();
        services.AddSingleton<TestLayer>();
    })
    .Build();

var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

host.StartAsync();

// Run graphics on the main thread:
host.Services.GetRequiredService<App>().Run();

// The application was closed:
lifetime.StopApplication();
host.WaitForShutdown();