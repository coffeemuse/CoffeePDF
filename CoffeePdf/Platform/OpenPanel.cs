using System.Runtime.InteropServices;

namespace CoffeePdf.Platform;

/// <summary>
/// The Mac's Open panel on its own, for File › Open… with no window to show it on, as Preview shows it. Avalonia shows
/// its Open panel only as a sheet on a window. Its macOS library shows a panel on its own when there's no window, with
/// <c>beginWithCompletionHandler:</c>, but nothing in Avalonia asks it to, so this asks AppKit the same way. The app
/// runs on while the panel is up.
/// </summary>
internal static unsafe partial class OpenPanel
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";
    private const string UniformTypeIdentifiers = "/System/Library/Frameworks/UniformTypeIdentifiers.framework/UniformTypeIdentifiers";
    private const nint ModalResponseOK = 1;

    // Clang's block flags (its Block-ABI-Apple): a global block, which is never copied or freed, with a type signature.
    private const int BlockIsGlobal = 1 << 28;
    private const int BlockHasSignature = 1 << 30;

    private static Block* _completion; // the panel's completion handler, made once
    private static nint _panel;        // the panel up, retained until it's answered
    private static TaskCompletionSource<IReadOnlyList<string>>? _choosing;

    /// <summary>
    /// Shows the panel for PDFs, several at a time, and answers with the paths chosen, or none if it was cancelled.
    /// While it's up already, it comes forward, and this answers none.
    /// </summary>
    public static Task<IReadOnlyList<string>> PickPdfsAsync()
    {
        if (!OperatingSystem.IsMacOS())
            return Task.FromResult<IReadOnlyList<string>>([]);
        if (_choosing is not null)
        {
            Send(_panel, "makeKeyAndOrderFront:", 0);
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        if (_completion is null)
            _completion = MakeCompletion();

        var panel = Send(Class("NSOpenPanel"), "openPanel");
        Send(panel, "retain");
        Send(panel, "setAllowsMultipleSelection:", 1);
        Send(panel, "setCanChooseDirectories:", 0);
        // UTType's framework is loaded with AppKit; should it not be, the panel shows every file, and Open skips
        // whatever isn't a PDF.
        NativeLibrary.TryLoad(UniformTypeIdentifiers, out _);
        if (Class("UTType") is var utType and not 0
            && Send(utType, "typeWithIdentifier:", objc_msgSend(Class("NSString"), sel_registerName("stringWithUTF8String:"), "com.adobe.pdf")) is var pdf and not 0)
            Send(panel, "setAllowedContentTypes:", Send(Class("NSArray"), "arrayWithObject:", pdf));

        _panel = panel;
        _choosing = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosing = _choosing.Task;
        Send(panel, "beginWithCompletionHandler:", (nint)_completion);
        return choosing;
    }

    /// <summary>The completion handler, which AppKit calls on the main thread once the panel is answered.</summary>
    [UnmanagedCallersOnly]
    private static void Completed(nint block, nint response)
    {
        var (panel, choosing) = (_panel, _choosing);
        _panel = 0;
        _choosing = null;
        var paths = new List<string>();
        try
        {
            if (response == ModalResponseOK)
            {
                var urls = Send(panel, "URLs");
                var count = Send(urls, "count");
                for (nint i = 0; i < count; i++)
                    if (Marshal.PtrToStringUTF8(Send(Send(Send(urls, "objectAtIndex:", i), "path"), "UTF8String")) is { } path)
                        paths.Add(path);
            }
        }
        finally
        {
            // Nothing may throw back into AppKit, which would end the app: the paths read so far are answered.
            Send(panel, "release");
            choosing?.TrySetResult(paths);
        }
    }

    /// <summary>A global block calling <see cref="Completed"/>: <c>void (^)(NSModalResponse)</c>.</summary>
    private static Block* MakeCompletion()
    {
        var descriptor = (BlockDescriptor*)NativeMemory.AllocZeroed((nuint)sizeof(BlockDescriptor));
        descriptor->Size = (nuint)sizeof(Block);
        descriptor->Signature = (byte*)Marshal.StringToCoTaskMemUTF8("v16@?0q8");
        var block = (Block*)NativeMemory.AllocZeroed((nuint)sizeof(Block));
        block->Isa = NativeLibrary.GetExport(NativeLibrary.Load("/usr/lib/libSystem.B.dylib"), "_NSConcreteGlobalBlock");
        block->Flags = BlockIsGlobal | BlockHasSignature;
        block->Invoke = &Completed;
        block->Descriptor = descriptor;
        return block;
    }

    private static nint Class(string name) => objc_getClass(name);

    private static nint Send(nint receiver, string selector) => objc_msgSend(receiver, sel_registerName(selector));

    private static nint Send(nint receiver, string selector, nint argument) => objc_msgSend(receiver, sel_registerName(selector), argument);

    [StructLayout(LayoutKind.Sequential)]
    private struct Block
    {
        public nint Isa;
        public int Flags;
        public int Reserved;
        public delegate* unmanaged<nint, nint, void> Invoke;
        public BlockDescriptor* Descriptor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public nuint Reserved;
        public nuint Size;
        public byte* Signature;
    }

    [LibraryImport(ObjectiveC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_getClass(string name);

    [LibraryImport(ObjectiveC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport(ObjectiveC)]
    private static partial nint objc_msgSend(nint receiver, nint selector);

    [LibraryImport(ObjectiveC)]
    private static partial nint objc_msgSend(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjectiveC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_msgSend(nint receiver, nint selector, string argument);
}
