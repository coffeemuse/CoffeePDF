// The tests run one at a time. Every window here holds a PdfViewer, whose workers load fonts through PDFium's shared font
// manager, and two documents loading at once can crash the test host (seen: a segmentation fault inside PDFium's Type1
// font loading). The UI work is serialised on the headless session's thread anyway, so this costs next to nothing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
