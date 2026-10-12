namespace Dictor.Sessions;

internal enum Phase { Starting, Ready, Recording, Recognizing, Delivering, Stopping }
internal sealed class SessionState
{
    public Phase Phase { get; private set; } = Phase.Starting;
    public long Id { get; private set; }
    public void Ready() => Phase = Phase.Ready;
    public bool Begin()
    {
        if (Phase != Phase.Ready) return false;
        Id++; Phase = Phase.Recording; return true;
    }
    public bool Stop()
    {
        if (Phase != Phase.Recording) return false;
        Phase = Phase.Recognizing; return true;
    }
    public bool Deliver(long id)
    {
        if (id != Id || Phase != Phase.Recognizing) return false;
        Phase = Phase.Delivering; return true;
    }
    public void Cancel() { Id++; Phase = Phase.Stopping; }
}
