using System.Drawing;
using Arch.Core;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Layers;
using GameFramework.PostProcessing;
using GameFramework.Renderer.Batch;
using GameFramework.Scene;
using System.Numerics;
using System.Runtime.CompilerServices;
using GameFramework.ImGui;
using Veldrid;
using Arch.Core.Extensions;
using GameFramework.Renderer;
using GameFramework.Utilities;
using ImGuiNET;

namespace Coyote.App.Nodes;

internal sealed class NodeEditorLayer : Layer, ITabStyle, IDisposable
{
    private const float MoveSpeed = 2f;
    private const float ZoomSpeed = 35;
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 20f;
    private const float FontSize = 0.1f;
    private const float BorderSize = 0.01f;
    private const float NodeIconSize = 0.1f;
    private const float RunOnceSize = 0.035f;

    private static readonly Vector4 SelectedTint = new(1.5f, 1.5f, 1.5f, 1.5f);

    private readonly App _app;
    private readonly ImGuiLayer _imGui;

    private readonly NodeBehavior[] _nodeBehaviors;
    private int _selectedBehaviorIndex;

    private readonly QuadBatch _editorBatch;

    // Used by the editor (translation and rotation)"
    private readonly PostProcessor _editorProcessor;
    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _world;

    private Entity? _selectedEntity;
    private int _pickIndex;
    private bool _dragLock;

    private readonly Sprite _runOnceSprite;

    public NodeEditorLayer(App app, ImGuiLayer imGui, NodeBehaviorRegistry behaviorRegistry)
    {
        _app = app;
        _imGui = imGui;
        _nodeBehaviors = behaviorRegistry.CreateSet();

        _cameraController = new OrthographicCameraController2D(
            new OrthographicCamera(0, -1, 1),
            translationInterpolate: 15f, zoomInterpolate: 10);

        _editorBatch = app.Resources.BatchPool.Get();

        _editorProcessor = new PostProcessor(app);
        _editorProcessor.BackgroundColor = new RgbaFloat(5f / 255f, 5f / 255f, 5f / 255f, 1f);

        _world = World.Create();

        _runOnceSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.RunOnce.png"));

        imGui.Submit += ImGuiOnSubmit;

        UpdateEditorPipeline();
    }

    private Vector2 _selectPoint;

    private void SelectEntity()
    {
        var entities = _world.Clip(MouseWorld, AlignMode.TopLeft);

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
        if (_disposed)
        {
            return false;
        }

        if (@event is { MouseButton: MouseButton.Left, Down: true })
        {
            SelectEntity();

            _dragLock = false;
        }
        
        return true;
    }

    protected override void OnAdded()
    {
        RegisterHandler<MouseEvent>(OnMouseEvent);
    }

    private void ImGuiOnSubmit(ImGuiRenderer obj)
    {
        if (_disposed)
        {
            return;
        }

        if (ImGui.Begin("Nodes"))
        {
            ImGui.Combo(
                "Place",
                ref _selectedBehaviorIndex, 
                _nodeBehaviors.Select(x => x.ToString()).ToArray(),
                _nodeBehaviors.Length);

            if (ImGui.Button("OK"))
            {
                Place();
            }
        }

        ImGui.End();

        if (ImGui.Begin("Inspector"))
        {
            if (_selectedEntity == null || !_selectedEntity.Value.IsAlive())
            {
                ImGui.Text("Nothing to show");
            }
            else
            {
                Inspector.SubmitEditor(_selectedEntity.Value);
            }
        }

        ImGui.End();
    }

    private void Place()
    {
        var behavior = _nodeBehaviors[_selectedBehaviorIndex];
        var entity = behavior.CreateEntity(_world, MouseWorld);

        Assert.IsTrue(entity.Get<NodeComponent>().Behavior == behavior);
        
        behavior.AttachComponents(in entity);

        _dragLock = true;

        _selectedEntity = entity;
    }

    private Vector2 MouseWorld => _cameraController.Camera.MouseToWorld2D(_app.Input.MousePosition, _app.Window.Width, _app.Window.Height);

    private void UpdateEditorPipeline()
    {
        _cameraController.Camera.AspectRatio = _app.Window.Width / (float)_app.Window.Height;

        _editorProcessor.ResizeInputs(_app.Window.Size() * 2);
        _editorProcessor.SetOutput(_app.Device.SwapchainFramebuffer);
        _editorBatch.UpdatePipelines(outputDescription: _editorProcessor.InputFramebuffer.OutputDescription, depthStencilState: DepthStencilStateDescription.DepthOnlyLessEqual);
    }

    protected override void Resize(Size size)
    {
        UpdateEditorPipeline();
    }
    private void UpdateCamera(FrameInfo frameInfo)
    {
        if (!_imGui.Captured)
        {
            foreach (var inputDownKey in _app.Input.DownKeys)
            {
                _cameraController.ProcessKey(inputDownKey,
                    MoveSpeed * frameInfo.DeltaTime,
                    0);
            }
        }

        _cameraController.FutureZoom += _app.Input.ScrollDelta * ZoomSpeed * frameInfo.DeltaTime;
        _cameraController.FutureZoom = Math.Clamp(_cameraController.FutureZoom, MinZoom, MaxZoom);

        _cameraController.Update(frameInfo.DeltaTime);
    }

    private void UpdateSelection()
    {
        if (_imGui.Captured)
        {
            return;
        }

        if (_selectedEntity == null || !(_app.Input.IsMouseDown(MouseButton.Left) || _dragLock))
        {
            return;
        }

        var entity = _selectedEntity.Value;
        var newPosition = MouseWorld + _selectPoint;

        if (_app.Input.IsKeyDown(Key.ShiftLeft))
        {
            newPosition *= 10;
            newPosition = new Vector2(MathF.Truncate(newPosition.X) / 10f, MathF.Truncate(newPosition.Y) / 10f);
        }


        entity.Move(newPosition);
    }

    protected override void Update(FrameInfo frameInfo)
    {
        UpdateCamera(frameInfo); 
        UpdateSelection();
    }

    private void RenderEditor()
    {
        _editorBatch.Clear();
        var query = _world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent, NodeComponent>());

        foreach (var chunk in query.GetChunkIterator())
        {
            foreach (var entity in chunk.Entities)
            {
                RenderNodeContent(entity, ref entity.Get<PositionComponent>(), ref entity.Get<ScaleComponent>(), ref entity.Get<NodeComponent>());
            }
        }
        
        _editorBatch.Submit(framebuffer: _editorProcessor.InputFramebuffer);
    }

    /// <summary>
    ///     Renders a node and re-fits the scale based on the rendered surface.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RenderNodeContent(in Entity e, ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref NodeComponent nodeComponent)
    {
        var isSelected = e == _selectedEntity;
        var surface = Vector2.Zero;
        var behavior = nodeComponent.Behavior;
        var font = _app.Font;
        var position = positionComponent.Position;

        var iconPosition = position + new Vector2(BorderSize * 1.25f, -BorderSize * 1.25f);
        var iconSize = Vector2.One * NodeIconSize;
        
        _editorBatch.TexturedQuad(iconPosition, iconSize, behavior.Icon, align: AlignMode.TopLeft);

        surface += font.MeasureText(nodeComponent.Description, FontSize);
        surface += iconSize with { Y = 0 };
        surface = surface.MaxWith(iconSize + Vector2.One * BorderSize * 2.5f);

        var borderTopLeft = position + new Vector2(-BorderSize / 2, BorderSize / 2);
        var borderWidth = surface.X + BorderSize;
        var borderHeight = surface.Y + BorderSize;
        var borderColor = new RgbaFloat(Vector4.One - behavior.BackgroundColor);

        // Left
        _editorBatch.Quad(
            borderTopLeft, 
            new Vector2(BorderSize, borderHeight),
            borderColor, 
            align: AlignMode.TopLeft);

        // Right
        _editorBatch.Quad(
            borderTopLeft + surface with { Y = 0 },
            new Vector2(BorderSize, borderHeight),
            borderColor,
            align: AlignMode.TopLeft);

        // Top
        _editorBatch.Quad(
            borderTopLeft,
            new Vector2(borderWidth, BorderSize),
            borderColor,
            align: AlignMode.TopLeft);

        // Bottom
        _editorBatch.Quad(
            borderTopLeft - surface with { X = 0},
            new Vector2(borderWidth, BorderSize),
            borderColor,
            align: AlignMode.TopLeft);

        font.Render(_editorBatch, position + iconSize with { Y = 0 } + new Vector2(2 * BorderSize, 0), nodeComponent.Description, size: FontSize);

        _editorBatch.Quad(position, surface, isSelected ? nodeComponent.Behavior.BackgroundColor * SelectedTint : nodeComponent.Behavior.BackgroundColor, align: AlignMode.TopLeft);

        if (nodeComponent.ExecuteOnce)
        {
            _editorBatch.TexturedQuad(position, Vector2.One * RunOnceSize, _runOnceSprite.Texture, align: AlignMode.TopLeft);
        }

        scaleComponent.Scale = surface;
    }

    protected override void Render(FrameInfo frameInfo)
    {
        _editorBatch.Effects = QuadBatchEffects.Transformed(_cameraController.Camera.CameraMatrix);

        _editorProcessor.ClearColor();

        RenderEditor();

        _editorProcessor.Render();
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _app.Resources.BatchPool.Return(_editorBatch);
        _editorProcessor.Dispose();
        _world.Dispose();
        _imGui.Submit -= ImGuiOnSubmit;
    }
}