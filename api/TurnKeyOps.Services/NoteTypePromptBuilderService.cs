using System.Text;
using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class NoteTypePromptBuilderService : INoteTypePromptBuilderService
    {
        private const string TargetClinical = "clinical_note";
        private const string TargetBilling = "billing_recommendations";
        private const string TargetExternal = "external_communication";

        private static readonly NoteTypePromptSection FallbackClinicalSection = new()
        {
            Key = "clinical_note",
            Label = "Clinical Note",
            OutputTarget = TargetClinical,
            PromptInstructions = "Create concise, clinically relevant documentation with clear findings, assessment, and plan.",
            SortOrder = 100,
            IsRequired = true,
            IsEnabled = true
        };

        private static readonly NoteTypePromptSection FallbackBillingSection = new()
        {
            Key = "billing_recommendations",
            Label = "Billing Recommendations",
            OutputTarget = TargetBilling,
            PromptInstructions = "Recommend billing codes only when supported by transcript evidence. If data is insufficient, explicitly state that additional details are required.",
            SortOrder = 900,
            IsRequired = true,
            IsEnabled = true
        };

        private static readonly NoteTypePromptSection FallbackExternalSection = new()
        {
            Key = "external_communication",
            Label = "External Communication",
            OutputTarget = TargetExternal,
            CommunicationMode = "email",
            PromptInstructions = "Write patient-friendly communication including summary, options discussed, and next steps.",
            SortOrder = 1000,
            IsRequired = false,
            IsEnabled = false
        };

        private readonly INoteTypeService _noteTypeService;
        private readonly INoteTypeProfileService _noteTypeProfileService;

        public NoteTypePromptBuilderService(
            INoteTypeService noteTypeService,
            INoteTypeProfileService noteTypeProfileService)
        {
            _noteTypeService = noteTypeService;
            _noteTypeProfileService = noteTypeProfileService;
        }

        public async Task<NoteTypePromptProfile> ResolveAsync(string noteTypeSelector, CancellationToken ct = default)
        {
            var selector = (noteTypeSelector ?? string.Empty).Trim();
            var noteTypes = await _noteTypeService.GetAllAsync(ct);
            var matched = MatchNoteType(noteTypes, selector);

            if (matched is null)
            {
                var fallbackSections = EnsureOutputTargets([], includeExternalByDefault: false);
                return new NoteTypePromptProfile
                {
                    NoteTypeId = null,
                    DisplayName = string.IsNullOrWhiteSpace(selector) ? "General Note" : selector,
                    Code = NormalizeToken(selector),
                    AlwaysCreateReferral = false,
                    ExternalCommunicationEnabled = fallbackSections.Any(x => x.OutputTarget == TargetExternal && x.IsEnabled),
                    Sections = fallbackSections
                };
            }

            var profile = await _noteTypeProfileService.GetByNoteTypeIdAsync(matched.Id, ct);
            var (sections, alwaysCreateReferral) = ParseSchema(profile?.SectionSchemaJson);
            var normalizedSections = EnsureOutputTargets(sections, includeExternalByDefault: false);

            return new NoteTypePromptProfile
            {
                NoteTypeId = matched.Id,
                DisplayName = matched.Name,
                Code = matched.Code,
                AlwaysCreateReferral = alwaysCreateReferral,
                ExternalCommunicationEnabled = normalizedSections.Any(x => x.OutputTarget == TargetExternal && x.IsEnabled),
                Sections = normalizedSections
            };
        }

        public string BuildSystemPrompt(NoteTypePromptProfile profile)
        {
            var orderedSections = profile.Sections
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sectionOrder = string.Join("\n", orderedSections.Select((section, index) =>
                $"{index + 1}. {section.Label}"));

            var sectionGuidance = BuildSectionGuidance(
                orderedSections,
                "Document the clinical narrative in a structured, concise way.");

            return $"""
You are a clinician-focused charting assistant.
Use ONLY facts in the transcript/context.
Do NOT invent diagnoses, vitals, labs, meds, allergies, imaging, or history.
If data is missing, explicitly say "Not discussed".
Keep output concise and professional.

Return plain text with EXACTLY these section headers in this exact order.
Each header must appear on its own line, followed by that section's content.
Do not add any other headers, wrapper headings, bullet labels, or preamble text.

Required section order:
{sectionOrder}

Note type context:
- Name: {profile.DisplayName}
- Code: {profile.Code}
- AlwaysCreateReferral: {(profile.AlwaysCreateReferral ? "true" : "false")}

Section guidance:
{sectionGuidance}
""";
        }

        public NoteTypePromptOutput SplitOutput(string generatedText, NoteTypePromptProfile profile)
        {
            var text = (generatedText ?? string.Empty).Replace("\r\n", "\n").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return new NoteTypePromptOutput();

            var orderedSections = profile.Sections
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var parsedSections = ParseSectionsByHeader(text, orderedSections);
            if (parsedSections.Count == 0)
                return SplitLegacyThreeSectionOutput(text, profile);

            var clinicalSections = orderedSections.Where(x => x.OutputTarget == TargetClinical).ToList();
            var billingSections = orderedSections.Where(x => x.OutputTarget == TargetBilling).ToList();
            var externalSections = orderedSections.Where(x => x.OutputTarget == TargetExternal).ToList();

            var clinical = BuildBucketText(parsedSections, clinicalSections, includeSingleSectionHeader: true);
            var billing = BuildBucketText(parsedSections, billingSections, includeSingleSectionHeader: false);
            var external = BuildBucketText(parsedSections, externalSections, includeSingleSectionHeader: externalSections.Count > 1);

            if (!profile.ExternalCommunicationEnabled ||
                external.Equals("Not requested for this note type.", StringComparison.OrdinalIgnoreCase))
            {
                external = string.Empty;
            }

            return new NoteTypePromptOutput
            {
                ClinicalNote = clinical,
                BillingRecommendations = billing,
                ExternalCommunication = external
            };
        }

        private static NoteTypeDto? MatchNoteType(IReadOnlyList<NoteTypeDto> noteTypes, string selector)
        {
            if (noteTypes.Count == 0)
                return null;

            var enabled = noteTypes.Where(x => x.IsEnabled).ToList();
            if (enabled.Count == 0)
                return null;

            if (Guid.TryParse(selector, out var id))
            {
                var byId = enabled.FirstOrDefault(x => x.Id == id);
                if (byId is not null)
                    return byId;
            }

            var normalized = NormalizeToken(selector);
            return enabled.FirstOrDefault(x =>
                string.Equals(x.Name, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Code, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeToken(x.Name), normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeToken(x.Code), normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static (List<NoteTypePromptSection> Sections, bool AlwaysCreateReferral) ParseSchema(string? sectionSchemaJson)
        {
            if (string.IsNullOrWhiteSpace(sectionSchemaJson))
                return (new List<NoteTypePromptSection>(), false);

            try
            {
                using var document = JsonDocument.Parse(sectionSchemaJson);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var fromArray = ParseSectionsArray(document.RootElement);
                    return (fromArray, false);
                }

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return (new List<NoteTypePromptSection>(), false);

                var root = document.RootElement;
                var alwaysCreateReferral = ReadAlwaysCreateReferral(root);
                var sections = root.TryGetProperty("sections", out var sectionsNode) && sectionsNode.ValueKind == JsonValueKind.Array
                    ? ParseSectionsArray(sectionsNode)
                    : new List<NoteTypePromptSection>();

                return (sections, alwaysCreateReferral);
            }
            catch (JsonException)
            {
                return (new List<NoteTypePromptSection>(), false);
            }
        }

        private static bool ReadAlwaysCreateReferral(JsonElement root)
        {
            if (root.TryGetProperty("alwaysCreateReferral", out var alwaysProp))
            {
                if (alwaysProp.ValueKind == JsonValueKind.True)
                    return true;
                if (alwaysProp.ValueKind == JsonValueKind.False)
                    return false;
            }

            if (root.TryGetProperty("tags", out var tagsNode) && tagsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsNode.EnumerateArray())
                {
                    if (tag.ValueKind != JsonValueKind.String)
                        continue;

                    var value = tag.GetString()?.Trim();
                    if (string.Equals(value, "AlwaysCreateReferral", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static List<NoteTypePromptSection> ParseSectionsArray(JsonElement sectionsNode)
        {
            return sectionsNode
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(ParseSection)
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static NoteTypePromptSection ParseSection(JsonElement sectionNode)
        {
            var key = ReadString(sectionNode, "key");
            var label = FirstNonEmpty(ReadString(sectionNode, "label"), key, "Section");
            var promptInstructions = ReadString(sectionNode, "promptInstructions");
            var outputTarget = NormalizeOutputTarget(
                FirstNonEmpty(
                    ReadString(sectionNode, "outputTarget"),
                    ReadString(sectionNode, "target"),
                    InferOutputTarget(key, label)));
            var communicationMode = FirstNonEmpty(ReadString(sectionNode, "communicationMode"), "email");
            var sortOrder = ReadInt(sectionNode, "sortOrder", int.MaxValue);
            var isRequired = ReadBool(sectionNode, "isRequired", false);
            var isEnabled = ReadBool(sectionNode, "isEnabled", true);

            return new NoteTypePromptSection
            {
                Key = key,
                Label = label,
                PromptInstructions = promptInstructions,
                OutputTarget = outputTarget,
                CommunicationMode = communicationMode,
                SortOrder = sortOrder,
                IsRequired = isRequired,
                IsEnabled = isEnabled
            };
        }

        private static List<NoteTypePromptSection> EnsureOutputTargets(List<NoteTypePromptSection> sections, bool includeExternalByDefault)
        {
            var normalized = sections
                .Select(section => new NoteTypePromptSection
                {
                    Key = section.Key,
                    Label = section.Label,
                    PromptInstructions = section.PromptInstructions,
                    OutputTarget = NormalizeOutputTarget(section.OutputTarget),
                    CommunicationMode = string.IsNullOrWhiteSpace(section.CommunicationMode) ? "email" : section.CommunicationMode.Trim(),
                    SortOrder = section.SortOrder,
                    IsRequired = section.IsRequired,
                    IsEnabled = section.IsEnabled
                })
                .ToList();

            if (!normalized.Any(x => x.OutputTarget == TargetClinical && x.IsEnabled))
                normalized.Add(FallbackClinicalSection);

            if (!normalized.Any(x => x.OutputTarget == TargetBilling && x.IsEnabled))
                normalized.Add(FallbackBillingSection);

            if (!normalized.Any(x => x.OutputTarget == TargetExternal && x.IsEnabled))
            {
                normalized.Add(new NoteTypePromptSection
                {
                    Key = FallbackExternalSection.Key,
                    Label = FallbackExternalSection.Label,
                    PromptInstructions = FallbackExternalSection.PromptInstructions,
                    OutputTarget = FallbackExternalSection.OutputTarget,
                    CommunicationMode = FallbackExternalSection.CommunicationMode,
                    SortOrder = FallbackExternalSection.SortOrder,
                    IsRequired = false,
                    IsEnabled = includeExternalByDefault
                });
            }

            return normalized
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildSectionGuidance(IEnumerable<NoteTypePromptSection> sections, string fallbackInstruction)
        {
            var list = sections.ToList();
            if (list.Count == 0)
                return $"- {fallbackInstruction}";

            return string.Join("\n", list.Select(section =>
                $"- {section.Label} [{DescribeOutputTarget(section)}] ({(section.IsRequired ? "Required" : "Optional")}): {FirstNonEmpty(section.PromptInstructions, "Document relevant details only from source data.")}"));
        }

        private static string InferOutputTarget(string key, string label)
        {
            var combined = $"{key} {label}".ToLowerInvariant();
            if (combined.Contains("billing") || combined.Contains("cpt") || combined.Contains("icd"))
                return TargetBilling;
            if (combined.Contains("external") || combined.Contains("communication") || combined.Contains("email") || combined.Contains("referral"))
                return TargetExternal;
            return TargetClinical;
        }

        private static string NormalizeOutputTarget(string? value)
        {
            var normalized = NormalizeToken(value);
            return normalized switch
            {
                "CLINICAL" or "CLINICAL_NOTE" or "NOTE" => TargetClinical,
                "BILLING" or "BILLING_RECOMMENDATIONS" => TargetBilling,
                "EXTERNAL" or "EXTERNAL_COMMUNICATION" or "COMMUNICATION" => TargetExternal,
                _ => TargetClinical
            };
        }

        private static string NormalizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = value
                .Trim()
                .ToUpperInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            return string.Join(
                "_",
                new string(chars).Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
                return string.Empty;

            return value.GetString()?.Trim() ?? string.Empty;
        }

        private static int ReadInt(JsonElement element, string propertyName, int fallback)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return fallback;

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue)
                ? intValue
                : fallback;
        }

        private static bool ReadBool(JsonElement element, string propertyName, bool fallback)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                return fallback;

            return value.ValueKind == JsonValueKind.True
                ? true
                : value.ValueKind == JsonValueKind.False
                    ? false
                    : fallback;
        }

        private static Dictionary<string, string> ParseSectionsByHeader(string text, IReadOnlyList<NoteTypePromptSection> sections)
        {
            var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (sections.Count == 0)
                return parsed;

            NoteTypePromptSection? currentSection = null;
            var currentContent = new StringBuilder();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (TryMatchSectionHeader(line, sections, out var matchedSection, out var inlineContent))
                {
                    CommitSection(parsed, currentSection, currentContent);
                    currentSection = matchedSection;
                    currentContent.Clear();
                    if (!string.IsNullOrWhiteSpace(inlineContent))
                        currentContent.AppendLine(inlineContent);
                    continue;
                }

                if (currentSection is null)
                    continue;

                currentContent.AppendLine(line);
            }

            CommitSection(parsed, currentSection, currentContent);
            return parsed;
        }

        private static void CommitSection(
            Dictionary<string, string> parsed,
            NoteTypePromptSection? section,
            StringBuilder content)
        {
            if (section is null)
                return;

            parsed[section.Key] = content.ToString().Trim();
        }

        private static bool TryMatchSectionHeader(
            string line,
            IReadOnlyList<NoteTypePromptSection> sections,
            out NoteTypePromptSection? matchedSection,
            out string inlineContent)
        {
            matchedSection = null;
            inlineContent = string.Empty;
            var trimmedLine = (line ?? string.Empty).Trim();
            var normalizedLine = NormalizeHeaderCandidate(trimmedLine);
            if (string.IsNullOrWhiteSpace(normalizedLine))
                return false;

            foreach (var section in sections)
            {
                var normalizedLabel = NormalizeHeaderCandidate(section.Label);
                var normalizedKey = NormalizeHeaderCandidate(section.Key);

                if (string.Equals(normalizedLine, normalizedLabel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedLine, normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    matchedSection = section;
                    return true;
                }

                if (TryExtractInlineSectionContent(trimmedLine, section.Label, out inlineContent)
                    || TryExtractInlineSectionContent(trimmedLine, section.Key, out inlineContent))
                {
                    matchedSection = section;
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractInlineSectionContent(string line, string header, out string inlineContent)
        {
            inlineContent = string.Empty;
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(header))
                return false;

            var candidate = line.Trim().TrimStart('#', '*', '-', '>', ' ');
            if (!candidate.StartsWith(header, StringComparison.OrdinalIgnoreCase))
                return false;

            var remainder = candidate[header.Length..];
            if (remainder.Length == 0)
                return true;

            if (remainder[0] != ':')
                return false;

            inlineContent = remainder[1..].Trim();
            return true;
        }

        private static string NormalizeHeaderCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var candidate = value.Trim();
            candidate = candidate.TrimStart('#', '*', '-', '>', ' ');

            while (candidate.Length > 1 && char.IsDigit(candidate[0]) && (candidate[1] == '.' || candidate[1] == ')'))
            {
                candidate = candidate[2..].TrimStart();
            }

            candidate = candidate.TrimEnd(':').Trim();
            return NormalizeToken(candidate);
        }

        private static string BuildBucketText(
            IReadOnlyDictionary<string, string> parsedSections,
            IReadOnlyList<NoteTypePromptSection> sections,
            bool includeSingleSectionHeader)
        {
            if (sections.Count == 0)
                return string.Empty;

            if (sections.Count == 1)
            {
                var singleContent = parsedSections.TryGetValue(sections[0].Key, out var content)
                    ? content.Trim()
                    : string.Empty;

                if (!includeSingleSectionHeader || string.IsNullOrWhiteSpace(singleContent))
                    return singleContent;

                return $"{sections[0].Label}\n{singleContent}".Trim();
            }

            return string.Join(
                "\n\n",
                sections
                    .Where(section => parsedSections.TryGetValue(section.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                    .Select(section => $"{section.Label}\n{parsedSections[section.Key].Trim()}"))
                .Trim();
        }

        private static NoteTypePromptOutput SplitLegacyThreeSectionOutput(string text, NoteTypePromptProfile profile)
        {
            const string h1 = "CLINICAL NOTE";
            const string h2 = "BILLING RECOMMENDATIONS";
            const string h3 = "EXTERNAL COMMUNICATION";

            var i1 = IndexOfHeader(text, h1);
            var i2 = IndexOfHeader(text, h2);
            var i3 = IndexOfHeader(text, h3);

            if (i1 < 0 || i2 < 0 || i3 < 0 || !(i1 < i2 && i2 < i3))
            {
                return new NoteTypePromptOutput
                {
                    ClinicalNote = text,
                    BillingRecommendations = string.Empty,
                    ExternalCommunication = string.Empty
                };
            }

            var clinical = ExtractSection(text, i1, i2).Trim();
            var billing = ExtractSection(text, i2, i3).Trim();
            var external = ExtractSection(text, i3, text.Length).Trim();

            if (!profile.ExternalCommunicationEnabled
                || external.Equals("Not requested for this note type.", StringComparison.OrdinalIgnoreCase))
            {
                external = string.Empty;
            }

            return new NoteTypePromptOutput
            {
                ClinicalNote = clinical,
                BillingRecommendations = billing,
                ExternalCommunication = external
            };
        }

        private static string ExtractSection(string text, int startIndex, int endIndex)
        {
            var slice = text.Substring(startIndex, endIndex - startIndex);
            var firstNewline = slice.IndexOf('\n');
            if (firstNewline >= 0)
                slice = slice[(firstNewline + 1)..];

            return slice;
        }

        private static int IndexOfHeader(string text, string header)
        {
            var needle = "\n" + header + "\n";
            var index = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return index + 1;

            return text.StartsWith(header + "\n", StringComparison.OrdinalIgnoreCase) ? 0 : -1;
        }

        private static string DescribeOutputTarget(NoteTypePromptSection section)
        {
            return section.OutputTarget switch
            {
                TargetClinical => "clinical",
                TargetBilling => "billing",
                TargetExternal when !string.IsNullOrWhiteSpace(section.CommunicationMode) => $"{section.CommunicationMode} external",
                TargetExternal => "external",
                _ => "clinical"
            };
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}
