using GLPack.Models;
using GLPack.Services;
using GLPack.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GLPack.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICompaniesService _companies;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ICompaniesService companies, ILogger<HomeController> logger)
        {
            _companies = companies ?? throw new ArgumentNullException(nameof(companies));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet("/")]
        public async Task<IActionResult> Index(string? q, CancellationToken ct)
        {
            var picks = await _companies.GetQuickPicksAsync(q, 200, ct);
            return View(new HomeIndexViewModel { Search = q, QuickPicks = picks.ToList() });
        }
        
    }
}
