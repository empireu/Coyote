using System.Drawing;
using Arch.Core;
using GameFramework;
using GameFramework.Extensions;
using GameFramework.Layers;
using GameFramework.PostProcessing;
using GameFramework.Renderer.Batch;
using GameFramework.Scene;
using System.Numerics;
using GameFramework.ImGui;
using Veldrid;
using Arch.Core.Extensions;
using Coyote.App.Movement;
using GameFramework.Renderer;
using GameFramework.Utilities;
using GameFramework.Utilities.Extensions;
using ImGuiNET;

namespace Coyote.App.Nodes;

internal sealed class NodeEditorLayer : Layer, ITabStyle, IDisposable, INodeEditor
{
    private const float ZoomSpeed = 35;
    private const float MinZoom = 2.0f;
    private const float MaxZoom = 10f;
    private const float FontSize = 0.1f;
    private const float BorderSize = 0.01f;
    private const float NodeIconSize = 0.1f;
    private const float RunOnceSize = 0.035f;
    private const float TerminalSize = 0.025f;
    private const float ConnectionSize = 0.008f;
    private const float CamDragSpeed = 5f;
    private const float DragCamInterpolateSpeed = 50;

    private const float TerminalHighlightSize = TerminalSize * 1.2f;

    private const float GridDragGranularity = 50f;
    private const float GridSnapDistanceX = 0.015f;
  
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

    private enum TerminalEffect
    {
        Disable,
    }

    private readonly Dictionary<NodeTerminal, TerminalEffect> _terminalEffects = new();

    private readonly Dictionary<Entity, List<NodeAnalysis.Message>> _transientMessages = new();

    private string _nodeProjectName = "My Project";
    private int _selectedProject;

    public NodeEditorLayer(App app, ImGuiLayer imGui, NodeBehaviorRegistry behaviorRegistry)
    {
        _app = app;
        _imGui = imGui;
        _nodeBehaviors = behaviorRegistry.CreateSet();

        _cameraController = new OrthographicCameraController2D(
            new OrthographicCamera(0, -1, 1),
            translationInterpolate: DragCamInterpolateSpeed, zoomInterpolate: 10);

        _editorBatch = app.Resources.BatchPool.Get();

        _editorProcessor = new PostProcessor(app);
        _editorProcessor.BackgroundColor = ClearColor;

        _world = ArchWorld.Get();

        _runOnceSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.RunOnce.png"));
        _parentTerminalSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Nodes.InputTerminal.png"));
        _childTerminalSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Nodes.OutputTerminal.png"));

        imGui.Submit += ImGuiOnSubmit;

        app.Project.OnMotionProjectChanged += ProjectOnOnMotionProjectChanged;

        UpdateEditorPipeline();
    }

    private void ProjectOnOnMotionProjectChanged(MotionProject obj)
    {
        _world.Query(new QueryDescription().WithAll<NodeComponent>(), (in Entity e, ref NodeComponent comp) =>
        {
            if (comp.Behavior.ListenForProjectUpdate)
            {
                comp.Behavior.OnCoyoteProjectUpdated(e, this);
            }
        });

        Analyze();
    }

    private Vector2 _selectPoint;

    public static RectangleF GetTerminalRect(Entity e, NodeTerminal terminal)
    {
        var position = terminal.Type == NodeTerminalType.Parent 
            ? e.Get<NodeComponent>().Terminals.GetParentPosition(e, BorderSize) 
            : e.Get<NodeComponent>().Terminals.GetChildPosition(terminal, e, BorderSize);

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
            return IntersectsTerminal(entity, entity.Get<NodeComponent>().Terminals.ParentTerminal);
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
                CreateTerminalEffects();
            }
        }
    }

    private void CreateTerminalEffects()
    {
        _terminalEffects.Clear();
        
        var sourceEntity = Assert.NotNull(_selectedEntity);

        _world.Query(new QueryDescription().WithAll<NodeComponent>(), (in Entity entity, ref NodeComponent node) =>
        {
            if (entity == sourceEntity)
            {
                return;
            }

            foreach (var terminal in node.Terminals.ChildTerminals)
            {
                if (!entity.CanLinkTo(sourceEntity, terminal))
                {
                    _terminalEffects.Add(terminal, TerminalEffect.Disable);
                }
            }
        });
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
                Analyze();
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
        else if (@event is { MouseButton: MouseButton.Middle, Down: false })
        {
            FocusNode();
        }

        return true;
    }

    private void FocusNode()
    {
        SelectEntity();
        _dragLock = false;

        if (_selectedEntity != null)
        {
            var entity = Assert.NotNull(_selectedEntity);

            _cameraController.FuturePosition2 = entity.Position() + entity.Scale() * new Vector2(1, -1) / 2;
        }
    }

    private void FormConnection()
    {
        _terminalEffects.Clear();

        Assert.IsTrue(_dragParent);

        _dragParent = false;
        var childEntity = Assert.NotNull(_selectedEntity);

        var clipped = _world.Clip(MouseWorld, AlignMode.TopLeft, condition: (entity, rectangle, check) =>
        {
            if (check)
            {
                return true;
            }

            return entity.Get<NodeComponent>().Terminals.ChildTerminals.Any(x => IntersectsTerminal(entity, x));
        }).Where(x => x != childEntity).ToArray();

        if (clipped.Length == 0)
        {
            return;
        }

        var parentEntity = clipped.First();

        if (parentEntity.ChildrenEnt().Any(c => c == childEntity))
        {
            return;
        }

        ref var parentComp = ref parentEntity.Get<NodeComponent>();

        var parentTerm =
            parentComp.Terminals.ChildTerminals.FirstOrDefault(t =>
                GetTerminalRect(parentEntity, t).Contains(MouseWorld.ToPointF()));

        if (parentTerm == null)
        {
            return;
        }

        if (!parentEntity.CanLinkTo(childEntity, parentTerm))
        {
            return;
        }

        parentEntity.LinkTo(childEntity, parentTerm);
        SendEntitiesChanged(parentEntity, childEntity);
    }

    protected override void OnAdded()
    {
        RegisterHandler<MouseEvent>(OnMouseEvent);
    }

    private NodeAnalysis GetAnalysis(Entity e)
    {
        return new NodeAnalysis(_transientMessages.GetOrAdd(e, _ => new List<NodeAnalysis.Message>()));
    }

    private void ImGuiOnSubmit(ImGuiRenderer obj)
    {
        if (_disposed)
        {
            return;
        }

        if (!IsEnabled)
        {
            return;
        }

        _world.Clip(MouseWorld, AlignMode.TopLeft, condition: (entity, _, check) =>
        {
            if (check)
            {
                return true;
            }

            var terminals = entity.Get<NodeComponent>().Terminals;

            return IntersectsTerminal(entity, terminals.ParentTerminal) || terminals.ChildTerminals
                .Any(childTerm => IntersectsTerminal(entity, childTerm));

        }).IfPresent(entity => entity.Behavior().Hover(entity, MouseWorld, this));

        const string imId = "NodeEditorLayer";

        if (ImGuiExt.Begin("Nodes", imId))
        {
            ImGui.Combo(
                "Behavior",
                ref _selectedBehaviorIndex,
                _nodeBehaviors.Select(x => x.ToString()).ToArray(),
                _nodeBehaviors.Length);

            if (ImGui.Button("Place"))
            {
                Place();
                Analyze();
            }

            if (_selectedEntity != null)
            {
                ImGui.Separator();

                if (ImGui.Button("Delete"))
                {
                    _selectedEntity?.Also(e => _transientMessages.Remove(e));
                    _selectedEntity?.Also(SendEntityDeleting);
                    _selectedEntity?.Destroy();
                    _selectedEntity = null;
                    Analyze();
                }

                ImGui.SameLine();
                if (ImGui.Button("-Parent"))
                {
                    _selectedEntity?.Get<NodeComponent>().Parent?.UnlinkFrom(_selectedEntity.Value);
                    _selectedEntity?.Also(e => SendEntitiesChanged(e));
                    Analyze();
                }

                ImGui.SameLine();

                if (ImGui.Button("-Children"))
                {
                    var children = _selectedEntity?.ChildrenEnt().ToArray() ?? Array.Empty<Entity>();

                    _selectedEntity?.UnlinkChildren();
                    _selectedEntity?.Also(e => SendEntitiesChanged(children.Prepend(e).ToArray()));

                    Analyze();
                }
            }

            ImGui.Separator();

            ImGui.Spacing();

            ImGui.BeginGroup();
            ImGui.Text("Project Analyzer:");
            {
                Entity? highlight = null;

                nint id = 1;

                foreach (var entity in _transientMessages.Keys)
                {
                    if (!entity.IsAlive())
                    {
                        Assert.Fail("Lingering entity in lookup");
                    }

                    var messages = _transientMessages[entity];

                    foreach (var message in messages)
                    {
                        ImGui.TextColored(NodeAnalysis.MessageColor(message.Type), message.Text);
                    }

                    if (messages.Count > 0)
                    {
                        ImGui.PushID(id++);

                        if (ImGui.Button("Highlight"))
                        {
                            highlight = entity;
                        }

                        ImGui.PopID();

                        ImGui.Separator();
                    }
                }

                if (highlight.HasValue)
                {
                    _selectedEntity = highlight.Value;

                    _cameraController.FuturePosition2 = _selectedEntity.Value.Map(entity =>
                    {
                        var position = entity.Get<PositionComponent>().Position;
                        var scale = entity.Get<ScaleComponent>().Scale;

                        return position + scale / 2;
                    });
                }
            }

            ImGui.EndGroup();
        }

        ImGui.End();

        if (ImGuiExt.Begin("Project", imId))
        {
            if (ImGui.BeginTabBar("Motion Projects"))
            {
                if (ImGui.BeginTabItem("Save"))
                {
                    ImGui.InputText("Name", ref _nodeProjectName, 100);

                    if (ImGui.Button("OK"))
                    {
                        if (!string.IsNullOrEmpty(_nodeProjectName))
                        {
                            var overwrote = _app.Project.NodeProjects.ContainsKey(_nodeProjectName);
                            SaveProject();
                            _app.ToastInfo($"{(overwrote ? "Updated" : "Created")} project {_nodeProjectName}");
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
                    var items = _app.Project.NodeProjects.Keys.ToArray();

                    ImGui.Combo("Projects", ref _selectedProject, items, items.Length);

                    if (ImGui.Button("OK") && _selectedProject >= 0 && _selectedProject < items.Length)
                    {
                        _nodeProjectName = items[_selectedProject];
                        LoadProject(_nodeProjectName);
                        _app.ToastInfo($"Loaded project {_nodeProjectName}");
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.End();

        if (ImGuiExt.Begin("Inspector", imId))
        {
            if (_selectedEntity == null || !_selectedEntity.Value.IsAlive())
            {
                ImGui.Text("Nothing to show");
            }
            else
            {
                var entity = _selectedEntity.Value;

                if (Inspector.SubmitEditor(entity))
                {
                    Analyze();
                    SendEntitiesChanged(entity);
                }

                if (entity.Get<NodeComponent>().Behavior.SubmitInspector(entity, this))
                {
                    Analyze();
                    SendEntitiesChanged(entity);
                }
            }
        }

        ImGui.End();
    }

    private void SendEntitiesChanged(params Entity[] changed)
    {
        _world.Query(new QueryDescription().WithAll<NodeComponent, EntitiesChangedListenerComponent>(), (in Entity entity, ref NodeComponent node) =>
        {
            node.Behavior.OnEntitiesChanged(entity, changed);
        });
    }

    private void SendEntityDeleting(Entity deleting)
    {
        _world.Query(new QueryDescription().WithAll<NodeComponent, EntityDeletingListenerComponent>(), (in Entity entity, ref NodeComponent node) =>
        {
            node.Behavior.OnEntityDeleting(entity, deleting);
        });
    }

    private void Analyze()
    {
        var nodeNames = new HashSet<string>();
        
        _world.Query(new QueryDescription().WithAll<NodeComponent>(), (in Entity entity, ref NodeComponent node) =>
        {
            var analysis = GetAnalysis(entity);
            analysis.Messages.Clear();

            if (node.Parent == null)
            {
                // Analyze duplicate root names. Also warn on empty names.

                if (!nodeNames.Add(node.Name))
                {
                    analysis.Error("Ambiguous root name");
                }

                if (string.IsNullOrWhiteSpace(node.Name))
                {
                    analysis.Warn("Empty root name");
                }
            }

            // Custom analysis:
            node.Behavior.Analyze(entity, analysis, this);
        });
    }

    private void SaveProject()
    {
        var project = NodeProject.FromNodeWorld(_world);
        _app.Project.NodeProjects[_nodeProjectName] = project;
        _app.Project.Save();
    }

    private void LoadProject(string nodeProjectName)
    {
        _transientMessages.Clear();

        var nodeProject = _app.Project.NodeProjects[nodeProjectName];

        _world.Clear();
        _selectedEntity = null;

        nodeProject.Load(_world, _nodeBehaviors);

        Analyze();
    }

    private void Place()
    {
        var behavior = _nodeBehaviors[_selectedBehaviorIndex];
        var entity = behavior.CreateEntity(_world, MouseWorld);

        Assert.IsTrue(entity.Get<NodeComponent>().Behavior == behavior);
        
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
            if (_dragCamera)
            {
                var delta = (_app.Input.MouseDelta / new Vector2(_app.Window.Width, _app.Window.Height)) * new Vector2(-1, 1) * _cameraController.Camera.Zoom * CamDragSpeed;
                _cameraController.FuturePosition2 += delta;
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

        if (NodeEditorBinds.GridSnap)
        {
            // Grid drag for more accurate placement:

            newPosition *= GridDragGranularity;
            newPosition = new Vector2(MathF.Truncate(newPosition.X) / GridDragGranularity, MathF.Truncate(newPosition.Y) / GridDragGranularity);
        }

        if (NodeEditorBinds.AlignSnap)
        {
            // Align to closest left node:

            var entities = new List<Entity>();

            _world.GetEntities(new QueryDescription().WithAll<PositionComponent, ScaleComponent, NodeComponent>(), entities);

            entities.Remove(entity);

            if (entities.Count > 0)
            {
                var actualPos = entity.Position();

                while (entities.Count > 0)
                {
                    var closest = 
                        entities.MinBy(e =>
                            Vector2.DistanceSquared( // Closest distance between the left node's right corner and our left corner
                                new Vector2(
                                    e.Position().X + e.Scale().X, 
                                    e.Position().Y
                                ), 
                                actualPos));

                    newPosition = closest.Position() with { X = closest.Position().X + closest.Scale().X + GridSnapDistanceX };

                    if (_world.Clip(newPosition).All(c => c == entity))
                    {
                        break;
                    }

                    entities.Remove(closest);
                }
            }
        }

        var deltaPos = newPosition - entity.Position();
      
        entity.Move(newPosition);

        if (NodeEditorBinds.MultiDrag)
        {
            if (entity.Children().Count > 0)
            {
                void Traverse(Entity e)
                {
                    e.Move(e.Position() + deltaPos);
                    e.ChildrenEnt().ForEach(Traverse);
                }

                entity.ChildrenEnt().ForEach(Traverse);
            }
            else
            {
                entity.Get<NodeComponent>()
                    .Parent
                    ?.ChildrenEnt()
                    .Where(c => c != entity)
                    .ForEach(sibling => sibling.Move(sibling.Position() + deltaPos));
            }
        }
    }

    private void RenderConnectionPreview()
    {
        if (!_dragParent)
        {
            return;
        }

        var entity = Assert.NotNull(_selectedEntity);

        RenderLineConnection(entity.Get<NodeComponent>().Terminals.GetParentPosition(entity, BorderSize), MouseWorld, PreviewConnection);
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
                _world.Query(
                    new QueryDescription()
                        .WithAll<PositionComponent, ScaleComponent, NodeComponent>(), 
                    callback);
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

        if (behavior.Icon.IsInvalid)
        {
            Assert.Fail("Invalid behavior icon");
        }

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
        var set = nodeComponent.Terminals;
        var mouseWorld = MouseWorld.ToPointF();

        void SubmitTerminal(Vector2 position, NodeTerminal terminal, Entity eDeRef)
        {
            var size = TerminalSize;

            if (
                GetTerminalRect(eDeRef, terminal).Contains(mouseWorld) 
                && (!_dragParent || terminal.Type == NodeTerminalType.Children) /* Do not highlight if we are dragging a connection and this terminal is a parent terminal.*/
                && (_dragParent || terminal.Type == NodeTerminalType.Parent)/* Do not highlight child terminals if we are not dragging a parent terminal (you cannot interact with them) */
            )
            {
                size = TerminalHighlightSize;
            }

            _editorBatch.TexturedQuad(
                position, 
                Vector2.One * size, 
                terminal.Type == NodeTerminalType.Children ? _childTerminalSprite.Texture : _parentTerminalSprite.Texture);
        }

        SubmitTerminal(set.GetParentPosition(e, BorderSize), set.ParentTerminal, e);

        for (var index = 0; index < set.ChildTerminals.Count; index++)
        {
            var terminal = set.ChildTerminals[index];

            if (_terminalEffects.TryGetValue(terminal, out var effect))
            {
                if (effect == TerminalEffect.Disable)
                {
                    continue;
                }
            }

            SubmitTerminal(set.GetChildPosition(terminal, e, BorderSize), terminal, e);
        }
    }

    private void RenderNodeConnections(in Entity e, ref PositionComponent positionComponent, ref ScaleComponent scaleComponent, ref NodeComponent nodeComponent)
    {
        foreach (var childNode in nodeComponent.ChildrenRef.Instance)
        {
            RenderLineConnection(nodeComponent.Terminals.GetChildPosition(childNode.Terminal, e, BorderSize), childNode.Entity.Get<NodeComponent>().Terminals.GetParentPosition(childNode.Entity, BorderSize), RealizedConnection);
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

    public override string ToString()
    {
        return $"Nodes | {_nodeProjectName}";
    }

    public void FocusCamera(Vector2 txWorldTarget)
    {
        _cameraController.FuturePosition2 = txWorldTarget;
    }

    public void FocusCamera(Entity entity)
    {
        _cameraController.FuturePosition2 = entity.Position() + entity.Scale() / 2 * new Vector2(1, -1);
    }

    public Project CoyoteProject => _app.Project;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _app.Resources.BatchPool.Return(_editorBatch);
        _editorProcessor.Dispose();
        ArchWorld.Return(_world);
        _imGui.Submit -= ImGuiOnSubmit;
    }
}

[InputAccessor]
public static class NodeEditorBinds
{
    public static KeyBind GridSnap = new("Grid Snap", Key.ShiftLeft);
    public static KeyBind AlignSnap = new("Align To Neighbor", Key.ControlLeft);
    public static KeyBind MultiDrag = new("Multi Drag", Key.AltLeft);
}