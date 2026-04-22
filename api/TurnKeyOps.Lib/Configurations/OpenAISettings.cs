
namespace MedInsights.Lib.Configurations
{
    public sealed class OpenAISettings
    {
        public string Key { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = "gpt-4.1";
        public string DefaultSystemPrompt { get; set; } = "You are a helpful, concise assistant."; //This must come from tempaltes not config
        public double Temperature { get; set; }
    }
}
