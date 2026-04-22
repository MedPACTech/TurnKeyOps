namespace MedInsights.Lib.Models;

public enum FileContainerType
{
    Pdf, Docx, Html, Xml, Csv, Json, Txt, Image, Unknown
}

public enum PdfNature
{
    TextPdf,
    ScannedPdf,
    MixedPdf,
    Unknown
}

public sealed class ExtractionOptions
{
    public int MaxPagesToProbe { get; set; } = 5;
    public int MinCharsPerTextPage { get; set; } = 200;
    public string OcrLanguages { get; set; } = "eng";
    public int OcrDpi { get; set; } = 300;
    public bool InsertPageSeparators { get; set; } = true;
    public string PageSeparatorFormat { get; set; } = "--- Page {0} ---";
    public bool NormalizeWhitespace { get; set; } = true;
    public bool UseMimeSniffing { get; set; } = true;
}

public sealed class ExtractionResult
{
    public string SourceName { get; init; } = "";         // e.g., blob file name
    public string? SourceContentType { get; init; }       // e.g., application/pdf
    public FileContainerType ContainerType { get; init; }
    public string Nature { get; init; } = "";             // e.g., TextPdf / ScannedPdf / PlainText
    public string Text { get; init; } = "";
    public Dictionary<string, object>? Diagnostics { get; init; }
}
