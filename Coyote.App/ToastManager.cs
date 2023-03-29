using System.Diagnostics;
using System.Numerics;
using GameFramework.Extensions;
using GameFramework.Renderer;
using GameFramework.Renderer.Batch;

namespace Coyote.App;

internal enum ToastNotificationType
{
    Information,
    Error
}

internal class ToastNotification
{
    public ToastNotificationType Type { get; }
    public string Message { get; }
    public double Duration { get; }

    public double AddedTimestamp { get; set; }

    public ToastNotification(ToastNotificationType type, string message, double duration = 2)
    {
        Type = type;
        Message = message;
        Duration = duration;

        DisplayedString = $"{Type}!\n{Message}";
    }

    public string DisplayedString { get; }
}

internal class ToastManager
{
    private const float DrawMargin = 0.01f;
    private const float BorderThickness = 0.005f;
    private const float YOffset = 0.01f;
    
    private static readonly Vector4 BorderColor = new(0.1f, 0.3f, 0.5f, 0.2f);
    private static readonly Vector4 InitialColor = new(1, 0.2f, 0.4f, 0.95f);
    private static readonly Vector4 FinalColor = new(0.1f, 1f, 0f, 0.1f);

    private readonly App _app;
    private readonly List<ToastNotification> _notifications = new();
    private readonly Queue<ToastNotification> _removeQueue = new();
    private readonly Stopwatch _stopWatch = Stopwatch.StartNew();

    public void Remove(ToastNotification notification)
    {
        _notifications.Remove(notification);
    }

    public void ResetTimer(ToastNotification notification)
    {
        notification.AddedTimestamp = _stopWatch.Elapsed.TotalSeconds;
    }

    public ToastManager(App app)
    {
        _app = app;
    }

    public void Add(ToastNotification notification)
    {
        _notifications.Add(notification);

        ResetTimer(notification);
    }

    public void Render(QuadBatch batch, float fontSize, Vector2 startPos, float edgeX)
    {
        var font = _app.Font;

        var y = startPos.Y;

        foreach (var toastNotification in _notifications)
        {
            var elapsed = _stopWatch.Elapsed.TotalSeconds - toastNotification.AddedTimestamp;

            if (elapsed > toastNotification.Duration)
            {
                _removeQueue.Enqueue(toastNotification);
                continue;
            }

            var progress = (float)(elapsed / toastNotification.Duration);

            var size = font.MeasureText(toastNotification.DisplayedString, fontSize) * 1.1f;
            var position = new Vector2(edgeX - size.X - DrawMargin, y);

            batch.Quad(position, size, Vector4.Lerp(InitialColor, FinalColor, progress), align: AlignMode.TopLeft);

            font.Render(batch, position + new Vector2(DrawMargin, DrawMargin), toastNotification.DisplayedString, size: fontSize);

            // upper border
            batch.Quad(
                new Vector2(position.X, position.Y),
                new Vector2(size.X + BorderThickness, BorderThickness),
                BorderColor, align: AlignMode.TopLeft);

            // lower border
            batch.Quad(
                new Vector2(position.X, position.Y - size.Y),
                new Vector2(size.X + BorderThickness, BorderThickness),
                BorderColor, align: AlignMode.TopLeft);

            // left border
            batch.Quad(
                new Vector2(position.X, position.Y),
                new Vector2(BorderThickness, size.Y + BorderThickness),
                BorderColor, align: AlignMode.TopLeft);

            // right border
            batch.Quad(
                new Vector2(position.X + size.X, position.Y),
                new Vector2(BorderThickness, size.Y + BorderThickness),
                BorderColor, align: AlignMode.TopLeft);


            y += size.Y + YOffset;
        }

        while (_removeQueue.TryDequeue(out var removed))
        {
            _notifications.Remove(removed);
        }
    }
}