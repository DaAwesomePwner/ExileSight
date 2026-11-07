using Xunit;

namespace ExileSight.Architecture.Tests;

public sealed class FeatureNetworkTests
{
    [Fact]
    public void FeaturesUseHostServicesInsteadOfDirectNetworkAccess()
    {
        var projects = AssemblyInventory.Projects.Where(Repository.IsProduction)
            .Where(p => Path.GetFileName(p).StartsWith("ExileSight.Features.", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(projects);
        foreach (var project in projects)
            AssemblyInventory.Read(AssemblyInventory.Output(project), reader =>
            {
                foreach (var handle in reader.TypeReferences)
                {
                    var type = reader.GetTypeReference(handle);
                    var ns = reader.GetString(type.Namespace);
                    Assert.False(ns == "System.Net" || ns.StartsWith("System.Net.", StringComparison.Ordinal),
                        $"Feature directly uses network type {ns}.{reader.GetString(type.Name)} in {project}; use a host service");
                }
            });
    }
}
