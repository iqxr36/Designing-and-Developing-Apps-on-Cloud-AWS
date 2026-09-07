using CloudMVCApplication.Data;
using CloudMVCApplication.Models;
using CloudMVCApplication.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services
{
    public class TechnicianWorkloadService
    {
        public const int DefaultMaxActiveJobs = 5;
        private static readonly HashSet<string> ClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Completed",
            "Cancelled",
            "Canceled",
            "Rejected"
        };

        private readonly ApplicationDbContext _context;

        public TechnicianWorkloadService(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool IsActiveAssignment(Assignment assignment)
        {
            return !IsClosedStatus(assignment.Status) &&
                !IsClosedStatus(assignment.Request.Status);
        }

        public async Task<TechnicianWorkloadSnapshot> GetSnapshotAsync(int technicianId)
        {
            var assignments = await _context.Assignments
                .Include(a => a.Request)
                .Where(a => a.TechnicianId == technicianId)
                .ToListAsync();

            return CreateSnapshot(assignments.Count(IsActiveAssignment));
        }

        public TechnicianWorkloadSnapshot CreateSnapshot(int activeJobs)
        {
            var workload = CalculateWorkloadPercentage(activeJobs, DefaultMaxActiveJobs);
            return new TechnicianWorkloadSnapshot(
                activeJobs,
                DefaultMaxActiveJobs,
                workload,
                CalculateAvailabilityPercentage(workload),
                GetAvailabilityStatus(workload));
        }

        public int CalculateWorkloadPercentage(int activeJobs, int maxActiveJobs = DefaultMaxActiveJobs)
        {
            if (maxActiveJobs <= 0)
            {
                return 100;
            }

            return Math.Min(100, (int)Math.Round(activeJobs * 100m / maxActiveJobs, MidpointRounding.AwayFromZero));
        }

        public int CalculateAvailabilityPercentage(int workloadPercentage)
            => Math.Clamp(100 - workloadPercentage, 0, 100);

        public string GetAvailabilityStatus(int workloadPercentage)
        {
            return workloadPercentage switch
            {
                <= 0 => "Fully Available",
                <= 30 => "Available",
                <= 50 => "Moderately Available",
                <= 70 => "Busy",
                < 100 => "Almost Full",
                _ => "Unavailable"
            };
        }

        public bool IsAtFullCapacity(int activeJobs)
            => CalculateWorkloadPercentage(activeJobs) >= 100;

        private static bool IsClosedStatus(string? status)
            => !string.IsNullOrWhiteSpace(status) && ClosedStatuses.Contains(status);
    }
}
