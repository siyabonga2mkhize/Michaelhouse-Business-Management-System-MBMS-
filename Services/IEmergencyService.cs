using Michaelhouse.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Michaelhouse.Services
{
    public interface IEmergencyService
    {
        Task<EmergencyAlert> CreateAlertAsync(EmergencyAlert alert);
        Task<EmergencyAlert> GetActiveAlertAsync();
        Task<EmergencyAlert> GetAlertByIdAsync(int alertId);
        Task<List<Student>> GetAllBoardingStudentsAsync();
        Task<int> GetSafeCountAsync(int alertId);
        Task<int> GetPendingCountAsync(int alertId);
        Task<List<Student>> GetNotRespondedStudentsAsync(int alertId);
    }
}