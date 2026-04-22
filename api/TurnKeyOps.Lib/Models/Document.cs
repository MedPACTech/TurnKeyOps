using System;
using System.Collections.Generic;

namespace MedInsights.Lib.Models
{
    public class DocumentText
    {
        public List<PageText> Pages { get; set; } = new List<PageText>();
    }

    public class PageText
    {
        public int PageNumber { get; set; }

        /// <summary>
        /// Lines with simple classification so the AI can optionally ignore "junk".
        /// </summary>
        public List<LineText> Lines { get; set; } = new List<LineText>();

        /// <summary>
        /// Paragraphs reconstructed from text lines (blank / junk lines act as separators).
        /// </summary>
        public List<string> Paragraphs { get; set; } = new List<string>();

        /// <summary>
        /// Full normalized text of the page (joined from all lines).
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }

    public class LineText
    {
        public string Text { get; set; } = string.Empty;
        public LineType Type { get; set; } = LineType.Text;
    }

    public enum LineType
    {
        Text,
        BlankField,   // e.g., "__________"
        NumericJunk,  // digit-heavy chart/axis lines
        OtherJunk
    }
}
