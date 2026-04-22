using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientInsurance : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public Guid? CardImage { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime VerificationDate { get; set; }
        public string Carrier { get; set; } = string.Empty;
        public string PolicyNumber { get; set; } = string.Empty;
        public string GroupNumber { get; set; } = string.Empty;
        public string InsuredType { get; set; } = string.Empty;
        public string VerificationPhone { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public Guid InsuranceProviderId { get; set; }
    }
}
