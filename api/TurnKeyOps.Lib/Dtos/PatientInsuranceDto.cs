using System.ComponentModel.DataAnnotations;

namespace MedInsights.Lib.Dtos
{
    public class PatientInsuranceDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        public Guid? CardImage { get; set; }
        [Required] public DateTime EffectiveDate { get; set; }
        [Required] public DateTime VerificationDate { get; set; }
        [Required] public string Carrier { get; set; } = string.Empty;
        [Required] public string PolicyNumber { get; set; } = string.Empty;
        [Required] public string GroupNumber { get; set; } = string.Empty;
        [Required] public string InsuredType { get; set; } = string.Empty;
        [Required] public string VerificationPhone { get; set; } = string.Empty;
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string MiddleName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required] public string Relationship { get; set; } = string.Empty;
        [Required] public Guid InsuranceProviderId { get; set; }
    }
}
