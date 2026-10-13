namespace Neirijnck.Transfr.Core.Tests;

public sealed class CoreAssemblyTests
{
    [Fact]
    public void Core_DoesNotReferenceUmbraco()
    {
        var references = typeof(CoreAssemblyMarker).Assembly.GetReferencedAssemblies();

        var umbraco = references.Where(r => r.Name?.StartsWith("Umbraco", StringComparison.OrdinalIgnoreCase) is true).ToList();

        Assert.True(umbraco.Count == 0, $"Core must stay free of Umbraco: {string.Join(", ", umbraco.Select(r => r.Name))}");
    }
}