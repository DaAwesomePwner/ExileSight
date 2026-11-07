using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ExileSight.Platform.Windows.Tests")]

namespace ExileSight.Platform.Windows.Window;

internal interface IGameWindowHooks : IDisposable
{
    void Start();
    void ScopeTo(uint pid);
}
