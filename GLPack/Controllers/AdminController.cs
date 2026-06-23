using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLPack.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    [Route("admin")]
    public class AdminController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Admin Console";
            ViewData["Breadcrumb"] = "Admin Console";

            return View("~/Views/Admin/Admin.cshtml");
        }
    }
}
