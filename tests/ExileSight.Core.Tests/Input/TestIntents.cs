using System.Runtime.CompilerServices;
using ExileSight.Core.Input;
using ExileSight.Platform.Abstractions.Input;
using ExileSight.Platform.Abstractions.Time;

namespace ExileSight.Core.Tests.Input;

internal static class TestIntents
{
    private static readonly ConditionalWeakTable<IntentRegistry, object> Keys = new();
    internal static IntentRegistry Create(IClock clock)
    {
        var key = new object();
        var registry = new IntentRegistry(clock, key);
        Keys.Add(registry, key);
        return registry;
    }
    internal static void Authorize(HotkeyDispatcher dispatcher)
    {
        var registry = dispatcher.Registry;
        dispatcher.InstallIssuer((time, id, action) => registry.Register(Keys.GetValue(registry, _ => new object()), time, id, action));
    }
    internal static IntentToken IssueForTest(this HotkeyDispatcher dispatcher, PhysicalPress press)
    {
        Authorize(dispatcher);
        return dispatcher.Issue(press);
    }
}
