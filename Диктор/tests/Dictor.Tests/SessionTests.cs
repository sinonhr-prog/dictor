using Dictor.Sessions;
using Dictor.Settings;
using Dictor.Windows;
using Xunit;

namespace Dictor.Tests;

public class SessionTests
{
    [Fact]
    public void RepeatedStopAndLateResultDoNotDeliver()
    {
        var state = new SessionState();
        Assert.False(state.Begin()); state.Ready(); Assert.True(state.Begin());
        var id = state.Id;
        Assert.False(state.Begin()); Assert.True(state.Stop()); Assert.False(state.Stop());
        state.Cancel(); Assert.False(state.Deliver(id)); Assert.False(state.Begin());
        state.Ready(); Assert.True(state.Begin()); Assert.True(state.Stop()); Assert.True(state.Deliver(state.Id));
        Assert.False(state.Deliver(state.Id));
    }
    [Fact]
    public void UnknownOrReusedWindowCannotMatch()
    {
        var target = new Target(1, 2, 3, [4, 5]);
        Assert.True(Desktop.Matches(target, new Target(1, 2, 3, [4, 5])));
        Assert.False(Desktop.Matches(target, null));
        Assert.False(Desktop.Matches(target, target with { Started = 4 }));
        Assert.False(Desktop.Matches(target, target with { Element = [4, 6] }));
        Assert.False(Desktop.Matches(target, target with { ProcessId = 7 }));
    }
    [Fact]
    public void CorruptSettingsRecoverLastGoodCopy()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            new Preferences { Paste = false }.Save(path);
            new Preferences { Paste = true }.Save(path);
            File.WriteAllText(path, "{corrupt");
            var restored = Preferences.Load(path, out var recovered);
            Assert.True(recovered); Assert.False(restored.Paste);
        }
        finally { Directory.Delete(directory, true); }
    }
}
