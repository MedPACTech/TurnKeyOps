using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib;
using System.Text.Json.Serialization;

namespace MedInsights.Lib.Entities
{
    public class PatientMilitaryFirstResponder : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public string MilitaryVeteran { get; set; } = string.Empty;
        public string MilitaryVeteranBranch { get; set; } = string.Empty;
        [JsonConverter(typeof(NullableDateOnlyConverter))]
        public DateOnly? DateDischarged { get; set; }

        public string ActiveMilitary { get; set; } = string.Empty;
        public string ActiveMilitaryBranch { get; set; } = string.Empty;
        [JsonConverter(typeof(NullableDateOnlyConverter))]
        public DateOnly? DateEnlisted { get; set; }
        public string MilitaryId { get; set; } = string.Empty;

        public string FirstResponder { get; set; } = string.Empty;
        public string FirstResponderType { get; set; } = string.Empty;
        public string FirstResponderDepartment { get; set; } = string.Empty;
        public string FirstResponderStation { get; set; } = string.Empty;

        public string LawEnforcement { get; set; } = string.Empty;
        public string LawEnforcementType { get; set; } = string.Empty;
        public string LawEnforcementAgency { get; set; } = string.Empty;
        public string LawEnforcementId { get; set; } = string.Empty;
    }
}
