namespace CoffeePdf.Core.Pdf;

/// <summary>An annotation's subtype, as PDFium numbers them (<c>FPDF_ANNOT_*</c> in <c>fpdf_annot.h</c>).</summary>
public enum AnnotationSubtype
{
    Unknown = 0, Text, Link, FreeText, Line, Square, Circle, Polygon, PolyLine, Highlight, Underline, Squiggly,
    StrikeOut, Stamp, Caret, Ink, Popup, FileAttachment, Sound, Movie, Widget, Screen, PrinterMark, TrapNet, Watermark,
    ThreeD, RichMedia, XfaWidget, Redact,
}

public static class AnnotationSubtypes
{
    /// <summary>
    /// Whether annotations of <paramref name="subtype"/> are markup, which carry an author (<c>/T</c>): ISO 32000-1
    /// §12.5.6.2. A widget's <c>/T</c> is its field's name instead, and links and popups have none.
    /// </summary>
    public static bool IsMarkup(this AnnotationSubtype subtype) => subtype is
        AnnotationSubtype.Text or AnnotationSubtype.FreeText or AnnotationSubtype.Line or AnnotationSubtype.Square or
        AnnotationSubtype.Circle or AnnotationSubtype.Polygon or AnnotationSubtype.PolyLine or
        AnnotationSubtype.Highlight or AnnotationSubtype.Underline or AnnotationSubtype.Squiggly or
        AnnotationSubtype.StrikeOut or AnnotationSubtype.Stamp or AnnotationSubtype.Caret or AnnotationSubtype.Ink or
        AnnotationSubtype.FileAttachment or AnnotationSubtype.Sound or AnnotationSubtype.Redact;
}
