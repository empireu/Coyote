using System.Numerics;
using GameFramework;
using GameFramework.Assets;
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
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);

            foreach (var layerIndex in _layerController.Indices)
            {
                var isSelected = layerIndex == _layerController.SelectedIndex;
                var style = _layerController[layerIndex] as ITabStyle;

                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, style?.SelectedColor ?? new Vector4(0.5f, 0.3f, 0.1f, 0.5f));
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, style?.IdleColor ?? new Vector4(0.1f, 0.7f, 0.8f, 0.5f));
                }

                if (ImGui.Button(_layerController[layerIndex].ToString()))
                {
                    _layerController.Select(layerIndex);
                }

                if (isSelected)
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.PopStyleVar();
        }

        ImGui.EndMainMenuBar();
    }

    public static EmbeddedResourceKey Asset(string name)
    {
        return new EmbeddedResourceKey(typeof(App).Assembly, $"Coyote.App.Assets.{name}");
    }
}