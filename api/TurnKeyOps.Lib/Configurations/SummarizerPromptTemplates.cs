
namespace MedInsights.Lib.Configurations
{

    public sealed class SummarizerPromptTemplates
    {
        /// <summary>Prepended to every summary prompt.</summary>
        public string Base { get; set; } =
        "You are a careful summarizer. Preserve factual accuracy, do not invent, and attribute facts to source turnIds.\n" +
        "Prefer concise language. If redacted placeholders like [REDACTED] appear, keep them as-is.\n" +
        "Always include key entities mentioned (patients, providers, contacts, facilities, tools, or other named items) with their known identifiers.\n" +
        "When you refer to an entity, include all known unique IDs or keys (for example: patientId, contactId, facilityId, email, or other GUIDs) when available.\n" +
        "If you reference a recent or active entity, include its most recent known values and preserve full factual context.\n" +
        "Maintain a clear chain of factual grounding to the most recent 8 turns when summarizing, ensuring entity continuity.";


        /// <summary>Template for JSON summaries. Use {TARGET_TOKENS} placeholder.</summary>
        public string Json { get; set; } =
        "Output strict JSON with this schema:\n\n" +
        "{\n" +
        "  \"context\": [{\"fact\": string, \"turnIds\": [string]}],\n" +
        "  \"open_questions\": [{\"q\": string, \"turnIds\": [string]}],\n" +
        "  \"constraints\": [string],\n" +
        "  \"recent_actions\": [{\"who\": \"user\"|\"assistant\"|\"tool\", \"did\": string, \"turnIds\": [string]}],\n" +
        "  \"entities\": [\n" +
        "    {\n" +
        "      \"type\": string, // e.g. 'patient', 'provider', 'facility', 'contact', 'tool'\n" +
        "      \"name\": string,\n" +
        "      \"ids\": { \"id\": string, \"patientId\": string, \"contactId\": string, \"facilityId\": string, ... },\n" +
        "      \"properties\": { \"dob\": string, \"gender\": string, \"email\": string, \"status\": string, ... },\n" +
        "      \"recent\": boolean // true if referenced in last few turns\n" +
        "    }\n" +
        "  ]\n" +
        "}\n\n" +
        "When summarizing, keep the most recent 8 turns in memory and merge new entities with prior known ones.\n" +
        "Preserve all unique identifiers and any associated data values (like GUIDs, patientIds, emails, or tool names).\n" +
        "If an entity reappears, merge rather than duplicate it — update its fields with the most recent known information.\n" +
        "Output valid JSON only, no trailing commas, no commentary.\n" +
        "Keep ≤ {TARGET_TOKENS} tokens.";


        /// <summary>Template for bullet summaries.</summary>
        public string Bullets { get; set; } =
            "Output markdown bullets only.\n" +
            "Include sections: **Context**, **Open Questions**, **Constraints**, **Recent Actions**.\n" +
            "Keep ≤ {TARGET_TOKENS} tokens.";

        /// <summary>Template for mixed JSON + bullets.</summary>
        public string Mixed { get; set; } =
            "Output JSON with a brief \"context\" and \"open_questions\", then a final \"bullets\" markdown field for human display.\n" +
            "Keep ≤ {TARGET_TOKENS} tokens.";
    }
    
}
