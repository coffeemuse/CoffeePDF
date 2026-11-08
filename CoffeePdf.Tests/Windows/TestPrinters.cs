using Avalonia.Controls.Pdf.Core;
using Avalonia.Controls.Pdf.Services;

namespace CoffeePdf.Tests.Windows;

/// <summary>Print services for the tests: one that fails, and one that records what it was asked to print.</summary>
internal sealed class FailingPrinter : IPrintService
{
    public bool IsSupported => true;

    public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No printer");

    public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
}

internal sealed class FakePrinter : IPrintService
{
    public List<string?> Titles { get; } = [];
    public bool IsSupported => true;

    public Task PrintAsync(IPdfDocument document, PrintOptions? options = null, CancellationToken cancellationToken = default)
    {
        Titles.Add(options?.Title);
        return Task.CompletedTask;
    }

    public Task ShowPrintPreviewAsync(IPdfDocument document, PrintOptions? options = null) => Task.CompletedTask;
}
