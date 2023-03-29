using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameFramework;
using GameFramework.Assets;
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
    }

    private static readonly Dictionary<ToolType, string> ToolDescriptions = new()
    {
        { ToolType.TranslateAdd , "Add Translation Points" },
        { ToolType.TranslateDelete, "Delete Translation Points" }
    };

    public const float FieldSize = 3.66f;

    private const float MoveSpeed = 2f;
    private const float ZoomSpeed = 25;
    private const float MinZoom = 1f;
    private const float MaxZoom = 5f;

    private readonly GameApplication _app;
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

    private readonly CommandList _commandList;

    private readonly Sprite _fieldSprite;
    private readonly Sprite _robotSprite;

    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _world;
    private readonly PathEditor _path;

    private ToolType _selectedTool = ToolType.TranslateAdd;

    private Entity? _selectedEntity;

    private readonly Stopwatch _playerWatch = Stopwatch.StartNew();

    public MotionEditorLayer(GameApplication app, ImGuiLayer imGuiLayer)
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

    private bool OnMouseEvent(MouseEvent @event)
    {
        if (@event.MouseButton == MouseButton.Left)
        {
            if (@event.Down)
            {
                _selectedEntity = _world.PickEntity(MouseWorld);

                if (_selectedEntity.HasValue)
                {
                    var entityPosition = _world.Get<PositionComponent>(_selectedEntity.Value).Position;

                    _selectPoint = entityPosition - MouseWorld;
                }
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

        if (_selectedTool == ToolType.TranslateAdd)
        {
            AddTranslationPoint();
        }

        if (_selectedTool == ToolType.TranslateDelete)
        {
            var picked = _world.PickEntity(MouseWorld);

            if (picked.HasValue && picked.Value.IsAlive() && _path.IsTranslationPoint(picked.Value))
            {
                _path.DeleteTranslationPoints(picked.Value);
            }
        }
    }

    private void AddTranslationPoint()
    {
        _path.CreateTranslationPoint(MouseWorld);
    }

    private void ImGuiLayerOnSubmit(ImGuiRenderer obj)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (ImGui.Begin("Tools"))
        {
            foreach (var value in Enum.GetValues<ToolType>())
            {
                if (ImGui.Button(ToolDescriptions[value]))
                {
                    _selectedTool = value;

                    break;
                }
            }
        }

        ImGui.End();

        if (ImGui.Begin("Inspector"))
        {

        }

        ImGui.End();

        if (ImGui.Begin("Player"))
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

            ImGui.Image(_playerBinding, imageSize.ToVector2());
        }

        ImGui.End();
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

    private void RenderPlayer()
    {
        var framebuffer = Assert.NotNull(_playerFramebuffer);

        _commandList.Begin();
        _commandList.SetFramebuffer(framebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Clear);
        _commandList.End();
        _app.Device.SubmitCommands(_commandList);

        _playerBatch.Clear();
        _playerBatch.TexturedQuad(Vector2.Zero, new Vector2(FieldSize, -FieldSize), _fieldSprite.Texture);
        _playerBatch.Submit(framebuffer: framebuffer); _playerBatch.Submit(framebuffer: framebuffer);

        _playerBatch.Clear();
        _path.DrawTranslationPath(_playerBatch, v => v with { Y = -v.Y });
        _playerBatch.Submit(framebuffer: framebuffer);
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
       UpdateCamera(frameInfo);
       UpdateSelection(frameInfo);
    }

    private void RenderEditor()
    {
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
        Systems.RenderConnections(_world, _editorBatch);
        _path.DrawTranslationPath(_editorBatch);

        _editorBatch.Submit(framebuffer: _editorProcessor.InputFramebuffer);
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