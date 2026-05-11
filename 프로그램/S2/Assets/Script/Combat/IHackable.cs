public interface IHackable
{
    HackableData HackData { get; }
    void OnHackReady();
    void OnHackStarted();
    void OnHackCompleted();
    void OnHackCanceled();
}
