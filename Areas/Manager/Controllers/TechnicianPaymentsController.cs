using CloudMVCApplication.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Areas.Manager.Controllers
{
    [Authorize(Roles = "Manager")]
    [Area("Manager")]
    [Route("[area]/TechnicianPayments")]
    public class TechnicianPaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TechnicianPaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Return")]
        public async Task<IActionResult> Return(int paymentId)
        {
            var payment = await _context.TechnicianPayments
                .Include(p => p.MaintenanceRequest)
                .Include(p => p.Technician)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }
    }
}
