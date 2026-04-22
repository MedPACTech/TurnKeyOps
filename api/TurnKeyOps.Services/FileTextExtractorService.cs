using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MedInsights.Lib.Models;
using MedInsights.Services;

// Optional namespaces (uncomment when packages are added)
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using Tesseract;
using DocumentFormat.OpenXml.Packaging;
using AngleSharp;
using AngleSharp.Dom;
// using CsvHelper;
// using System.Globalization;

// NEW: Mime-Detective
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Engine;
using MedInsights.Lib.Utils;
using System.Runtime.InteropServices;
using PDFiumSharp;
using PDFiumSharp.Enums;

// ================================================================================
// Implementation (stream-first, blob-friendly) using FileUtilities + allowlist
// ================================================================================
public sealed class FileTextExtractorService : IFileTextExtractorService
    {
        public async Task<ExtractionResult> ExtractAsync(
            Stream content,
            string fileName,
            string? contentType = null,
            ExtractionOptions? options = null,
            CancellationToken ct = default)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("fileName is required.", nameof(fileName));

            options ??= new ExtractionOptions();

            // Buffer once so we can re-open for probes/extraction.
            var buffer = await ReadAllBytesAsync(content, ct);

            // Determine effective extension and enforce allowlist
            var effectiveExt = DocumentUtilities.GetEffectiveExtension(buffer, fileName, contentType);
            EnsureAcceptedOrThrow(effectiveExt);

            var containerType = MapExtensionToContainerType(effectiveExt);

            switch (containerType)
            {
                case FileContainerType.Pdf:
                    return await ExtractFromPdfAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Docx:
                    return await ExtractFromDocxAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Html:
                    return await ExtractFromHtmlAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Xml:
                    return await ExtractFromXmlAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Csv:
                    return await ExtractFromCsvAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Json:
                    return await ExtractFromJsonAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Txt:
                    return await ExtractFromTxtAsync(buffer, fileName, contentType, options, ct);

                case FileContainerType.Image:
                    return await ExtractFromImageOcrAsync(buffer, fileName, contentType, options, ct);

                default:
                    // Allowed but not yet implemented (e.g., doc, rtf, ppt/pptx, xls/xlsx, dwg)
                    throw new NotSupportedException($"Files with extension '.{effectiveExt}' are allowed by policy but not yet supported for text extraction.");
            }
        }

        // --------------------------------------------------------------------------
        // Allowlist + mapping
        // --------------------------------------------------------------------------
        private static void EnsureAcceptedOrThrow(string effectiveExt)
        {
            if (string.IsNullOrWhiteSpace(effectiveExt) || !DocumentUtilities.AcceptedExtensions.Contains(effectiveExt))
            {
                throw new NotSupportedException(
                    $"File type '.{effectiveExt}' is not accepted. Accepted: " +
                    "Documents (doc, docx, dwg, pdf, ppt, pptx, rtf, xls, xlsx); " +
                    "Images (bmp, gif, ico, jpeg, jpg, png, psd, tiff); " +
                    "Text-like (txt, html, csv, json); Xml (xml).");
            }
        }

        private static FileContainerType MapExtensionToContainerType(string extNoDot)
        {
            var ext = extNoDot.ToLowerInvariant();

            if (ext == "pdf") return FileContainerType.Pdf;
            if (ext == "docx") return FileContainerType.Docx;
            if (ext == "html") return FileContainerType.Html;
            if (ext == "xml") return FileContainerType.Xml;
            if (ext == "csv") return FileContainerType.Csv;
            if (ext == "json") return FileContainerType.Json;
            if (ext == "txt") return FileContainerType.Txt;

            if (ext is "bmp" or "gif" or "ico" or "jpeg" or "jpg" or "png" or "psd" or "tiff")
                return FileContainerType.Image;

            return FileContainerType.Unknown;
        }

        // --------------------------------------------------------------------------
        // PDF (probe -> choose text layer or OCR; all memory-only)
        // --------------------------------------------------------------------------
        private async Task<ExtractionResult> ExtractFromPdfAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var probeOk = ProbePdfWithPdfPig(buffer, opts, out PdfNature nature, out int pagesChecked, out int pagesWithText);
            string text;

            if (probeOk && nature == PdfNature.TextPdf)
            {
                text = ExtractPdfTextWithPdfPig(buffer, opts);
                return new ExtractionResult
                {
                    SourceName = fileName,
                    SourceContentType = contentType,
                    ContainerType = FileContainerType.Pdf,
                    Nature = nameof(PdfNature.TextPdf),
                    Text = Normalize(text, opts),
                    Diagnostics = new Dictionary<string, object> {
                        ["pdf_nature"] = nature.ToString(),
                        ["pages_checked"] = pagesChecked,
                        ["pages_with_text"] = pagesWithText
                    }
                };
            }

            if (probeOk && nature == PdfNature.ScannedPdf)
            {
                text = await OcrEntirePdfAsync(buffer, opts, ct);
                return new ExtractionResult
                {
                    SourceName = fileName,
                    SourceContentType = contentType,
                    ContainerType = FileContainerType.Pdf,
                    Nature = nameof(PdfNature.ScannedPdf),
                    Text = Normalize(text, opts),
                    Diagnostics = new Dictionary<string, object> {
                        ["pdf_nature"] = nature.ToString(),
                        ["pages_checked"] = pagesChecked,
                        ["pages_with_text"] = pagesWithText,
                        ["ocr"] = "tesseract"
                    }
                };
            }

            // Mixed or unknown -> simplest path: OCR all (optimize per-page later)
            text = await OcrEntirePdfAsync(buffer, opts, ct);
            return new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Pdf,
                Nature = nameof(PdfNature.MixedPdf),
                Text = Normalize(text, opts),
                Diagnostics = new Dictionary<string, object> {
                    ["pdf_nature"] = nature.ToString(),
                    ["pages_checked"] = pagesChecked,
                    ["pages_with_text"] = pagesWithText,
                    ["ocr"] = "tesseract"
                }
            };
        }

        private static bool ProbePdfWithPdfPig(
            byte[] buf,
            ExtractionOptions opts,
            out PdfNature nature,
            out int pagesChecked,
            out int pagesWithText)
        {
            pagesChecked = 0;
            pagesWithText = 0;
            try
            {
                using var ms = new MemoryStream(buf, writable: false);
                using var doc = UglyToad.PdfPig.PdfDocument.Open(ms);
                int total = doc.NumberOfPages;
                int pagesToProbe = Math.Min(total, opts.MaxPagesToProbe);

                for (int i = 1; i <= pagesToProbe; i++)
                {
                    var page = doc.GetPage(i);
                    var txt = page.Text ?? "";
                    pagesChecked++;
                    if (txt.Trim().Length >= opts.MinCharsPerTextPage)
                        pagesWithText++;
                }

                if (pagesChecked == 0) { nature = PdfNature.Unknown; return false; }
                if (pagesWithText == 0) { nature = PdfNature.ScannedPdf; return true; }
                if (pagesWithText == pagesChecked) { nature = PdfNature.TextPdf; return true; }
                nature = PdfNature.MixedPdf; return true;
            }
            catch
            {
                nature = PdfNature.Unknown;
                return false;
            }
        }

        private static string ExtractPdfTextWithPdfPig(byte[] buf, ExtractionOptions opts)
        {
            var sb = new StringBuilder();
            try
            {
                using var ms = new MemoryStream(buf, writable: false);
                using var doc = UglyToad.PdfPig.PdfDocument.Open(ms);
                int total = doc.NumberOfPages;
                for (int i = 1; i <= total; i++)
                {
                    if (opts.InsertPageSeparators)
                        sb.AppendLine(string.Format(opts.PageSeparatorFormat, i));

                    var page = doc.GetPage(i);
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

    private static async Task<string> OcrEntirePdfAsync(byte[] buf, ExtractionOptions opts, CancellationToken ct, string? password = null)
    {
        var parts = new List<string>();

        // Open directly from bytes (count = -1 means “all”)
        using var doc = new PDFiumSharp.PdfDocument(buf, 0, -1, password);

        for (int i = 0; i < doc.Pages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var page = doc.Pages[i];

            // Convert PDF points (72 DPI) → target pixels
            int widthPx  = (int)Math.Round(page.Width  / 72.0 * opts.OcrDpi);
            int heightPx = (int)Math.Round(page.Height / 72.0 * opts.OcrDpi);

            // Force 4 bytes/pixel; BGRA when hasAlpha=true, otherwise BGRx (still 4 bytes)
            using var bmp = new PDFiumBitmap(widthPx, heightPx, hasAlpha: true);
            bmp.Fill(0xFFFFFFFF); // white background

            var dest  = (left: 0, top: 0, width: widthPx, height: heightPx);
            var flags = RenderingFlags.LcdText | RenderingFlags.Annotations;
            page.Render(bmp, dest, PageOrientations.Normal, flags);

            // Prepare a tightly-packed buffer (no padding) for ImageSharp
            const int BytesPerPixel = 4; // BGRA/BGRx
            int tightRowBytes = widthPx * BytesPerPixel;
            var raw = new byte[heightPx * tightRowBytes];

            // Copy row-by-row from Scan0 honoring Stride
            IntPtr srcBase = bmp.Scan0;
            int stride = bmp.Stride; // may be >= tightRowBytes
            for (int y = 0; y < heightPx; y++)
            {
                IntPtr srcRow = srcBase + y * stride;
                int dstOffset = y * tightRowBytes;
                Marshal.Copy(srcRow, raw, dstOffset, tightRowBytes);
            }

            // Wrap BGRA bytes into ImageSharp, encode PNG in-memory for existing OCR path
            using var image = Image.LoadPixelData<Bgra32>(raw, widthPx, heightPx);
            using var ms = new MemoryStream();
            await image.SaveAsync(ms, PngFormat.Instance, ct);
            var pngBytes = ms.ToArray();

            if (opts.InsertPageSeparators)
                parts.Add(string.Format(opts.PageSeparatorFormat, i + 1));

            var pageText = OcrImageBytes(pngBytes, opts.OcrLanguages);
            parts.Add(pageText);
        }

        return string.Join(Environment.NewLine, parts);
    }


    private static string OcrImageBytes(byte[] pngBytes, string languages)
    {
        if (string.IsNullOrWhiteSpace(languages)) languages = "eng";

        // App-local tessdata path (works in dev, publish, Azure)
        var tessDataPath = Path.Combine(AppContext.BaseDirectory, "Tessdata");

        // Optional: sanity check for missing language file
        var primaryLang = languages.Split('+', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        var expected = Path.Combine(tessDataPath, $"{primaryLang}.traineddata");
        if (!File.Exists(expected))
            throw new InvalidOperationException($"Missing traineddata file: {expected}");

        using var engine = new Tesseract.TesseractEngine(tessDataPath, languages, Tesseract.EngineMode.Default);
        using var img = Tesseract.Pix.LoadFromMemory(pngBytes);
        using var page = engine.Process(img);
        return page.GetText() ?? "";
    }


        // --------------------------------------------------------------------------
        // DOCX (OpenXML, memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromDocxAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var text = ExtractDocxText(buffer);
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Docx,
                Nature = "TextDocx",
                Text = Normalize(text, opts)
            });
        }

        private static string ExtractDocxText(byte[] buffer)
        {
            using var ms = new MemoryStream(buffer, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            return body?.InnerText ?? "";
        }

        // --------------------------------------------------------------------------
        // HTML (AngleSharp, memory-only)
        // --------------------------------------------------------------------------
        private async Task<ExtractionResult> ExtractFromHtmlAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var html = Encoding.UTF8.GetString(buffer);
            var text = await ExtractVisibleTextFromHtmlAsync(html, ct);
            return new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Html,
                Nature = "TextHtml",
                Text = Normalize(text, opts)
            };
        }

        private static async Task<string> ExtractVisibleTextFromHtmlAsync(string html, CancellationToken ct)
        {
            var context = BrowsingContext.New(AngleSharp.Configuration.Default);
            var doc = await context.OpenAsync(req => req.Content(html), ct);
            // AngleSharp's Text() returns visible text w/o tags (roughly)
            return doc.Body?.Text().Trim() ?? "";
        }

        // --------------------------------------------------------------------------
        // XML (memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromXmlAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var xml = Encoding.UTF8.GetString(buffer);
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Xml,
                Nature = "Structured",
                Text = Normalize(xml, opts)
            });
        }

        // --------------------------------------------------------------------------
        // CSV (memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromCsvAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var text = Encoding.UTF8.GetString(buffer);
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Csv,
                Nature = "Structured",
                Text = Normalize(text, opts)
            });
        }

        // --------------------------------------------------------------------------
        // JSON (memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromJsonAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var json = Encoding.UTF8.GetString(buffer);
            // (Optional) pretty-print later with System.Text.Json if you want
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Json,
                Nature = "Structured",
                Text = Normalize(json, opts)
            });
        }

        // --------------------------------------------------------------------------
        // TXT (memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromTxtAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var text = Encoding.UTF8.GetString(buffer);
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Txt,
                Nature = "PlainText",
                Text = Normalize(text, opts)
            });
        }

        // --------------------------------------------------------------------------
        // Image OCR (memory-only)
        // --------------------------------------------------------------------------
        private Task<ExtractionResult> ExtractFromImageOcrAsync(
            byte[] buffer,
            string fileName,
            string? contentType,
            ExtractionOptions opts,
            CancellationToken ct)
        {
            var text = OcrImageBytes(buffer, opts.OcrLanguages);
            return Task.FromResult(new ExtractionResult
            {
                SourceName = fileName,
                SourceContentType = contentType,
                ContainerType = FileContainerType.Image,
                Nature = "OcrImage",
                Text = Normalize(text, opts),
                Diagnostics = new Dictionary<string, object> { ["ocr"] = "tesseract" }
            });
        }

        // --------------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------------
        private static async Task<byte[]> ReadAllBytesAsync(Stream input, CancellationToken ct)
        {
            if (input is MemoryStream ms && ms.TryGetBuffer(out var seg))
                return seg.Array!.AsSpan(seg.Offset, seg.Count).ToArray();

            using var mem = new MemoryStream();
            await input.CopyToAsync(mem, 81920, ct);
            return mem.ToArray();
        }

        private static string Normalize(string input, ExtractionOptions opts)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var s = input.Replace("\r\n", "\n");
            if (opts.NormalizeWhitespace)
                s = string.Join("\n", s.Split('\n').Select(line => line.TrimEnd()));
            return s.Trim();
        }
    }
