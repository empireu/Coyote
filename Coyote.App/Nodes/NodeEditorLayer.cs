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
using GameFramework.Utilities.Extensions;
using ImGuiNET;

namespace Coyote.App.Nodes;

internal sealed class NodeEditorLayer : Layer, ITabStyle, IDisposable
{
    private const float MoveSpeed = 2f;
    private const float ZoomSpeed = 35;
    private const float MinZoom = 1.0f;
    private const float MaxZoom = 20f;
    private const float FontSize = 0.1f;
    private const float BorderSize = 0.01f;
    private const float NodeIconSize = 0.1f;
    private const float RunOnceSize = 0.035f;
    private const float TerminalSize = 0.015f;
    private const float ConnectionSize = 0.008f;
    private const float GridDragGranularity = 100f;
    private const float CamDragSpeed = 5f;
    private const float KeyboardCamInterpolateSpeed = 15;
    private const float DragCamInterpolateSpeed = 35;

    private static readonly RgbaFloat ClearColor = new(0.05f, 0.05f, 0.05f, 0.95f);
    private static readonly Vector4 SelectedTint = new(1.1f, 1.1f, 1.1f, 1.2f);
    private static readonly RgbaFloat PreviewConnection = new(0.5f, 0.5f, 0.1f, 0.5f);
    private static readonly RgbaFloat RealizedConnection = new(0.5f, 0.5f, 0.1f, 0.95f);

    private readonly App _app;
    private readonly ImGuiLayer _imGui;

    private readonly NodeBehavior[] _nodeBehaviors;
    private int _selectedBehaviorIndex;

    private readonly QuadBatch _editorBatch;

    private readonly PostProcessor _editorProcessor;
    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _world;

    private Entity? _selectedEntity;
    private int _pickIndex;

    // Locked dragging a node
    private bool _dragLock;
    
    // Locked dragging a parent terminal
    private bool _dragParent;

    // Dragging the camera with the mouse
    private bool _dragCamera;

    private readonly Sprite _runOnceSprite;
    private readonly Sprite _parentTerminalSprite;
    private readonly Sprite _childTerminalSprite;

    public NodeEditorLayer(App app, ImGuiLayer imGui, NodeBehaviorRegistry behaviorRegistry)
    {
        _app = app;
        _imGui = imGui;
        _nodeBehaviors = behaviorRegistry.CreateSet();

        _cameraController = new OrthographicCameraController2D(
            new OrthographicCamera(0, -1, 1),
            translationInterpolate: KeyboardCamInterpolateSpeed, zoomInterpolate: 10);

        _editorBatch = app.Resources.BatchPool.Get();

        _editorProcessor = new PostProcessor(app);
        _editorProcessor.BackgroundColor = ClearColor;

        _world = World.Create();

        _runOnceSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.RunOnce.png"));
        _parentTerminalSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Nodes.InputTerminal.png"));
        _childTerminalSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Nodes.OutputTerminal.png"));

        imGui.Submit += ImGuiOnSubmit;

        UpdateEditorPipeline();
    }

    private Vector2 _selectPoint;

    private static RectangleF GetTerminalRect(Entity e, NodeTerminal terminal)
    {
        var position = terminal.Type == NodeTerminalType.Parent 
            ? e.Get<NodeComponent>().Connections.GetParentPosition(e, BorderSize) 
            : e.Get<NodeComponent>().Connections.GetChildPosition(terminal, e, BorderSize);

        return new RectangleF(position.X - TerminalSize / 2, position.Y - TerminalSize / 2, TerminalSize, TerminalSize);
    }

    private bool IntersectsTerminal(Entity entity, NodeTerminal terminal)
    {
        return GetTerminalRect(entity, terminal).Contains(MouseWorld.ToPointF());
    }

    private void SelectEntity()
    {
        bool IntersectsParentTerminal(Entity entity)
        {
            return IntersectsTerminal(entity, entity.Get<NodeComponent>().Connections.ParentTerminal);
        }

        List<Entity> ClipTerminalEntities()
        {
            return _world.Clip(MouseWorld, AlignMode.TopLeft, condition: (entity, rectangle, check) =>
            {
                if (check)
                {
                    return true;
                }

                return IntersectsParentTerminal(entity);
            });
        }

        var entities = ClipTerminalEntities();

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

            if (IntersectsParentTerminal(_selectedEntity.Value))
            {
                _dragParent = true;
            }
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
        else if(@event is {MouseButton: MouseButton.Left, Down: false})
        {
            if (_dragParent)
            {
                FormConnection();
            }
        }
        else if (@event is { MouseButton: MouseButton.Right, Down: true })
        {
            _dragCamera = true;
        }
        else if (@event is { MouseButton: MouseButton.Right, Down: false })
        {
            _dragCamera = false;
        }

        return true;
    }

    private void FormConnection()
    {
        Assert.IsTrue(_dragParent);

        _dragParent = false;
        var childEntity = Assert.NotNull(_selectedEntity);

        var clipped = _world.Clip(MouseWorld, AlignMode.TopLeft, condition: (entity, rectangle, check) =>
        {
            if (check)
            {
                return true;
            }

            return entity.Get<NodeComponent>().Connections.ChildTerminals.Any(x => IntersectsTerminal(entity, x));
        }).Where(x => x != childEntity).ToArray();

        if (clipped.Length == 0)
        {
            return;
        }

        var parentEntity = clipped.First();

        ref var parentComp = ref parentEntity.Get<NodeComponent>();
        ref var childComp = ref childEntity.Get<NodeComponent>();

        var parentTerm =
            parentComp.Connections.ChildTerminals.FirstOrDefault(t =>
                GetTerminalRect(parentEntity, t).Contains(MouseWorld.ToPointF()));

        if (parentTerm == null)
        {
            return;
        }

        if (!childComp.Connections.ParentTerminal.AcceptsConnection(parentTerm, childEntity, parentEntity) ||
            !parentTerm.AcceptsConnection(childComp.Connections.ParentTerminal, parentEntity, childEntity) ||
            !childComp.Behavior.AcceptsParent(childEntity, parentEntity, parentTerm) ||
            !parentComp.Behavior.AcceptsChild(parentEntity, childEntity))
        {
            return;
        }
        
        // Remove old parent
        childComp.Parent?.Unlink(childEntity);

        // Prepare connections
        childComp.Connections.ParentTerminal.PrepareConnection(parentTerm, childEntity, parentEntity);
        parentTerm.PrepareConnection(childComp.Connections.ParentTerminal, parentEntity, childEntity);


        // Form Connections

        childComp.Parent = parentEntity;
        parentComp.ChildrenRef.Instance.Add(new NodeChild(childEntity, parentTerm));

        childComp.Connections.ParentTerminal.FinishConnection(parentTerm, childEntity, parentEntity);
        parentTerm.FinishConnection(childComp.Connections.ParentTerminal, parentEntity, childEntity);
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

            if (ImGui.Button("Save"))
            {
                var result = NodeProject.FromNodeWorld(_world);

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
        
        behavior.AttachComponents(entity);

        _dragLock = true;

        _selectedEntity = entity;
    }

    private Vector2 MouseWorld => _cameraController.Camera.MouseToWorld2D(_app.Input.MousePosition, _app.Window.Width, _app.Window.Height);

    private void UpdateEditorPipeline()
    {
        _cameraController.Camera.AspectRatio = _app.Window.Width / (float)_app.Window.Height;

        _editorProcessor.ResizeInputs(_app.Window.Size() * 2);
        _editorProcessor.SetOutput(_app.Device.SwapchainFramebuffer);
        _editorBatch.UpdatePipelines(outputDescription: _editorProcessor.InputFramebuffer.OutputDescription);
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

            if (_dragCamera)
            {
                var delta = (_app.Input.MouseDelta / new Vector2(_app.Window.Width, _app.Window.Height)) * new Vector2(-1, 1) * _cameraController.Camera.Zoom * CamDragSpeed;
                _cameraController.FuturePosition2 += delta;
                _cameraController.TranslationInterpolate = DragCamInterpolateSpeed;
            }
            else
            {
                _cameraController.TranslationInterpolate = KeyboardCamInterpolateSpeed;
            }
        }

        _cameraController.FutureZoom += _app.Input.ScrollDelta * ZoomSpeed * frameInfo.DeltaTime;
        _cameraController.FutureZoom = Math.Clamp(_cameraController.FutureZoom, MinZoom, MaxZoom);

        _cameraController.Update(frameInfo.DeltaTime);
    }

    private void UpdateDragNode()
    {
        if (_imGui.Captured)
        {
            return;
        }

        if (_selectedEntity == null || !(_app.Input.IsMouseDown(MouseButton.Left) || _dragLock))
        {
            return;
        }

        if (_dragParent)
        {
            return;
        }

        var entity = _selectedEntity.Value;
        var newPosition = MouseWorld + _selectPoint;

        if (_app.Input.IsKeyDown(Key.ShiftLeft))
        {
            newPosition *= GridDragGranularity;
            newPosition = new Vector2(MathF.Truncate(newPosition.X) / GridDragGranularity, MathF.Truncate(newPosition.Y) / GridDragGranularity);
        }

        entity.Move(newPosition);
    }

    private void RenderConnectionPreview()
    {
        if (!_dragParent)
        {
            return;
        }

        var entity = Assert.NotNull(_selectedEntity);

        RenderLineConnection(entity.Get<NodeComponent>().Connections.GetParentPosition(entity, BorderSize), MouseWorld, PreviewConnection);
    }

    private void RenderLineConnection(Vector2 a, Vector2 b, RgbaFloat4 color)
    {
        if (b.Y < a.Y)
        {
            (a, b) = (b, a);
        }

        var height = b.Y - a.Y;

        a.X -= ConnectionSize / 2f;
        b.X -= ConnectionSize / 2f;

        _editorBatch.Quad(b, new Vector2(ConnectionSize, height / 2), color, align: AlignMode.TopLeft);
        _editorBatch.Quad(a + new Vector2(0, height / 2), new Vector2(ConnectionSize, height / 2), color, align: AlignMode.TopLeft);
        _editorBatch.Quad(new Vector2(Math.Min(a.X, b.X), a.Y + height / 2), new Vector2(Math.Abs(a.X - b.X) + ConnectionSize, ConnectionSize), color, align: AlignMode.TopLeft);
    }

    protected override void Update(FrameInfo frameInfo)
    {
        UpdateCamera(frameInfo); 
        UpdateDragNode();
    }

    private void RenderEditor()
    {
        void RenderPass(Action submit)
        {
            _editorBatch.Clear();
            submit();
            _editorBatch.Submit(framebuffer: _editorProcessor.InputFramebuffer, wait: false);
        }

        void RenderPassEntity(ForEachWithEntity<PositionComponent, ScaleComponent, NodeComponent> callback)
        {
            RenderPass(() =>
            {
                var query = _world.Query(new QueryDescription().WithAll<PositionComponent, ScaleComponent, NodeComponent>());

                foreach (var chunk in query.GetChunkIterator())
                {
                    foreach (var entity in chunk.Entities)
                    {
                        callback(entity, ref entity.Get<PositionComponent>(), ref entity.Get<ScaleComponent>(), ref entity.Get<NodeComponent>());
                    }
                }
            });
        }

        RenderPassEntity(RenderNodeContent);
        RenderPassEntity(RenderNodeConnections);
        RenderPass(RenderConnectionPreview);
        RenderPassEntity(RenderNodeTerminals);
    }

    /// <summary>
    ///     Renders a node and re-fits the scale based on the rendered surface.
    /// </summary>
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

    private void RenderNodeTerminals(in Entity e, ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref NodeComponent nodeComponent)
    {
        var set = nodeComponent.Connections;

        void SubmitTerminal(Vector2 position, NodeTerminal terminal)
        {
            _editorBatch.TexturedQuad(position, Vector2.One * TerminalSize, terminal.Type == NodeTerminalType.Children ? _childTerminalSprite.Texture : _parentTerminalSprite.Texture);
        }

        SubmitTerminal(set.GetParentPosition(e, BorderSize), set.ParentTerminal);
        
        foreach (var terminal in set.ChildTerminals)
        {
            SubmitTerminal(set.GetChildPosition(terminal, e, BorderSize), terminal);
        }
    }

    private void RenderNodeConnections(in Entity e, ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref NodeComponent nodeComponent)
    {
        foreach (var childNode in nodeComponent.ChildrenRef.Instance)
        {
            RenderLineConnection(nodeComponent.Connections.GetChildPosition(childNode.Terminal, e, BorderSize), childNode.Entity.Get<NodeComponent>().Connections.GetParentPosition(childNode.Entity, BorderSize), RealizedConnection);
        }
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