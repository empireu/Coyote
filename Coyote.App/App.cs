using GameFramework;
using GameFramework.ImGui;
using GameFramework.Utilities;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Veldrid;

namespace Coyote.App;
internal class App : GameApplication
{
    private readonly IServiceProvider _serviceProvider;

    private LayerController? _layerController;

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

            imGui.Submit += ImGuiOnSubmit;
        });

        _layerController = new LayerController(
            Layers.ConstructLayer<MotionEditorLayer>(),
            Layers.ConstructLayer<TestLayer>()
        );
    }

    private void ImGuiOnSubmit(ImGuiRenderer obj)
    {
        Assert.NotNull(ref _layerController);

        ImGui.ShowDemoWindow();

        if (ImGui.BeginMainMenuBar())
        {
            foreach (var layerIndex in _layerController.Indices)
            {
                if (ImGui.Button(_layerController[layerIndex].ToString()))
                {
                    _layerController.Select(layerIndex);
                }
            }
        }

        ImGui.EndMainMenuBar();
    }
}