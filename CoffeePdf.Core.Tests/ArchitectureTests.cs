using CoffeePdf.Core.Files;

namespace CoffeePdf.Core.Tests;

/// <summary>Spec §4.3 and §14 item 8: the core stays free of Avalonia, so its logic never depends on the PdfViewer.</summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void The_core_references_no_Avalonia_assembly()
    {
        var references = typeof(PdfFile).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
