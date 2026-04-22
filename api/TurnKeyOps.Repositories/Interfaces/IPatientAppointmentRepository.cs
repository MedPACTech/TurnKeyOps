using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Models;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientAppointmentRepository : IBaseRepositoryAsync<PatientAppointment>
    {
        Task<IReadOnlyList<PatientAppointment>> SearchAsync(AppointmentSearchRepositoryFilter filter, CancellationToken cancellationToken = default);
        Task<PatientAppointment?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
