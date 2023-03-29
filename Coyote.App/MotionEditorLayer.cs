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
using GameFramework.Utilities.Extensions;
using ImGuiNET;
using Veldrid;

namespace Coyote.App;

internal class MotionEditorLayer : Layer, ITabStyle
{
    private enum ToolType
    {
        TranslateAdd,
        TranslateDelete,
    }

    private static readonly Dictionary<ToolType, string> ToolDescriptions = new Dictionary<ToolType, string>
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

    private readonly QuadBatch _batch;

    private readonly PostProcessor _processor;
    private readonly CommandList _commandList;

    private readonly Sprite _fieldSprite;
    private readonly Sprite _robotSprite;

    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _world;
    private readonly PathEditor _path;

    private ToolType _selectedTool = ToolType.TranslateAdd;

    private Entity? _selectedEntity;

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

        _batch = new QuadBatch(app);

        _processor = new PostProcessor(app);
        _processor.BackgroundColor = new RgbaFloat(25f / 255f, 25f / 255f, 25f / 255f, 1f);

        _commandList = app.Device.ResourceFactory.CreateCommandList();

        _fieldSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.PowerPlayField.jpg"));
        _robotSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Robot.png"));

        _world = World.Create();
        _path = new PathEditor(app, _world);

        UpdatePipeline();
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

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

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
    }

    protected override void Resize(Size size)
    {
        UpdatePipeline();
    }

    private void UpdatePipeline()
    {
        _cameraController.Camera.AspectRatio = _app.Window.Width / (float)_app.Window.Height;

        _processor.ResizeInputs(_app.Window.Size() * 2);
        _processor.SetOutput(_app.Device.SwapchainFramebuffer);
        _batch.UpdatePipelines(outputDescription: _processor.InputFramebuffer.OutputDescription);
        _path.UpdatePipelines(_processor.InputFramebuffer.OutputDescription);
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

    private void RenderBackground()
    {
        _batch.Clear();
        _batch.TexturedQuad(Vector2.Zero, Vector2.One * FieldSize, _fieldSprite.Texture);
        _batch.Submit(framebuffer: _processor.InputFramebuffer);
    }

    private void RenderWorld()
    {
        _batch.Clear();

        if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive())
        {
            var selected = _selectedEntity.Value;
            var rectangle = selected.GetRectangle();

            _batch.Quad(
                new Vector2(rectangle.CenterX(), rectangle.CenterY()),
                Vector2.One * new Vector2(rectangle.Width, rectangle.Height), 
                new RgbaFloat4(0, 1, 0, 0.5f));
        }

        Systems.RenderSprites(_world, _batch);
        Systems.RenderConnections(_world, _batch);
        _path.DrawPaths(_processor.InputFramebuffer, _cameraController.Camera.CameraMatrix);

        _batch.Submit(framebuffer: _processor.InputFramebuffer);
    }

    protected override void Render(FrameInfo frameInfo)
    {
        _batch.Effects = QuadBatchEffects.Transformed(_cameraController.Camera.CameraMatrix);

        _processor.ClearColor();
        
        RenderBackground();
        RenderWorld();

        _processor.Render();
    }
}