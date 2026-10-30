using System.Runtime.InteropServices;
using System.Text;

namespace CoffeePdf.Core.Pdf;

/// <summary>
/// The PDFium functions CoffeePDF calls itself, for what the PdfViewer doesn't do (spec §4.3, §16 Decision 1). They
/// bind to the <c>libpdfium</c> the PdfViewer ships (bblanchon.PDFium 157), so the process holds one copy.
/// </summary>
/// <remarks>
/// PDFium isn't thread-safe. <see cref="Gate"/> serialises CoffeePDF's own calls. The PdfViewer serialises its own per
/// document, and nothing joins the two, so CoffeePDF keeps its calls few: a save's, on the pages it needs. PDFium
/// ignores <c>FPDF_InitLibrary</c> once it's initialised, and in the app the PdfViewer initialises it first, with its
/// own configuration. Nothing here calls <c>FPDF_DestroyLibrary</c>, which would pull PDFium from under the PdfViewer.
/// </remarks>
internal static unsafe partial class Pdfium
{
    private const string Library = "pdfium";

    /// <summary><c>FPDF_INCREMENTAL</c>: a save that appends to the original bytes, so a signature over them holds.</summary>
    private const uint Incremental = 1;

    public static readonly Lock Gate = new();

    private static bool s_initialised;

    /// <summary>Initialises PDFium, which does nothing if the PdfViewer has. Call under <see cref="Gate"/>.</summary>
    public static void Initialise()
    {
        if (s_initialised)
            return;
        FPDF_InitLibrary();
        s_initialised = true;
    }

    /// <summary>Whether <paramref name="annotation"/>'s dictionary has <paramref name="key"/> (null-terminated ASCII).</summary>
    public static bool HasKey(nint annotation, ReadOnlySpan<byte> key)
    {
        fixed (byte* k = key)
            return FPDFAnnot_HasKey(annotation, k) != 0;
    }

    /// <summary><paramref name="annotation"/>'s string for <paramref name="key"/> (null-terminated ASCII), or null if it has none or it's empty.</summary>
    public static string? GetString(nint annotation, ReadOnlySpan<byte> key)
    {
        fixed (byte* k = key)
        {
            // The length is in bytes of UTF-16, with its two-byte terminator: 2 is empty or missing.
            var length = (int)FPDFAnnot_GetStringValue(annotation, k, null, new CULong(0)).Value;
            if (length <= 2)
                return null;
            var buffer = new char[length / 2];
            fixed (char* b = buffer)
                FPDFAnnot_GetStringValue(annotation, k, b, new CULong((nuint)length));
            return new string(buffer, 0, buffer.Length - 1);
        }
    }

    /// <summary>Sets <paramref name="annotation"/>'s string for <paramref name="key"/> (null-terminated ASCII).</summary>
    public static bool SetString(nint annotation, ReadOnlySpan<byte> key, string value)
    {
        // A string's characters are followed by a null in memory, which PDFium's wide string needs.
        fixed (byte* k = key)
        fixed (char* v = value)
            return FPDFAnnot_SetStringValue(annotation, k, v) != 0;
    }

    /// <summary>Writes <paramref name="document"/> as an incremental update.</summary>
    public static byte[] SaveIncremental(nint document)
    {
        using var output = new MemoryStream();
        var target = GCHandle.Alloc(output);
        try
        {
            var writer = new FileWrite { Version = 1, WriteBlock = &WriteBlock, Target = GCHandle.ToIntPtr(target) };
            if (FPDF_SaveAsCopy(document, &writer, new CULong(Incremental)) == 0)
                throw new IOException($"PDFium couldn't write the document (error {FPDF_GetLastError().Value})");
            return output.ToArray();
        }
        finally
        {
            target.Free();
        }
    }

    /// <summary><c>FPDF_FILEWRITE</c>, and after PDFium's two fields the stream it writes to, found through <c>pThis</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct FileWrite
    {
        public int Version;
        public delegate* unmanaged<FileWrite*, void*, CULong, int> WriteBlock;
        public nint Target;
    }

    [UnmanagedCallersOnly]
    private static int WriteBlock(FileWrite* self, void* data, CULong size)
    {
        try
        {
            var output = (MemoryStream)GCHandle.FromIntPtr(self->Target).Target!;
            output.Write(new ReadOnlySpan<byte>(data, checked((int)size.Value)));
            return 1;
        }
        catch (Exception) // nothing may unwind into PDFium: a 0 fails the save instead
        {
            return 0;
        }
    }

    /// <summary><paramref name="text"/> as null-terminated UTF-8, or null for null.</summary>
    public static byte[]? Utf8(string? text) => text is null ? null : Encoding.UTF8.GetBytes(text + '\0');

    [LibraryImport(Library)] private static partial void FPDF_InitLibrary();
    [LibraryImport(Library)] public static partial nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);
    [LibraryImport(Library)] public static partial nint FPDF_LoadDocument(byte* path, byte* password);
    [LibraryImport(Library)] public static partial void FPDF_CloseDocument(nint document);
    [LibraryImport(Library)] public static partial CULong FPDF_GetLastError();
    [LibraryImport(Library)] public static partial int FPDF_GetPageCount(nint document);
    [LibraryImport(Library)] public static partial nint FPDF_LoadPage(nint document, int index);
    [LibraryImport(Library)] public static partial void FPDF_ClosePage(nint page);
    [LibraryImport(Library)] public static partial int FPDFPage_GetAnnotCount(nint page);
    [LibraryImport(Library)] public static partial nint FPDFPage_GetAnnot(nint page, int index);
    [LibraryImport(Library)] public static partial void FPDFPage_CloseAnnot(nint annotation);
    [LibraryImport(Library)] public static partial int FPDFAnnot_GetSubtype(nint annotation);
    [LibraryImport(Library)] private static partial int FPDFAnnot_HasKey(nint annotation, byte* key);
    [LibraryImport(Library)] private static partial CULong FPDFAnnot_GetStringValue(nint annotation, byte* key, char* buffer, CULong length);
    [LibraryImport(Library)] private static partial int FPDFAnnot_SetStringValue(nint annotation, byte* key, char* value);
    [LibraryImport(Library)] private static partial int FPDF_SaveAsCopy(nint document, FileWrite* writer, CULong flags);
}

/// <summary>A PDFium document. Bytes it's opened on stay pinned until it's disposed, since PDFium reads them lazily. Use under <see cref="Pdfium.Gate"/>.</summary>
internal sealed unsafe class PdfiumDocument : IDisposable
{
    private GCHandle _pin;

    private PdfiumDocument(nint handle, GCHandle pin)
    {
        Handle = handle;
        _pin = pin;
    }

    public nint Handle { get; }

    public int PageCount => Pdfium.FPDF_GetPageCount(Handle);

    /// <summary>Opens <paramref name="pdf"/>, or returns null if PDFium can't (it isn't a PDF, or needs a password it wasn't given).</summary>
    public static PdfiumDocument? Open(byte[] pdf, string? password)
    {
        Pdfium.Initialise();
        var pin = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        nint handle;
        fixed (byte* p = Pdfium.Utf8(password))
            handle = Pdfium.FPDF_LoadMemDocument64((void*)pin.AddrOfPinnedObject(), (nuint)pdf.Length, p);
        if (handle != 0)
            return new PdfiumDocument(handle, pin);
        pin.Free();
        return null;
    }

    /// <summary>Opens the file at <paramref name="path"/>, or returns null if PDFium can't.</summary>
    public static PdfiumDocument? OpenFile(string path, string? password)
    {
        Pdfium.Initialise();
        nint handle;
        fixed (byte* f = Pdfium.Utf8(path))
        fixed (byte* p = Pdfium.Utf8(password))
            handle = Pdfium.FPDF_LoadDocument(f, p);
        return handle == 0 ? null : new PdfiumDocument(handle, default);
    }

    /// <summary>Calls <paramref name="visit"/> with each annotation on page <paramref name="pageIndex"/>, which is open meanwhile.</summary>
    public void ForEachAnnotation(int pageIndex, Action<nint> visit)
    {
        var page = Pdfium.FPDF_LoadPage(Handle, pageIndex);
        if (page == 0)
            return;
        try
        {
            var count = Pdfium.FPDFPage_GetAnnotCount(page);
            for (var i = 0; i < count; i++)
            {
                var annotation = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annotation == 0)
                    continue;
                try
                {
                    visit(annotation);
                }
                finally
                {
                    Pdfium.FPDFPage_CloseAnnot(annotation);
                }
            }
        }
        finally
        {
            Pdfium.FPDF_ClosePage(page);
        }
    }

    public byte[] SaveIncremental() => Pdfium.SaveIncremental(Handle);

    public void Dispose()
    {
        Pdfium.FPDF_CloseDocument(Handle);
        if (_pin.IsAllocated)
            _pin.Free();
    }
}
