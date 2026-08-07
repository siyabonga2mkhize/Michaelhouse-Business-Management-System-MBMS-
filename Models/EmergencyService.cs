using Michaelhouse.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Michaelhouse.Services
{
    public class EmergencyService : IEmergencyService
    {
        private readonly DBContextClass _context;

        public EmergencyService(DBContextClass context)
        {
            _context = context;
        }

        public async Task<EmergencyAlert> CreateAlertAsync(EmergencyAlert alert)
        {
            _context.EmergencyAlerts.Add(alert);
            await _context.SaveChangesAsync();
            return alert;
        }

        public async Task<EmergencyAlert> GetActiveAlertAsync()
        {
            return await _context.EmergencyAlerts
                .FirstOrDefaultAsync(a => a.Status == AlertStatus.Active);
        }

        public async Task<EmergencyAlert> GetAlertByIdAsync(int alertId)
        {
            return await _context.EmergencyAlerts
                .FirstOrDefaultAsync(a => a.AlertId == alertId);
        }

        public async Task<List<Student>> GetAllBoardingStudentsAsync()
        {
            return await _context.Students
                .Where(s => s.IsActive && s.IsBoarding)
                .ToListAsync();
        }

        public async Task<int> GetSafeCountAsync(int alertId)
        {
            return await _context.StudentSafetyConfirmations
                .Where(c => c.AlertId == alertId &&
                            c.Status == SafetyStatus.Confirmed)
                .CountAsync();
        }

        public async Task<int> GetPendingCountAsync(int alertId)
        {
            var total = await GetAllBoardingStudentsAsync();
            var safe = await GetSafeCountAsync(alertId);

            return total.Count - safe;
        }

        public async Task<List<Student>> GetNotRespondedStudentsAsync(int alertId)
        {
            var confirmations = await _context.StudentSafetyConfirmations
                .Where(c => c.AlertId == alertId)
                .Select(c => c.StudentId)
                .ToListAsync();

            return await _context.Students
                .Where(s => s.IsActive &&
                            s.IsBoarding &&
                            !confirmations.Contains(s.StudentId))
                .ToListAsync();
        }
    }
}