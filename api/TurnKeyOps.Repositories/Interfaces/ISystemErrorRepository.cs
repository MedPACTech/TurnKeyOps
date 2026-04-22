
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{

    public interface ISystemErrorRepository
    {
        Task SaveAsync(SystemError error);
    }
}