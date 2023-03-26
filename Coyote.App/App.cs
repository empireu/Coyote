using GameFramework;
using GameFramework.ImGui;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Veldrid;

namespace Coyote.App;
internal class App : GameApplication
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        ClearColor = RgbaFloat.Black;

        Window.Title = "Coyote";
    }

    protected override IServiceProvider BuildLayerServiceProvider(ServiceCollection registeredServices)
    {
        return _serviceProvider;
    }

    protected override void Initialize()
    {
        Window.Title = "DashViu";

        Layers.ConstructLayer<ImGuiLayer>(imGui =>
        {
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGuiStyles.Dark();
        });

        Layers.ConstructLayer<MainLayer>();
    }
}