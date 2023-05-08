using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Coyote.App.Movement;
using Coyote.App.Nodes;
using GameFramework;
using GameFramework.Assets;
using GameFramework.Extensions;
using GameFramework.Gui;
using GameFramework.ImGui;
using GameFramework.Layers;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Renderer.Text;
using GameFramework.Scene;
using GameFramework.Utilities;
using GameFramework.Utilities.Extensions;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;
using Veldrid;

namespace Coyote.App;

internal class App : GameApplication
{
    private const string ProjectDirectory = "./projects/";
    private const string Extension = "awoo";
    private const string KeyBindFile = "keybinds.json";
    private static readonly Vector2 TabSize = new(12, 12);
    private readonly IServiceProvider _serviceProvider;

    private const float DefaultWeight = 0.465f;
    private const float DefaultSmoothing = 0.015f;
    private const float ToastWeight = 0.45f;
    private const float ToastSmoothing = 0.05f;

    public SdfFont Font { get; }

    public ToastManager ToastManager { get; }

    private readonly QuadBatch _toastBatch;
    private readonly QuadBatch _slideShowBatch;
    private readonly LayerController _layerController = new();

    private Project? _project;

    private readonly string[] _detectedFiles;

    private string _projectName = "";
    private int _selectedIndex;

    public Project Project => _project ?? throw new Exception("Tried to get project before it was loaded/created");

    private readonly InputManager _inputManager;

    private readonly OrthographicCameraController2D _fullCamera = new OrthographicCameraController2D(new OrthographicCamera(0, -1, 1));

    private readonly Sprite _wallpaper;

    private readonly List<RegisteredLayer> _tabLayers = new();

    private readonly Stopwatch _loadSw = new();

    private bool _showSettingsWindow;

    // Better to switch them like this instead of creating a copy:

    private void SetDefaultFont()
    {
        Font.Options.SetWeight(DefaultWeight);
        Font.Options.SetSmoothing(DefaultSmoothing);
    }

    private void SetToastFont()
    {
        Font.Options.SetWeight(ToastWeight);
        Font.Options.SetSmoothing(ToastSmoothing);
    }

    public App(IServiceProvider serviceProvider, NodeBehaviorRegistry behaviors)
    {
        Device.SyncToVerticalBlank = true;

        _serviceProvider = serviceProvider;

        ClearColor = RgbaFloat.Black;

        Window.Title = "Coyote";

        if (!Directory.Exists(ProjectDirectory))
        {
            Directory.CreateDirectory(ProjectDirectory);
        }

        _inputManager = new InputManager(this);
        _inputManager.Load(KeyBindFile);
        _inputManager.OnChanged += InputManagerOnOnChanged;

        _detectedFiles = Directory.GetFiles(ProjectDirectory, $"*{Extension}");

        Font = Resources.AssetManager.GetOrAddFont(Asset("Fonts.Roboto.font"));
        SetDefaultFont();

        ToastManager = new ToastManager(Font);
        _toastBatch = new QuadBatch(this);
        _slideShowBatch = new QuadBatch(this);

        _wallpaper = Resources.AssetManager.GetSpriteForTexture(Asset("Images.Slideshow0.png"));
        
        RegisterNodes(behaviors);

        RegisterTab("+T", "MotionEditorTab", () => Layers.ConstructLayer<MotionEditorLayer>());
        RegisterTab("+N", "NodeEditorTab", () => Layers.ConstructLayer<NodeEditorLayer>());

        ResizeCamera();
    }

    private void InputManagerOnOnChanged()
    {
        _inputManager.Save(KeyBindFile);
    }

    private void RegisterNodes(NodeBehaviorRegistry reg)
    {
        LeafNode Leaf(string name)
        {
            return reg.Register(new LeafNode(Resources.AssetManager.GetSpriteForTexture(Asset($"Images.Nodes.{name}.png")).Texture, name.AddSpacesToSentence(true)));
        }

        ProxyNode Proxy(string name)
        {
            return reg.Register(new ProxyNode(Resources.AssetManager.GetSpriteForTexture(Asset($"Images.Nodes.{name}.png")).Texture, name.AddSpacesToSentence(true)));
        }
        
        DecoratorNode Decorator(string name)
        {
            return reg.Register(new DecoratorNode(Resources.AssetManager.GetSpriteForTexture(Asset($"Images.Nodes.{name}.png")).Texture, name.AddSpacesToSentence(true)));
        }

        Proxy("Sequence");
        Proxy("Selector").Also(x => x.BackgroundColor = new Vector4(0.6f, 0.1f, 0.2f, 0.8f));

        Decorator("Success");

        reg.Register(new MotionNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.Motion.png")).Texture, "Motion"));
        reg.Register(new ParallelNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.Parallel.png")).Texture, "Parallel"));
        reg.Register(new CallNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.Call.png")).Texture, "Call"));
        reg.Register(new RepeatNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.Repeat.png")).Texture, "Repeat"));
        reg.Register(new ProxyNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.RepeatUntilFail.png")).Texture, "Repeat Until Fail")).Also(x => x.BackgroundColor *= new Vector4(1.4f, 0.6f, 0.6f, 1f));
        reg.Register(new ProxyNode(Resources.AssetManager.GetSpriteForTexture(Asset("Images.Nodes.RepeatUntilSuccess.png")).Texture, "Repeat Until Success")).Also(x => x.BackgroundColor *= new Vector4(0.9f, 1.1f, 0.9f, 1f));
    }

    private void RegisterTab(string label, string texture, Func<Layer> factory)
    {
        _tabLayers.Add(new RegisteredLayer(label, Resources.AssetManager.GetView(Asset($"Images.{texture}.png")), factory));
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
        Window.Title = "Coyote";

        Layers.ConstructLayer<ImGuiLayer>(imGui =>
        {
            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGuiStyles.Dark();

            ImGui.LoadIniSettingsFromDisk("imgui.ini");


            imGui.Submit += ImGuiOnSubmit;
        });
    }

    private string GetProjectFile(string name)
    {
        return $"{ProjectDirectory}{name}.{Extension}";
    }

    private void ImGuiOnSubmit(ImGuiRenderer obj)
    {
        if (!_loadSw.IsRunning)
        {
            _loadSw.Start();
            ToastInfo("Loading... Please wait!");
        }

        if (_loadSw.Elapsed.TotalSeconds >= ToastManagerOptions.DefaultEaseIn)
        {
            // pre-load
            // This is the lazy solution.

            ToastManager.StopWatch.Stop();
            Resources.AssetManager.GetSpriteForTexture(Asset("Images.PowerPlayField.jpg"));
            Resources.AssetManager.GetSpriteForTexture(Asset("Images.Robot.png"));
            Resources.AssetManager.GetSpriteForTexture(Asset("Images.Arrow.png"));
            ToastManager.StopWatch.Start();
        }

        var imGui = _serviceProvider.GetRequiredService<ImGuiLayer>();

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        if (_project == null)
        {
            SubmitProjectLoad();
            return;
        }

        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.Button("Settings"))
            {
                _showSettingsWindow = true;
            }

            if (ImGui.Button("Save All"))
            {
                _layerController.Layers.ForEach(l =>
                {
                    (l as IProjectTab)?.Save();
                });

                Project.Save();

                ToastInfo("Saved");
            }

            if (_layerController.Selected != null)
            {
                if (ImGui.Button("Close Tab"))
                {
                    if (_layerController.Selected is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    _layerController.Selected.IsEnabled = false;

                    Layers.RemoveLayer(_layerController.Selected);

                    _layerController.Remove(_layerController.Selected);

                    if (_layerController.Layers.Any())
                    {
                        _layerController.SwitchSelection(_layerController.Layers.Last());
                    }
                }
            }

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);

            var histogram = new Dictionary<string, int>();

            _layerController.Layers.ForEach(layer =>
            {
                var style = layer as ITabStyle;

                if (layer.IsEnabled)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, style?.SelectedColor ?? new Vector4(0.5f, 0.3f, 0.1f, 0.5f));
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, style?.IdleColor ?? new Vector4(0.1f, 0.7f, 0.8f, 0.5f));
                }

                var layerName = layer.ToString()!;
                histogram.AddOrUpdate(layerName, _ => 1, i => histogram[i] + 1);

                if (ImGui.Button($"{histogram[layerName]} {layerName}"))
                {
                    _layerController.SwitchSelection(layer);
                }

                ImGui.PopStyleColor();
            });

            ImGui.PopStyleVar();

            var addLayers = new List<Layer>();

            for (var i = 0; i < _tabLayers.Count; i++)
            {
                var tab = _tabLayers[i];
               
                if (ImGui.ImageButton($"{i} {tab.Label}", imGui.Renderer.GetOrCreateImGuiBinding(Resources.Factory, tab.TabView), TabSize))
                {
                    addLayers.Add(tab.Factory());
                }
            }

            addLayers.RemoveAll(l =>
            {
                _layerController.Add(l);

                if (_layerController.Selected == null || l == addLayers.Last())
                {
                    _layerController.SwitchSelection(l);
                }
                else
                {
                    if (l.IsEnabled)
                    {
                        l.Disable();
                    }
                }
            });
        }

        ImGui.EndMainMenuBar();

        if (_showSettingsWindow)
        {
            if (ImGui.Begin("Settings", ref _showSettingsWindow))
            {
                ImGui.Text("Keys");

                _inputManager.ImGuiSubmit();
            }

            ImGui.End();
        }
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
        const string imId = "App";

        if (ImGuiExt.Begin("Project Manager", imId))
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
                                CreateProject(name);
                                ToastInfo("Created Project");
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
                        }
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void CreateProject(string name)
    {
        Assert.IsTrue(_project == null);

        _project = Project
            .CreateEmpty(name)
            .Also(p => p.SetChanged());
    }

    public static EmbeddedResourceKey Asset(string name)
    {
        return new EmbeddedResourceKey(typeof(App).Assembly, $"Coyote.App.Assets.{name}");
    }

    protected override void Render(FrameInfo frameInfo)
    {
        _slideShowBatch.Clear();
        _slideShowBatch.Effects = QuadBatchEffects.Transformed(_fullCamera.Camera.CameraMatrix);
        _slideShowBatch.TexturedQuad(Vector2.Zero, Vector2.Normalize(new Vector2(3840, 1920)) * 3, _wallpaper.Texture);
        _slideShowBatch.Submit();

        base.Render(frameInfo);
    }

    protected override void AfterRender(FrameInfo frameInfo)
    {
        SetToastFont();

        _toastBatch.Clear();
        _toastBatch.Effects = QuadBatchEffects.Transformed(_fullCamera.Camera.CameraMatrix);

        ToastManager.Render(_toastBatch, 0.05f, -Vector2.UnitY * 0.35f, 0.925f);

        _toastBatch.Submit();

        SetDefaultFont();

        base.AfterRender(frameInfo);
    }

    private sealed class RegisteredLayer
    {
        public string Label { get; }
        public TextureView TabView { get; }
        public Func<Layer> Factory { get; }

        public RegisteredLayer(string label, TextureView tabView, Func<Layer> factory)
        {
            Label = label;
            TabView = tabView;
            Factory = factory;
        }
    }
}