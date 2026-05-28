using Microsoft.EntityFrameworkCore;
using tramsac99.Areas.Admin.Models;
using tramsac99.Data;

namespace tramsac99.Services
{
    public class ChargingHierarchyService
    {
        private readonly AppDbContext _context;

        public ChargingHierarchyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task SyncStationFromChildrenAsync(int stationId)
        {
            var station = await _context.ChargingStations
                .Include(x => x.ChargingPoles)
                .FirstOrDefaultAsync(x => x.Id == stationId);

            if (station == null)
            {
                return;
            }

            if (station.Status == ChargingStatus.Inactive)
            {
                foreach (var pole in station.ChargingPoles)
                {
                    pole.Status = ChargingStatus.Inactive;
                }

                await _context.SaveChangesAsync();
                return;
            }

            if (!station.ChargingPoles.Any())
            {
                return;
            }

            station.Status = station.ChargingPoles.Any(x => ChargingStatus.IsNodeOperational(x.Status))
                ? ChargingStatus.Active
                : ChargingStatus.Inactive;

            await _context.SaveChangesAsync();
        }

        public async Task SyncAfterPoleChangedAsync(int poleId)
        {
            var pole = await _context.ChargingPoles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == poleId);

            if (pole == null)
            {
                return;
            }

            await SyncStationFromChildrenAsync(pole.StationId);
        }

        public Task SyncAfterPortChangedAsync(int portId)
        {
            // Why changed: charging ports are no longer used in the active business flow.
            return Task.CompletedTask;
        }
    }
}
