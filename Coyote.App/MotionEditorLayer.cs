using System.Drawing;
using System.Numerics;
using GameFramework;
using GameFramework.Assets;
using GameFramework.Extensions;
using GameFramework.ImGui;
using GameFramework.Layers;
using GameFramework.PostProcessing;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;
using GameFramework.Scene;
using Veldrid;

namespace Coyote.App;

internal class MotionEditorLayer : Layer, ITabStyle
{
    public const float FieldSize = 3.66f;

    private const float MoveSpeed = 2f;
    private const float ZoomSpeed = 25;
    private const float MinZoom = 1f;
    private const float MaxZoom = 5f;

    private readonly GameApplication _app;
    private readonly ImGuiRenderer _imGui;
    private readonly QuadBatch _batch;

    private readonly PostProcessor _processor;
    private readonly CommandList _commandList;

    private readonly Sprite _fieldSprite;
    private readonly Sprite _robotSprite;

    private readonly OrthographicCameraController2D _cameraController;

    public MotionEditorLayer(GameApplication app, ImGuiLayer imGuiLayer)
    {
        _app = app;
        _imGui = imGuiLayer.Renderer;

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

        UpdatePipeline();
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
    }

    private void RenderBackground()
    {
        _batch.Clear();
        _batch.TexturedQuad(Vector2.Zero, Vector2.One * FieldSize, _fieldSprite.Texture);
        _batch.Submit(framebuffer: _processor.InputFramebuffer);
    }

    protected override void Update(FrameInfo frameInfo)
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

    protected override void Render(FrameInfo frameInfo)
    {
        _batch.Effects = QuadBatchEffects.Transformed(_cameraController.Camera.CameraMatrix);

        _processor.ClearColor();
        
        RenderBackground();

        _processor.Render();
    }
}