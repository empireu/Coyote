using System.Drawing;
using System.Numerics;
using Coyote.App.Movement;
using GameFramework;
using GameFramework.Assets;
using GameFramework.Extensions;
using GameFramework.ImGui;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Renderer.Text;
using GameFramework.Scene;
using GameFramework.Utilities;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Veldrid;

namespace Coyote.App;
internal class App : GameApplication
{
    private const string ProjectDirectory = "./projects/";
    private const string Extension = "awoo";

    private readonly IServiceProvider _serviceProvider;
    
    public SdfFont Font { get; }
    public ToastManager ToastManager { get; }

    private readonly QuadBatch _toastBatch;
    private readonly QuadBatch _slideshowBatch;

    private LayerController? _layerController;

    private Project? _project;

    private readonly string[] _detectedFiles;

    private string _projectName = "";
    private int _selectedIndex;

    public Project Project => _project ?? throw new Exception("Tried to get project before it was loaded/created");

    private readonly OrthographicCameraController2D _fullCamera = new OrthographicCameraController2D(new OrthographicCamera(0, -1, 1));

    private readonly Sprite _wallpaper;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        ClearColor = RgbaFloat.Black;

        Window.Title = "Coyote";

        if (!Directory.Exists(ProjectDirectory))
        {
            Directory.CreateDirectory(ProjectDirectory);
        }

        _detectedFiles = Directory.GetFiles(ProjectDirectory, $"*{Extension}");

        Font = Resources.AssetManager.GetOrAddFont(Asset("Fonts.Roboto.font"));
        Font.Options.SetWeight(0.46f);

        ToastManager = new ToastManager(this);
        _toastBatch = new QuadBatch(this);
        _slideshowBatch = new QuadBatch(this);

        _wallpaper = Resources.AssetManager.GetSpriteForTexture(Asset("Images.Slideshow0.png"));

        ResizeCamera();
    }

    private void ResizeCamera()
    {
        _fullCamera.Camera.AspectRatio = Window.Width / (float)Window.Height;
    }

    protected override void Resize(Size size)
    {
        ResizeCamera();

        base.Resize(size);
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
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGuiStyles.Dark();

            ImGui.LoadIniSettingsFromDisk("imgui.ini");

            imGui.Submit += ImGuiOnSubmit;
        });

        _layerController = new LayerController(
            Layers.ConstructLayer<MotionEditorLayer>(),
            Layers.ConstructLayer<TestLayer>()
        );

        _layerController.Selected.Disable();
    }

    private string GetProjectFile(string name)
    {
        return $"{ProjectDirectory}{name}.{Extension}";
    }

    private void ImGuiOnSubmit(ImGuiRenderer obj)
    {
        Assert.NotNull(ref _layerController);

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        if (_project == null)
        {
            SubmitProjectLoad();
            return;
        }

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

                ImGui.PopStyleColor();
            }

            ImGui.PopStyleVar();

            if(ImGui.Button("Save"))
            {
                Project.Save();
            }
        }

        ImGui.EndMainMenuBar();
    }

    public void ToastInfo(string message)
    {
        ToastManager.Add(new ToastNotification(ToastNotificationType.Information, message));
    }

    public void ToastError(string message)
    {
        ToastManager.Add(new ToastNotification(ToastNotificationType.Error, message));
    }

    private void SubmitProjectLoad()
    {
        if (ImGui.Begin("Project Manager"))
        {
            if(ImGui.BeginTabBar("Create or Load"))
            {
                if (ImGui.BeginTabItem("Create"))
                {
                    ImGui.InputText("Name", ref _projectName, 100);

                    if (ImGui.Button("OK"))
                    {
                        if (!string.IsNullOrEmpty(_projectName))
                        {
                            var name = GetProjectFile(_projectName);

                            if (!File.Exists(name))
                            {
                                _project = Project.Create(name);

                                ToastInfo("Created Project");

                                _layerController!.Selected.Enable();
                            }
                        }
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Load"))
                {
                    ImGui.Combo("Projects", ref _selectedIndex, _detectedFiles, _detectedFiles.Length);

                    if (ImGui.Button("OK"))
                    {
                        if (_selectedIndex >= 0 && _selectedIndex < _detectedFiles.Length)
                        {
                            _project = Project.Load(_detectedFiles[_selectedIndex]);

                            ToastInfo("Loaded Project");

                            _layerController!.Selected.Enable();
                        }
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    public static EmbeddedResourceKey Asset(string name)
    {
        return new EmbeddedResourceKey(typeof(App).Assembly, $"Coyote.App.Assets.{name}");
    }

    protected override void Render(FrameInfo frameInfo)
    {
        _slideshowBatch.Clear();
        _slideshowBatch.Effects = QuadBatchEffects.Transformed(_fullCamera.Camera.CameraMatrix);
        _slideshowBatch.TexturedQuad(Vector2.Zero, Vector2.Normalize(new Vector2(3840, 1920)) * 3, _wallpaper.Texture);
        _slideshowBatch.Submit();

        base.Render(frameInfo);
    }

    protected override void AfterRender(FrameInfo frameInfo)
    {
        _toastBatch.Clear();
        _toastBatch.Effects = QuadBatchEffects.Transformed(_fullCamera.Camera.CameraMatrix);

        ToastManager.Render(_toastBatch, 0.05f, -Vector2.UnitY * 0.35f, 0.90f);

        _toastBatch.Submit();

        base.AfterRender(frameInfo);
    }
}