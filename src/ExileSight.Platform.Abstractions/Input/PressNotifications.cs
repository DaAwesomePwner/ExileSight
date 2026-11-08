namespace ExileSight.Platform.Abstractions.Input;

// Identity and hook timestamps only: release never carries game-action authority.
public sealed record PressStarted(long Id, long Timestamp);
public sealed record PressReleased(long Id, long DownTimestamp, long UpTimestamp);
public interface IPressCommandHandler : IFeatureCommandHandler
{
    ActionResult Press(IntentToken? token, PressStarted press);
    void Release(PressReleased release);
}
