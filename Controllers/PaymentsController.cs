using CloudMVCApplication.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Controllers
{
    [AllowAnonymous]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> SubscriptionReturn(int paymentId)
        {
            var payment = await _context.SubscriptionPayments
                .Include(p => p.Company)
                .Include(p => p.CompanySubscription)
                    .ThenInclude(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }
    }
}
