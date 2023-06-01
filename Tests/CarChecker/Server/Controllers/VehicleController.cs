using CarChecker.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarChecker.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CarChecker.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class VehicleController : ControllerBase
    {
        private ApplicationDbContext db;

        public VehicleController(ApplicationDbContext db)
        {
            this.db = db;
        }

        public IEnumerable<Vehicle> ChangedVehicles([FromQuery] DateTime since)
        {
            return this.db.Vehicles.Where(v => v.LastUpdated >= since).Include(v => v.Notes);
        }

        [HttpPut]
        public async Task<IActionResult> Details(Vehicle vehicle)
        {
            string id = vehicle.LicenseNumber;
            List<InspectionNote> existingNotes = (await this.db.Vehicles.AsNoTracking().Include(v => v.Notes).SingleAsync(v => v.LicenseNumber == id)).Notes;
            ILookup<int, InspectionNote> retainedNotes = vehicle.Notes.ToLookup(n => n.InspectionNoteId);
            IEnumerable<InspectionNote> notesToDelete = existingNotes.Where(n => !retainedNotes.Contains(n.InspectionNoteId));
            this.db.RemoveRange(notesToDelete);

            vehicle.LastUpdated = DateTime.Now;
            this.db.Vehicles.Update(vehicle);

            await this.db.SaveChangesAsync();
            return this.Ok();
        }
    }
}
