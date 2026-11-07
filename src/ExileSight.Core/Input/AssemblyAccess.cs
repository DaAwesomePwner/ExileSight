using System.Runtime.CompilerServices;

// Only the native hook pipeline and tests may report physical events or mint tokens.
[assembly: InternalsVisibleTo("ExileSight.Platform.Windows")]
[assembly: InternalsVisibleTo("ExileSight.Core.Tests")]
[assembly: InternalsVisibleTo("ExileSight.Platform.Windows.Tests")]
