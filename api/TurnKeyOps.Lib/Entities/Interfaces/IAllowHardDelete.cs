

namespace MedInsights.Lib.Entities.Interfaces
{
    /// <summary>
    /// Marker interface for entities that can be hard-deleted from Azure Table Storage.
    /// If not implemented, the repository will attempt a soft delete (set IsDeleted = true).
    /// </summary>
    public interface IAllowHardDelete { }
}