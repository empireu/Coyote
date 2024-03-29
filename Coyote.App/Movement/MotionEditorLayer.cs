﻿using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using Coyote.Mathematics;
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

namespace Coyote.App.Movement;

internal class MotionEditorLayer : Layer, ITabStyle, IDisposable, IProjectTab
{
    private enum ToolType
    {
        TranslateAdd,
        TranslateDelete,
        RotateAdd,
        RotateDelete,
        MarkerAdd,
        MarkerDelete
    }

    private static readonly Dictionary<ToolType, string> ToolDescriptions = new()
    {
        { ToolType.TranslateAdd, "Add Translation Points" },
        { ToolType.TranslateDelete, "Delete Translation Points" },
        { ToolType.RotateAdd, "Add Rotation Points" },
        { ToolType.RotateDelete, "Delete Rotation Points" },
        { ToolType.MarkerAdd, "Add Markers" },
        { ToolType.MarkerDelete, "Delete Markers" }
    };

    private static readonly Dictionary<ToolType, IResourceKey> ToolTextures = new()
    {
        { ToolType.TranslateAdd, App.Asset("Images.AddTranslationPoint.png") },
        { ToolType.TranslateDelete, App.Asset("Images.DeleteTranslationPoint.png") },
        { ToolType.RotateAdd, App.Asset("Images.AddRotationPoint.png") },
        { ToolType.RotateDelete, App.Asset("Images.DeleteRotationPoint.png") },
        { ToolType.MarkerAdd, App.Asset("Images.AddMarker.png") },
        { ToolType.MarkerDelete, App.Asset("Images.DeleteMarker.png") }
    };

    private const float FieldSize = 3.66f;

    private static readonly Vector4 VelocityColor = new(1, 0.1f, 0.1f, 1f);
    private static readonly Vector4 AccelerationColor = new(0.5f, 1f, 0.1f, 1f);
    private static readonly Vector4 AngularVelocityColor = new(1, 0.1f, 0.5f, 1f);
    private static readonly Vector4 AngularAccelerationColor = new(0.5f, 1f, 0.5f, 1f);

    private static readonly Vector4 DisplacementColor = new(1, 1f, 1f, 1f);
    private static readonly Vector4 TimeColor = new(0, 0.5f, 1f, 1f);

    private static readonly Vector4 PendingMarkerColor = new(0.8f, 0.1f, 0.1f, 1.0f);
    private static readonly Vector4 HitMarkerColor = new(0.1f, 0.9f, 0f, 1.0f);

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
    private readonly Sprite _arrowSprite;

    private readonly OrthographicCameraController2D _cameraController;

    private readonly World _pathWorld;
    private readonly PathEditor _path;

    private ToolType _selectedTool = ToolType.TranslateAdd;

    private Entity? _selectedEntity;

    private int _pickIndex;

    private string _motionProjectName = "My Project";
    private int _selectedProject;

    private bool _renderPositionPoints = true;
    private bool _renderTranslationVelocityPoints = true;
    private bool _renderTranslationAccelerationPoints = true;
    private bool _renderRotationPoints = true;
    private bool _renderRotationTangents = true;
    private bool _renderPlayerVelocity;
    private bool _renderPlayerAcceleration;

    private readonly Simulator _simulator;

    private float _dt;

    private bool _disposed;

    public MotionEditorLayer(App app, ImGuiLayer imGuiLayer)
    {
        _app = app;

        _imGuiLayer = imGuiLayer;

        imGuiLayer.Submit += ImGuiLayerOnSubmit;

        _cameraController = new OrthographicCameraController2D(new OrthographicCamera(0, -1, 1), translationInterpolate: 15f, zoomInterpolate: 10)
        {
            FutureZoom = FieldSize
        };

        _editorBatch = app.Resources.BatchPool.Get();
        _playerBatch = app.Resources.BatchPool.Get().Also(b =>
        {
            b.Effects = QuadBatchEffects.None with
            {
                Transform = Matrix4x4.CreateOrthographic(
                    FieldSize,
                    FieldSize,
                    -1,
                    1)
            };
        });

        _editorProcessor = new PostProcessor(app);
        _editorProcessor.BackgroundColor = new RgbaFloat(25f / 255f, 25f / 255f, 25f / 255f, 1f);

        _commandList = app.Device.ResourceFactory.CreateCommandList();

        _fieldSprite = app.Resources.AssetManager.GetSpriteForTexture(new FileResourceKey("Field.png"));
        _robotSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Robot.png"));
        _arrowSprite = app.Resources.AssetManager.GetSpriteForTexture(App.Asset("Images.Arrow.png"));

        _pathWorld = ArchWorld.Get();
        _path = new PathEditor(app, _pathWorld);
        _simulator = new Simulator(app, _path);

        UpdateEditorPipeline();
    }

    protected override void OnAdded()
    {
        RegisterHandler<MouseEvent>(OnMouseEvent);
    }

    private Vector2 MouseWorld => _cameraController.Camera.MouseToWorld2D(_app.Input.MousePosition, _app.Window.Width, _app.Window.Height);

    private Vector2 _selectPoint;

    private void SelectEntity()
    {
        var entities = _pathWorld.Clip(MouseWorld);

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

            var entityPosition = _pathWorld.Get<PositionComponent>(_selectedEntity.Value).Position;

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
                if (_path.CanCreateRotationPoint)
                {
                    _path.CreateRotationPoint(MouseWorld);
                }
                break;
            case ToolType.RotateDelete:
                {
                    SelectEntity();

                    if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive() && _path.IsRotationPoint(_selectedEntity.Value))
                    {
                        _path.DestroyRotationPoint(_selectedEntity.Value);
                    }

                    _selectedEntity = null;
                    break;
                }
            case ToolType.MarkerAdd:
                if (_path.CanCreateMarker)
                {
                    EnsureUniqueMarkerName(_path.CreateMarker(MouseWorld));
                }
                break;
            case ToolType.MarkerDelete:
            {
                SelectEntity();

                if (_selectedEntity.HasValue && _selectedEntity.Value.IsAlive() && _path.IsMarker(_selectedEntity.Value))
                {
                    _path.DestroyMarker(_selectedEntity.Value);
                }

                _selectedEntity = null;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void EnsureUniqueMarkerName(Entity marker)
    {
        if (!marker.Has<MarkerComponent>())
        {
            return;
        }

        ref var component = ref marker.Get<MarkerComponent>();
        var name = component.Name;

        if (name.StartsWith("Marker ") && int.TryParse(name.Replace("Marker ", ""), out _))
        {
            name = "Marker";
        }

        var duplicates = 0;
        while (_path.MarkerPoints.Any(x => x != marker && x.Get<MarkerComponent>().Name.Equals(name)))
        {
            name = $"{component.Name} {++duplicates}";
        }

        component.Name = name;
    }

    private void ImGuiLayerOnSubmit(ImGuiRenderer obj)
    {
        if (_disposed)
        {
            return;
        }

        if (!IsEnabled)
        {
            return;
        }
        
        var hoveredMarkersEntities = _pathWorld.Clip(MouseWorld).Where(x => x.Has<MarkerComponent>()).ToArray();

        if (hoveredMarkersEntities.Length > 0)
        {
            var marker = hoveredMarkersEntities.First().Get<MarkerComponent>();
            var hit = _simulator.MarkerEvents.Any(x => x.Marker.Parameter == marker.Parameter);

            ImGui.BeginTooltip();
            ImGui.TextColored(hit ? HitMarkerColor : PendingMarkerColor, marker.Name);
            ImGui.EndTooltip();
        }

        const string imId = "MotionEditorLayer";

        if (ImGuiExt.Begin("Tools", imId))
        {
            ImGui.TextColored(new Vector4(1, 1, 1, 1), "Path Tools");
            ImGui.BeginGroup();

            var types = Enum.GetValues<ToolType>();
            for (var index = 0; index < types.Length; index++)
            {
                var value = types[index];
                ImGui.PushStyleColor(ImGuiCol.Button, _selectedTool == value ? new Vector4(0.3f, 0, 0, 0.8f) : new Vector4(0, 0, 0, 0f));

                if (ImGui.ImageButton(
                        ToolDescriptions[value],
                        _imGuiLayer.Renderer.GetOrCreateImGuiBinding(
                            _app.Resources.Factory,
                            _app.Resources.AssetManager.GetView(ToolTextures[value])),
                        new Vector2(32, 32), Vector2.Zero, new Vector2(1f, -1f)))
                {
                    _selectedTool = value;

                    ImGui.PopStyleColor();

                    break;
                }

                ImGui.PopStyleColor();

                if (index != types.Length - 1)
                {
                    ImGui.SameLine();
                }
            }

            ImGui.Text(ToolDescriptions[_selectedTool]);

            ImGui.EndGroup();
            ImGui.Separator();

            ImGui.TextColored(new Vector4(1, 1, 1, 1), "Review");
            ImGui.BeginGroup();

            if (ImGui.Button("Player"))
            {
                if (!_showPlayer)
                {
                    _app.ToastInfo("Opening simulation!");
                }

                _showPlayer = true;
            }

            ImGui.EndGroup();

            ImGui.Separator();

            if (ImGui.Button("Reverse Path"))
            {
                _path.ReversePath();
                _simulator.Generate();
            }

            ImGui.Separator();

            if (ImGui.Button("Clear"))
            {
                _pathWorld.Clear();
                _path.Clear();
                _selectedEntity = null;
            }
        }

        ImGui.End();

        if (ImGuiExt.Begin("Render", imId))
        {
            if (ImGui.Checkbox("Show Translation Points", ref _renderPositionPoints))
            {
                _selectedEntity = null;

                foreach (var pathTranslationPoint in _path.TranslationPoints)
                {
                    pathTranslationPoint.Get<SpriteComponent>().Disabled = !_renderPositionPoints;
                }
            }

            if (ImGui.Checkbox("Show Rotation Points", ref _renderRotationPoints))
            {
                _selectedEntity = null;

                foreach (var pathTranslationPoint in _path.RotationPoints)
                {
                    pathTranslationPoint.Get<SpriteComponent>().Disabled = !_renderRotationPoints;
                }
            }

            if (ImGui.Checkbox("Show Translation Velocities", ref _renderTranslationVelocityPoints))
            {
                _selectedEntity = null;

                foreach (var pathTranslationPoint in _path.TranslationPoints)
                {
                    pathTranslationPoint.Get<TranslationPointComponent>().VelocityMarker.Get<SpriteComponent>().Disabled = !_renderTranslationVelocityPoints;
                }
            }

            if (ImGui.Checkbox("Show Translation Accelerations", ref _renderTranslationAccelerationPoints))
            {
                _selectedEntity = null;

                foreach (var pathTranslationPoint in _path.TranslationPoints)
                {
                    pathTranslationPoint.Get<TranslationPointComponent>().AccelerationMarker.Get<SpriteComponent>().Disabled = !_renderTranslationAccelerationPoints;
                }
            }

            if (ImGui.Checkbox("Show Rotation Tangents", ref _renderRotationTangents))
            {
                _selectedEntity = null;

                foreach (var pathTranslationPoint in _path.RotationPoints)
                {
                    pathTranslationPoint.Get<RotationPointComponent>().HeadingMarker.Get<SpriteComponent>().Disabled = !_renderRotationTangents;
                }
            }
        }

        ImGui.End();

        if (ImGuiExt.Begin("Project", imId))
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
                            SaveProject();
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

        if (ImGuiExt.Begin("Inspector", imId))
        {
            if (_selectedEntity == null || !_selectedEntity.Value.IsAlive())
            {
                ImGui.Text("Nothing to show");
            }
            else
            {
                if (Inspector.SubmitEditor(_selectedEntity.Value))
                {
                    OnUserChange();
                }

                if (SubmitPointInspector(_selectedEntity.Value))
                {
                    OnUserChange();
                }
            }
        }

        ImGui.End();

        if (MotionEditorConfig.AxisMove)
        {
            ImGui.SetTooltip("Axis Move");
        }

        if (MotionEditorConfig.PolarMove)
        {
            ImGui.SetTooltip("Polar Move");
        }

        if (_showPlayer)
        {
            if (ImGuiExt.Begin("Player", imId, ref _showPlayer))
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

                var lastPoint = _simulator.Last;

                if (ImGui.Button("Re-generate"))
                {
                    _simulator.Generate();
                }

                ImGui.SameLine();
                ImGui.Text($"Points: {_simulator.Points}");

                if (ImGui.CollapsingHeader("Base Constraints"))
                {
                    ImGuiExt.InputDouble("Lin Vel(m/s)", ref _simulator.MaxLinearVelocity, step: 0.01, stepFast: 0.1, min: 10e-5);
                    ImGuiExt.InputDouble("Lin Accel(m/s²)", ref _simulator.MaxLinearAcceleration, step: 0.01, stepFast: 0.1, min: 10e-5);
                    ImGuiExt.InputDouble("Lin Deaccel(m/s²)", ref _simulator.MaxLinearDeacceleration, step: 0.01, stepFast: 0.1, min: 10e-5);
                    ImGuiExt.InputDouble("Centripetal Accel(m/s²/r)", ref _simulator.MaxCentripetalAcceleration, step: 0.01, stepFast: 0.1, min: 10e-5);
                    ImGuiExt.InputDegrees("Ang Vel(rad/s)", ref _simulator.MaxAngularVelocity, step: 1, stepFast: 10, minDeg: 10e-5);
                    ImGuiExt.InputDegrees("Ang Accel(rad/s²)", ref _simulator.MaxAngularAcceleration, step: 1, stepFast: 10, minDeg: 10e-5);
                }

                if (ImGui.CollapsingHeader("Kinematic Analysis"))
                {
                    ImGui.TextColored(TimeColor, $"{lastPoint.Time:F4} s ({_simulator.TotalTime:F4} s total)");

                    ImGui.TextColored(VelocityColor, $"{lastPoint.Velocity.Length:F4}/{_simulator.MaxProfileVelocity:F4} m/s");
                    ImGui.Checkbox("Show Velocity", ref _renderPlayerVelocity);
                    ImGui.Separator();

                    ImGui.TextColored(AccelerationColor, $"{lastPoint.Acceleration.Length:F4}/{_simulator.MaxProfileAcceleration:F4} m/s²");
                    ImGui.Checkbox("Show Acceleration", ref _renderPlayerAcceleration);
                    ImGui.Separator();

                    ImGui.TextColored(AngularVelocityColor, $"{lastPoint.AngularVelocity:F4}/{_simulator.MaxProfileAngularVelocity:F4} rad/s");
                    ImGui.Separator();

                    ImGui.TextColored(AngularAccelerationColor, $"{lastPoint.AngularAcceleration:F4}/{_simulator.MaxProfileAngularAcceleration:F4} rad/s²");
                    ImGui.Separator();

                    ImGui.TextColored(DisplacementColor, $"{lastPoint.Displacement:F4} m ({_simulator.TotalLength:F4} m total)");
                    ImGui.Separator();
                }

                if (ImGui.CollapsingHeader("Playback"))
                {
                    ImGui.SliderFloat("Playback Speed", ref _simulator.Speed, 0f, 10f);

                    if (_simulator.TotalTime > 0)
                    {
                        var playTime = _simulator.PlayTime;

                        if (ImGui.SliderFloat("Scrub Timeline", ref playTime, 0f, _simulator.TotalTime))
                        {
                            // Prevent ImGui rounding causing the simulator to reset:
                            _simulator.PlayTime = Math.Clamp(playTime, 0f, _simulator.TotalTime);
                        }
                    }

                    if (ImGui.Button("Normal"))
                    {
                        _simulator.Speed = 1;
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Pause"))
                    {
                        _simulator.Speed = 0;
                    }

                    if (ImGui.Button("0.75"))
                    {
                        _simulator.Speed = 0.75f;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("0.5"))
                    {
                        _simulator.Speed = 0.5f;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("0.25"))
                    {
                        _simulator.Speed = 0.25f;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("0.1"))
                    {
                        _simulator.Speed = 0.1f;
                    }
                }

                if (ImGui.CollapsingHeader("Marker Events"))
                {
                    ImGui.BeginGroup();

                    foreach (var @event in _simulator.MarkerEvents)
                    {
                        ImGui.Text(@event.Marker.Label);
                        ImGui.Text($"T+{@event.HitTime:F4}s");
                        ImGui.Separator();
                    }

                    ImGui.EndGroup();
                }

                if (ImGui.CollapsingHeader("Path Generation"))
                {
                    // I don't like these names very much, but the "proper" full name is super clunky.
                    ImGuiExt.InputTwist2dIncr("Path Incr(m, deg)", ref _simulator.PathIncr, min: 10e-5, format: "%f");
                    ImGuiExt.InputDouble("Path T Incr(%)", ref _simulator.PathParamIncr, min: 10e-5, max: 1, format: "%f");
                    ImGuiExt.InputDegrees("Rot Incr(deg)", ref _simulator.SplineRotIncr, minDeg: 10e-5, format: "%f");
                    ImGuiExt.InputDouble("Rot T Incr(%)", ref _simulator.RotParamIncr, min: 10e-5, max: 1, format: "%f");
                }
            }

            ImGui.End();
        }
    }

    private bool SubmitPointInspector(Entity point)
    {
        var pointPosWorld = point.Position();

        var changed = false;

        void SubmitAngleControl(Entity markerEntity, string label)
        {
            var markerPosWorld = markerEntity.Get<PositionComponent>().Position;
            var actualPosActual = markerPosWorld - pointPosWorld;
            var actualDistance = actualPosActual.Length();

            if (actualDistance > 0)
            {
                var actualRotation = Rotation2d.Dir(actualPosActual);
                var tangentAngle = Angles.ToDegrees((float)actualRotation.Log());

                if (ImGui.SliderFloat(label, ref tangentAngle, -180, 180))
                {
                    markerEntity.Move(pointPosWorld + actualDistance * (Vector2)Rotation2d.Exp(Angles.ToRadians(tangentAngle)).Direction);
                  
                    changed = true;
                }
            }
        }

        void SubmitAlignControl(Entity marker, string label, Entity targetPoint, Entity targetMarker)
        {
            if (ImGui.Button(label))
            {
                var targetPosActual = targetMarker.Position() - targetPoint.Position();

                marker.Move(point.Position() + targetPosActual);
            }
        }

        if (point.Has<TranslationPointComponent>())
        {
            var tangentMarker = point.Get<TranslationPointComponent>().VelocityMarker;
            SubmitAngleControl(tangentMarker, "Tangent Angle");

            var index = Array.FindIndex(_path.TranslationPoints.ToArray(), e => e == point);

            if (index > 0)
            {
                SubmitAlignControl(
                    tangentMarker,
                    "Pre-align tangent",
                    _path.TranslationPoints[index - 1],
                    _path.TranslationPoints[index - 1].Get<TranslationPointComponent>().VelocityMarker);
            }
        }

        if (point.Has<RotationPointComponent>())
        {
            var rotationMarker = point.Get<RotationPointComponent>().HeadingMarker;
            SubmitAngleControl(rotationMarker, "Heading Angle");

            var index = Array.FindIndex(_path.RotationPoints.ToArray(), e => e == point);

            if (index > 0)
            {
                SubmitAlignControl(
                    rotationMarker, 
                    "Pre-align heading", 
                    _path.RotationPoints[index - 1],
                    _path.RotationPoints[index - 1].Get<RotationPointComponent>().HeadingMarker);
            }
        }

        return changed;
    }

    private void OnUserChange()
    {
        var entity = Assert.NotNull(_selectedEntity);
        Assert.IsTrue(entity.IsAlive());
        EnsureUniqueMarkerName(entity);
    }

    private void SaveProject()
    {
        var motionProject = MotionProject.FromPath(_path);

        motionProject.Version = _path.Version;

        var baseConstraints = _simulator.Constraints;

        motionProject.Constraints = new JsonMotionConstraints
        {
            LinearVelocity = baseConstraints.LinearVelocity,
            LinearAcceleration = baseConstraints.LinearAcceleration,
            LinearDeacceleration = baseConstraints.LinearDeacceleration,
            AngularVelocity = baseConstraints.AngularVelocity,
            AngularAcceleration = baseConstraints.AngularAcceleration,
            CentripetalAcceleration = baseConstraints.CentripetalAcceleration
        };

        motionProject.Parameters = new JsonGenerationParameters
        {
            Dx = _simulator.PathIncr.TrIncr.X,
            Dy = _simulator.PathIncr.TrIncr.Y,
            DAngleTranslation = _simulator.PathIncr.RotIncr,
            DParameterTranslation = _simulator.PathParamIncr,
            DAngleRotation = _simulator.SplineRotIncr,
            DParameterRotation = _simulator.RotParamIncr,
        };

        _app.Project.MotionProjects[_motionProjectName] = motionProject;
        _app.Project.SetChanged(motionProject);

        _app.Project.Save();

        _app.ToastInfo($"Saved motion {_motionProjectName}");
    }

    private void LoadProject(string motionProjectName)
    {
        var motionProject = _app.Project.MotionProjects[motionProjectName];

        _pathWorld.Clear();
        _selectedEntity = null;

        motionProject.Load(_path);

        _simulator.MaxLinearVelocity = motionProject.Constraints.LinearVelocity;
        _simulator.MaxLinearAcceleration = motionProject.Constraints.LinearAcceleration;
        _simulator.MaxLinearDeacceleration = motionProject.Constraints.LinearDeacceleration;
        _simulator.MaxCentripetalAcceleration = motionProject.Constraints.CentripetalAcceleration;
        _simulator.MaxAngularVelocity = motionProject.Constraints.AngularVelocity;
        _simulator.MaxAngularAcceleration = motionProject.Constraints.AngularAcceleration;

        var parameters = motionProject.Parameters;
        _simulator.PathIncr = new Twist2dIncr(motionProject.Parameters.Dx, motionProject.Parameters.Dy, parameters.DAngleTranslation);
        _simulator.PathParamIncr = parameters.DParameterTranslation;
        _simulator.SplineRotIncr = parameters.DAngleRotation;
        _simulator.RotParamIncr = parameters.DParameterRotation;
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
        _path.SubmitPath(_playerBatch);
        _playerBatch.Submit(framebuffer: framebuffer);

        if (_simulator.Update(_dt, out var pose))
        {
            _playerBatch.Clear();
            _playerBatch.TexturedQuad(pose.Translation, Vector2.One * 0.4f, (float)pose.Rotation.Log(), _robotSprite.Texture);
            _playerBatch.Submit(framebuffer: framebuffer);

            void Arrow(Vector2 start, Vector2 end, RgbaFloat tint)
            {
                _playerBatch.Clear();

                var effect = _playerBatch.Effects;
                _playerBatch.Effects = effect with { Tint = tint };

            
                var angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);

                const float thickness = 0.1f;

                _playerBatch.TexturedQuad(end, new Vector2(thickness), angle, _arrowSprite.Texture);
                _playerBatch.Line(start, end, RgbaFloat4.White, thickness * (127f / 1024 /*calculated from texture size*/));

                _playerBatch.Submit(framebuffer: framebuffer);
                _playerBatch.Effects = effect;
            }

            if (_renderPlayerVelocity)
            {
                Arrow(pose.Translation, pose.Translation + _simulator.Last.Velocity / 10, new RgbaFloat(VelocityColor));
            }

            if (_renderPlayerAcceleration)
            {
                Arrow(pose.Translation, pose.Translation + _simulator.Last.Acceleration / 10, new RgbaFloat(AccelerationColor));
            }
        }
    }

    private void UpdateCamera(FrameInfo frameInfo)
    {
        if (!_imGuiLayer.Captured)
        {
            foreach (var inputDownKey in _app.Input.DownKeys)
            {
                _cameraController.ProcessKey(inputDownKey,
                    MotionEditorConfig.MoveSpeed * frameInfo.DeltaTime,
                    0);
            }

            _cameraController.FutureZoom += _app.Input.ScrollDelta * MotionEditorConfig.ZoomSpeed * frameInfo.DeltaTime;
            _cameraController.FutureZoom = Math.Clamp(_cameraController.FutureZoom, MotionEditorConfig.MinZoom, MotionEditorConfig.MaxZoom);
        }

        _cameraController.Update(frameInfo.DeltaTime);
    }

    private void UpdateSelection()
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
        var position = MouseWorld + _selectPoint;
        var component = entity.Get<PositionComponent>();

        if (component.ControlledMoveCallback != null)
        {
            position = component.ControlledMoveCallback(entity, position, _app.Input);
        }

        entity.Move(position);
    }

    protected override void Update(FrameInfo frameInfo)
    {
        _dt = frameInfo.DeltaTime;

        UpdateCamera(frameInfo);
        UpdateSelection();
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

        _path.SubmitPath(_editorBatch);
        Draw();
        Systems.RenderConnections(
            _pathWorld,
            _editorBatch,
            _renderPositionPoints,
            _renderRotationPoints,
            _renderTranslationVelocityPoints,
            _renderTranslationAccelerationPoints,
            _renderRotationTangents);
        Draw();
        _path.SubmitIndicator(_editorBatch, MouseWorld);
        Draw();
        Systems.RenderSprites(_pathWorld, _editorBatch);
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

    public override string ToString()
    {
        return $"Motion | {_motionProjectName}";
    }

    public void Save()
    {
        SaveProject();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _imGuiLayer.Submit -= ImGuiLayerOnSubmit;
        _app.Resources.BatchPool.Return(_editorBatch);
        _app.Resources.BatchPool.Return(_playerBatch);
        _editorProcessor.Dispose();
        _playerTexture?.Dispose();
        _playerFramebuffer?.Dispose();
        _commandList.Dispose();
        _path.Dispose();
        _simulator.Dispose();

        ArchWorld.Return(_pathWorld);
    }
}