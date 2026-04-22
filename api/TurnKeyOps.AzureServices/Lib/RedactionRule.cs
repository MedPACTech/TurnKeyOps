using System.Text.RegularExpressions;

namespace MedInsights.AzureServices.Lib
{
    public sealed class RedactionRule
    {
        public Regex Pattern { get; init; } = default!;
        public string Placeholder { get; init; } = default!;
    }
}