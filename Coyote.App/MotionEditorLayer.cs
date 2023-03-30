using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.ImGui;
using GameFramework.Layers;
using GameFramework.PostProcessing;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Scene;
using GameFramework.Utilities;
using GameFramework.Utilities.Extensions;
using ImGuiNET;
using Veldrid;
using Point = System.Drawing.Point;

namespace Coyote.App;

internal class MotionEditorLayer : Layer, ITabStyle
{
    private enum ToolType
    {
        TranslateAdd,
        TranslateDelete,
        RotateAdd,
        RotateRemove
    }

    private static readonly Dictionary<ToolType, string> ToolDescriptions = new()
    {
        { ToolType.TranslateAdd , "Add Translation Points" },
        { ToolType.TranslateDelete, "Delete Translation Points" },
        { ToolType.RotateAdd, "Add Rotation Points" },
        { ToolType.RotateRemove, "Delete Rotation Points"}
    };

    public const float FieldSize = 3.66f;

    private const float MoveSpeed = 2f;
    private const float ZoomSpeed = 25;
    private const float MinZoom = 1f;
    private const float MaxZoom = 5f;

    private readonly App _app;
    private readonly ImGuiLayer _imGuiLayer;
    
    private ImGuiRenderer ImGuiRenderer => _imGuiLayer.Renderer;

    private readonly QuadBatch _editorBatch;
    private readonly QuadBatch _playerBatch;

    // Used by the editor (translation and rotation)"
    private readonly PostProcessor _editorProcessor;

    // Used by the player window:
    private Texture? _playerTexture;
    private Framebuffer? _playerFramebuffer;
    private nint _playerBinding;
    private Point _playerSize = new(-1, -1);
    private bool _showPlayer;

    private readonly CommandList _commandList;

    private readonly Sprite _fieldSprite;
    private readonly Sprite _robotSprite;

    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _world;
    private readonly PathEditor _path;

    private ToolType _selectedTool = ToolType.TranslateAdd;

    private Entity? _selectedEntity;

    private float _playTime;

    private int _pickIndex;

    private string _motionProjectName = "My Project";
    private int _selectedProject;

    public MotionEditorLayer(App app, ImGuiLayer imGuiLayer)
    {
        _app = app;

        _imGuiLayer = imGuiLayer;

        imGuiLayer.Submit += ImGuiLayerOnSubmit;

        _cameraController = new OrthographicCameraController2D(
            new OrthographicCamera(0, -1, 1),
            translationInterpolate: 15f,
            zoomInterpolate: 10);

        _cameraController.FutureZoom = FieldSize;

        _editorBatch = new QuadBatch(app);
        _playerBatch = new QuadBatch(app)
        {
            Effects = QuadBatchEffects.None with
            {
                Transform = Matrix4x4.CreateOrthographic(
                    FieldSize,
                    FieldSize,
                    -1,
                    1)
            }
        };

        _editorProcessor = new PostProcessor(app);
        _editorProcessor.BackgroundColor = new RgbaFloat(25f / 255f, 25f / 255f, 25f / 255f, 1f);

        _commandList = app.Device.ResourceFactory.CreateCommandList();

        _fieldSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PowerPlayField.jpg"));
        _robotSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Robot.png"));

        _world = World.Create();
        _path = new PathEditor(app, _world);

        UpdateEditorPipeline();
    }

    protected override void OnAdded()
    {
        RegisterHandler<MouseEvent>(OnMouseEvent);
    }

    private Vector2 MouseWorld => _cameraController.Camera.MouseToWorld2D(
        _app.Input.MousePosition,
        _app.Window.Width, 
        _app.Window.Height);

    private Vector2 _selectPoint;

    private void SelectEntity()
    {
        var entities = _world.Clip(MouseWorld);

        if (entities.Count == 0)
        {
            _selectedEntity = null;
        }
        else
        {
            if (_pickIndex >= entities.Count)
            {
                _pickIndex = 0;
            }

            _selectedEntity = entities[_pickIndex++];

            var entityPosition = _world.Get<PositionComponent>(_selectedEntity.Value).Position;

            _selectPoint = entityPosition - MouseWorld;
        }
    }

    private bool OnMouseEvent(MouseEvent @event)
    {
        if (@event.MouseButton == MouseButton.Left)
        {
            if (@event.Down)
            {
                SelectEntity();
            }
        }
        else
        {
            HandleToolMouseEvent(@event);
        }


        return true;
    }

    private void HandleToolMouseEvent(MouseEvent @event)
    {
        _selectedEntity = null;

        if (!@event.Down)
        {
            return;
        }

        switch (_selectedTool)
        {
            case ToolType.TranslateAdd:
                _path.CreateTranslationPoint(MouseWorld);
                break;
            case ToolType.TranslateDelete:
            {
                SelectEntity();

                if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive() && _path.IsTranslationPoint(_selectedEntity.Value))
                {
                    _path.DestroyTranslationPoint(_selectedEntity.Value);
                }

                _selectedEntity = null;
                break;
            }
            case ToolType.RotateAdd:
                _path.CreateRotationPoint(MouseWorld);
                break;
            case ToolType.RotateRemove:
            {
                SelectEntity();

                if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive() && _path.IsRotationPoint(_selectedEntity.Value))
                {
                    _path.DestroyRotationPoint(_selectedEntity.Value);
                }

                _selectedEntity = null;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ImGuiLayerOnSubmit(ImGuiRenderer obj)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (ImGui.Begin("Tools"))
        {
            ImGui.TextColored(new Vector4(1, 1, 1, 1), "Path Tools");
            ImGui.BeginGroup();

            foreach (var value in Enum.GetValues<ToolType>())
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _selectedTool == value ? new Vector4(1, 0, 0, 0.5f) : new Vector4(0, 1, 0, 0.3f));

                if (ImGui.Button(ToolDescriptions[value]))
                {
                    _selectedTool = value;

                    ImGui.PopStyleColor();

                    break;
                }

                ImGui.PopStyleColor();

                ImGui.Separator();
            }

            ImGui.EndGroup();

            ImGui.TextColored(new Vector4(1, 1, 1, 1), "Review");
            ImGui.BeginGroup();
            
            if (ImGui.Button("Player"))
            {
                _showPlayer = true;
            }
            
            ImGui.EndGroup();
        }

        ImGui.End();

        if (ImGui.Begin("Project"))
        {
            if (ImGui.BeginTabBar("Motion Projects"))
            {
                if (ImGui.BeginTabItem("Save"))
                {
                    ImGui.InputText("Name", ref _motionProjectName, 100);

                    if (ImGui.Button("OK"))
                    {
                        if (!string.IsNullOrEmpty(_motionProjectName))
                        {
                            var overwrote = _app.Project.MotionProjects.ContainsKey(_motionProjectName);

                            SaveProject();

                            _app.ToastInfo($"{(overwrote ? "Updated" : "Created")} project {_motionProjectName}");
                        }
                        else
                        {
                            _app.ToastError("Invalid project name!");
                        }
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Open"))
                {
                    var items = _app.Project.MotionProjects.Keys.ToArray();

                    ImGui.Combo("Projects", ref _selectedProject, items, items.Length);

                    if (ImGui.Button("OK") && _selectedProject >= 0 && _selectedProject < items.Length)
                    {
                        _motionProjectName = items[_selectedProject];
                        LoadProject(_motionProjectName);

                        _app.ToastInfo($"Loaded project {_motionProjectName}");
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.End();

        if (ImGui.Begin("Inspector"))
        {

        }

        ImGui.End();

        if (_showPlayer)
        {
            if (ImGui.Begin("Player", ref _showPlayer))
            {
                var wndSize = ImGui.GetWindowSize();

                var min = new Vector2(Math.Min(wndSize.X, wndSize.Y));

                var imageSize = (min * 0.95f).ToPoint();

                if (imageSize != _playerSize)
                {
                    _playerSize = imageSize;
                    UpdatePlayerPipeline();
                }

                RenderPlayer();

                // Fixes inverted Y coordinate by inverting Y in the texture coordinate.
                ImGui.Image(_playerBinding, imageSize.ToVector2(), Vector2.Zero, new Vector2(1f, -1f));
            }

            ImGui.End();
        }
    }

    private void SaveProject()
    {
        var motionProject = MotionProject.FromPath(_path);

        _app.Project.MotionProjects[_motionProjectName] = motionProject;

        _app.Project.Save();
    }

    private void LoadProject(string motionProjectName)
    {
        var motionProject = _app.Project.MotionProjects[motionProjectName];

        _world.Clear();

        motionProject.Load(_path);
    }

    protected override void Resize(Size size)
    {
        UpdateEditorPipeline();
    }

    private void UpdateEditorPipeline()
    {
        _cameraController.Camera.AspectRatio = _app.Window.Width / (float)_app.Window.Height;

        _editorProcessor.ResizeInputs(_app.Window.Size() * 2);
        _editorProcessor.SetOutput(_app.Device.SwapchainFramebuffer);
        _editorBatch.UpdatePipelines(outputDescription: _editorProcessor.InputFramebuffer.OutputDescription);
    }

    private void UpdatePlayerPipeline()
    {
        if (_playerTexture != null)
        {
            ImGuiRenderer.RemoveImGuiBinding(_playerTexture);
            _playerTexture.Dispose();
        }

        _playerTexture = _app.Device.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            (uint)(_playerSize.X * 2),
            (uint)(_playerSize.Y * 2),
            1,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled
        ));

        _playerFramebuffer?.Dispose();
        _playerFramebuffer = _app.Device.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(null, colorTargets: new[]
            {
                new FramebufferAttachmentDescription(_playerTexture!, 0)
            }));

        _playerBatch.UpdatePipelines(outputDescription: _playerFramebuffer!.OutputDescription);

        _playerBinding = ImGuiRenderer.GetOrCreateImGuiBinding(_app.Resources.Factory, _playerTexture!);
    }

    private void RenderPlayer()
    {
        var framebuffer = Assert.NotNull(_playerFramebuffer);

        _commandList.Begin();
        _commandList.SetFramebuffer(framebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Clear);
        _commandList.End();
        _app.Device.SubmitCommands(_commandList);

        _playerBatch.Clear();
        _playerBatch.TexturedQuad(Vector2.Zero, new Vector2(FieldSize, FieldSize), _fieldSprite.Texture);
        _playerBatch.Submit(framebuffer: framebuffer); _playerBatch.Submit(framebuffer: framebuffer);

        _playerBatch.Clear();
        _path.DrawTranslationPath(_playerBatch);
        _playerBatch.Submit(framebuffer: framebuffer);

        _playerBatch.Clear();

        const float speed = 2.5f;

        if (_path.ArcLength > 0)
        {
            var t = (_playTime / (_path.ArcLength / speed));

            if (t > 1)
            {
                _playTime = 0;
            }
            else
            {
                var translation = _path.TranslationSpline.Evaluate(t);

                var pose = _path.RotationSpline.IsEmpty 
                    ? new Pose(translation, _path.TranslationSpline.EvaluateDerivative1(t)) // Spline Tangent Heading
                    : new Pose(translation, (float)_path.RotationSpline.Evaluate(t)); // Spline Spline Heading

                pose -= MathF.PI / 2f;

                _playerBatch.TexturedQuad(pose.Translation, Vector2.One * 0.4f, pose.Rotation, _robotSprite.Texture);
            }
        }

        _playerBatch.Submit(framebuffer: framebuffer);
    }

    private void UpdateCamera(FrameInfo frameInfo)
    {
        foreach (var inputDownKey in _app.Input.DownKeys)
        {
            _cameraController.ProcessKey(inputDownKey,
                MoveSpeed * frameInfo.DeltaTime,
                0);
        }

        _cameraController.FutureZoom += _app.Input.ScrollDelta * ZoomSpeed * frameInfo.DeltaTime;
        _cameraController.FutureZoom = Math.Clamp(_cameraController.FutureZoom, MinZoom, MaxZoom);

        _cameraController.Update(frameInfo.DeltaTime);
    }

    private void UpdateSelection(FrameInfo frameInfo)
    {
        if (_imGuiLayer.Captured)
        {
            return;
        }

        if (_selectedEntity == null || !_app.Input.IsMouseDown(MouseButton.Left))
        {
            return;
        }

        var entity = _selectedEntity.Value;

        entity.Move(MouseWorld + _selectPoint);
    }

    protected override void Update(FrameInfo frameInfo)
    {
        _playTime += frameInfo.DeltaTime;

        UpdateCamera(frameInfo);
        UpdateSelection(frameInfo);
    }

    private void RenderEditor()
    {
        void Draw()
        {
            _editorBatch.Submit(framebuffer: _editorProcessor.InputFramebuffer);
            _editorBatch.Clear();
        }

        _editorBatch.Clear();

        if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive())
        {
            var selected = _selectedEntity.Value;
            var rectangle = selected.GetRectangle();

            _editorBatch.Quad(
                new Vector2(rectangle.CenterX(), rectangle.CenterY()),
                Vector2.One * new Vector2(rectangle.Width, rectangle.Height), 
                new RgbaFloat4(0, 1, 0, 0.5f));
        }

        Systems.RenderSprites(_world, _editorBatch);
        Draw();
        _path.DrawTranslationPath(_editorBatch);
        Draw();
        _path.DrawIndicator(_editorBatch, MouseWorld);
        Draw();
        Systems.RenderConnections(_world, _editorBatch);
        Draw();
    }

    protected override void Render(FrameInfo frameInfo)
    {
        _editorBatch.Effects = QuadBatchEffects.Transformed(_cameraController.Camera.CameraMatrix);

        _editorProcessor.ClearColor();

        _editorBatch.Clear();
        _editorBatch.TexturedQuad(Vector2.Zero, Vector2.One * FieldSize, _fieldSprite.Texture);
        _editorBatch.Submit(framebuffer: _editorProcessor.InputFramebuffer);

        RenderEditor();

        _editorProcessor.Render();
    }
}