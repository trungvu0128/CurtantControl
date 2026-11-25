using System;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services;

public enum Razr3Direction { Outside, Room }
public enum Razr3Stage { MovingCamera, Settling, ApplyingBackground }

public sealed class Razr3SceneController
{
    public Razr3Direction? SettledDirection { get; private set; }
    public int? CompletedScene { get; private set; }

    public void InvalidateScene() => CompletedScene = null;

    public void Reset()
    {
        SettledDirection = null;
        CompletedScene = null;
    }

    public async Task ApplyAsync(int sceneId, RazrConfiguration motor, Func<double, CancellationToken, Task> moveCamera,
        Func<int, CancellationToken, Task> applyBackground, Action<Razr3Stage> reportStage, CancellationToken token)
    {
        if (sceneId is < 0 or > Razr3Configuration.OutsideScene)
            throw new ArgumentOutOfRangeException(nameof(sceneId));
        var direction = sceneId == Razr3Configuration.OutsideScene ? Razr3Direction.Outside : Razr3Direction.Room;
        CompletedScene = null;
        token.ThrowIfCancellationRequested();
        if (SettledDirection != direction)
        {
            SettledDirection = null;
            reportStage(Razr3Stage.MovingCamera);
            await moveCamera(direction == Razr3Direction.Outside ? motor.Position1Angle : motor.Position2Angle, token);
            token.ThrowIfCancellationRequested();
            reportStage(Razr3Stage.Settling);
            await Task.Delay(TimeSpan.FromSeconds(motor.SettleSeconds), token);
            token.ThrowIfCancellationRequested();
            SettledDirection = direction;
        }
        if (direction == Razr3Direction.Room)
        {
            reportStage(Razr3Stage.ApplyingBackground);
            token.ThrowIfCancellationRequested();
            await applyBackground(sceneId, token);
        }
        token.ThrowIfCancellationRequested();
        CompletedScene = sceneId;
    }
}
