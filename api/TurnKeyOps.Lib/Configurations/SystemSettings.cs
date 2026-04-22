

namespace MedInsights.Lib.Configurations
{
    public sealed class SystemSettings
    {
        public string EmailLogicAppURL { get; set; }
        public string EmailLogicAppBackupURL { get; set; }
        public string DefaultSenderEmail { get; set; }
        public string EmailTemplateDirectoryName { get; set; }
        public string ApplicationHost { get; set; }
        public string APIHost { get; set; }
        public string MarketingDomain { get; set; }
    }

}
