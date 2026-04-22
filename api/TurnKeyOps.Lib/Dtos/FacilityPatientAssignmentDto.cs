namespace MedInsights.Lib.Dtos
{
    public class FacilityPatientAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid FacilityId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientFirstName { get; set; } = string.Empty;
        public string PatientLastName { get; set; } = string.Empty;
        public DateTime AdmitDate { get; set; }
        public DateTime? DischargeDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
