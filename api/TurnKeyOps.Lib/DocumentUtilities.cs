using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Engine;

namespace MedInsights.Lib.Utils
{
    public static class DocumentUtilities
    {
        // Build once for app lifetime (fast & thread-safe).
        private static readonly IContentInspector Inspector = new ContentInspectorBuilder
        {
            // You can switch to Condensed/Exhaustive builders if you prefer.
            Definitions = DefaultDefinitions.All()
            // Example:
            // Definitions = new Definitions.CondensedBuilder {
            //     UsageType = Definitions.Licensing.UsageType.PersonalNonCommercial
            // }.Build()
        }.Build();

        private static readonly Regex PageSplitRegex =
            new Regex(@"---\s*Page\s+(\d+)\s*---", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const int MaxCharsPerDocument = 0;  //8000 tweak as needed add to config default to all (0);

        // Allowlist (now includes HTML, CSV, and JSON)
        public static readonly HashSet<string> AcceptedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            "doc", "docx", "dwg", "pdf", "ppt", "pptx", "rtf", "xls", "xlsx",
            // Images
            "bmp", "gif", "ico", "jpeg", "jpg", "png", "psd", "tiff",
            // Text-like
            "txt", "html", "csv", "json",
            // XML
            "xml"
        };


        /// <summary>
        /// Returns the most likely file extension WITHOUT leading dot (e.g. "pdf").
        /// Strategy: bytes (Mime-Detective) → contentType → file name extension.
        /// </summary>
        public static string GetEffectiveExtension(byte[] buffer, string fileName, string? contentType)
        {
            // 1) Byte-level sniff: group by extension; pick best (highest confidence)
            try
            {
                var results = Inspector.Inspect(buffer);
                var byExt = results.ByFileExtension();
                var bestExt = byExt.FirstOrDefault()?.Extension; // e.g., ".pdf"
                if (!string.IsNullOrWhiteSpace(bestExt))
                    return bestExt.TrimStart('.');
            }
            catch
            {
                // ignore, fall back below
            }

            // 2) Map provided contentType to common extension
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                var mapped = MapMimeToExtension(contentType);
                if (!string.IsNullOrWhiteSpace(mapped))
                    return mapped!;
            }

            // 3) File name extension
            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(ext))
                return ext.TrimStart('.');

            return "";
        }

        /// <summary>
        /// Takes raw extracted text and returns a structured, cleaned DocumentText
        /// with page separation preserved.
        /// </summary>
        public static string NormalizeDocument(string rawText)
        {
            if (rawText == null) throw new ArgumentNullException(nameof(rawText));

            var pages = SplitIntoPages(rawText);
            var sb = new StringBuilder();

            foreach (var (pageNumber, pageContent) in pages)
            {
                // Normalize whitespace for this page
                var normalized = NormalizeWhitespace(pageContent);

                var lines = normalized
                    .Split('\n')
                    .Select(l => l.TrimEnd())
                    .ToList();

                // Filter out junk lines (underscore fields, numeric noise, etc.)
                var cleanedLines = lines
                    .Where(l => !IsJunkLine(l))
                    .ToList();

                if (cleanedLines.Count == 0)
                    continue;

                sb.AppendLine($"[Page {pageNumber}]");

                foreach (var line in cleanedLines)
                {
                    sb.AppendLine(line);
                }

                sb.AppendLine(); // blank line between pages
            }

            return sb.ToString().Trim();
        }

        //TODO: This needs to be pulled from prompt service or configuration
        public static string BuildDocumentsContextPrompt(
        IReadOnlyCollection<Entities.Document> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        //TODO: this needs to pull from our prompt service but also cached to prevent hitting db every call. 
        sb.AppendLine("You have access to the following uploaded documents.");
        sb.AppendLine("Each document may contain important reference text.");
        sb.AppendLine("Use these documents as primary sources when answering questions.");
        sb.AppendLine("Always reference the current set of documents, identified by the label [Document (x)], where 'x' corresponds to the document count.");
        sb.AppendLine("If a document is noted but contains no content, inform the user that there is no content available in that document if they ask about it.");
        sb.AppendLine("If a question cannot be answered from these documents and prior messages, say so clearly.");
        sb.AppendLine("Do not assume or make up data in the documents. ");
        sb.AppendLine("The number of documents can change per turn ALWAYS reference the current set of documents instead of previous turn responses when it comes to document FileNames, Types, and Counts.");
        sb.AppendLine("Documents are formatted as follows:");
        sb.AppendLine("- FileName");
        sb.AppendLine("- Type");
        sb.AppendLine("- Cleaned extracted text with page markers like [Page 1], [Page 2], etc.");
        sb.AppendLine();
        sb.AppendLine("Uploaded documents:");
        sb.AppendLine();

        int index = 1;
        foreach (var doc in documents)
        {

            var truncatedText = TruncateText(doc.TextContent ?? string.Empty, MaxCharsPerDocument);

            sb.AppendLine($"[Document {index}]");
            sb.AppendLine($"FileName: {doc.FileName}");
            sb.AppendLine($"Type: {doc.ContentType}");
            sb.AppendLine("Content:");
            sb.AppendLine(truncatedText);
            sb.AppendLine(); // gap between documents

            index++;
        }

        sb.AppendLine("When you refer to a document in your answer, mention its file name and, if relevant, the page number (e.g., \"See Lab Results, page 2\").");

        return sb.ToString().Trim();
    } 


    private static string TruncateText(string text, int maxLength = 0)
    {
        if (maxLength == 0)
            return text;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength)
            + "\n...[text truncated for length]...";
    }

    private static string? MapMimeToExtension(string mime)
    {
        var m = (mime ?? "").ToLowerInvariant();

        if (m.Contains("pdf")) return "pdf";
        if (m.Contains("word") || m.Contains("officedocument.wordprocessingml.document")) return "docx";
        if (m.StartsWith("text/html") || m.Contains("html")) return "html";
        if (m.StartsWith("text/plain")) return "txt";
        if (m.StartsWith("application/xml") || m == "text/xml") return "xml";
        if (m == "text/csv" || m.Contains("csv")) return "csv";
        if (m == "application/json" || m.EndsWith("/json")) return "json";

        if (m.StartsWith("image/"))
        {
            var part = m.Split('/').LastOrDefault();
            return part switch
            {
                "jpeg" => "jpg",
                "tiff" => "tiff",
                _ => part // bmp, gif, png, ico, psd, etc.
            };
        }

        return null;
    }

         /// <summary>
        /// Split the raw text into pages using markers like "--- Page 1 ---".
        /// If no markers are found, the whole text is treated as page 1.
        /// </summary>
        private static List<(int PageNumber, string Content)> SplitIntoPages(string rawText)
        {
            var parts = PageSplitRegex.Split(rawText);
            var pages = new List<(int, string)>();

            if (parts.Length > 1)
            {
                // parts layout:
                // [0] = text before first match
                // [1] = "1" (page number)
                // [2] = text after first match
                // [3] = "2"
                // [4] = text after second match, etc.
                for (int i = 1; i < parts.Length; i += 2)
                {
                    if (!int.TryParse(parts[i], out var pageNumber))
                        pageNumber = pages.Count + 1;

                    var content = (i + 1 < parts.Length) ? parts[i + 1] : string.Empty;
                    pages.Add((pageNumber, content));
                }
            }
            else
            {
                pages.Add((1, rawText));
            }

            return pages;
        }

        /// <summary>
        /// Normalize line endings, collapse multiple spaces, and collapse
        /// multiple blank lines into a single blank line.
        /// </summary>
        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Normalize line endings
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var rawLines = text.Split('\n');
            var cleanedLines = new List<string>();

            int blankStreak = 0;

            foreach (var rawLine in rawLines)
            {
                var line = rawLine.TrimEnd();

                // Collapse multiple spaces/tabs inside the line
                line = Regex.Replace(line, @"[ \t]+", " ");

                if (string.IsNullOrWhiteSpace(line))
                {
                    blankStreak++;
                    // Allow at most one consecutive blank line
                    if (blankStreak > 1)
                        continue;
                }
                else
                {
                    blankStreak = 0;
                }

                cleanedLines.Add(line);
            }

            // Trim leading blank lines
            while (cleanedLines.Count > 0 && string.IsNullOrWhiteSpace(cleanedLines[0]))
                cleanedLines.RemoveAt(0);

            // Trim trailing blank lines
            while (cleanedLines.Count > 0 && string.IsNullOrWhiteSpace(cleanedLines[^1]))
                cleanedLines.RemoveAt(cleanedLines.Count - 1);

            return string.Join("\n", cleanedLines);
        }

        /// <summary>
        /// Decide if a line is "junk" for analysis purposes.
        /// You can relax this if you want more data passed through.
        /// </summary>
        private static bool IsJunkLine(string line)
        {
            var trimmed = (line ?? string.Empty).Trim();

            if (trimmed.Length == 0)
                return false; // keep blanks; NormalizeWhitespace handles extra ones

            if (IsUnderscoreLine(trimmed))
                return true;

            //if (IsMostlyDigits(trimmed))
            //   return true;

            return false;
        }

        private static bool IsUnderscoreLine(string trimmed)
        {
            // e.g. "__________", "-----", "......"
            if (trimmed.Length == 0)
                return false;

            foreach (var c in trimmed)
            {
                if (c != '_' && c != '-' && c != '.')
                    return false;
            }

            return true;
        }

        private static bool IsMostlyDigits(string trimmed, double threshold = 0.6)
        {
            // Used to filter chart/axis junk like
            // "02000400060008000100001200014000..."
            if (trimmed.Length < 10)
                return false;

            int digits = 0;
            foreach (var c in trimmed)
            {
                if (char.IsDigit(c))
                    digits++;
            }

            double ratio = (double)digits / trimmed.Length;
            return ratio >= threshold;
        }
    }
}
