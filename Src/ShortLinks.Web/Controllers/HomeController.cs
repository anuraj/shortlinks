using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShortLinks.Shared.ViewModels;
using ShortLinks.Web.ViewModels;

namespace ShortLinks.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HttpClient _httpClient;

    public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ApiClient");
    }
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var listOfLinks = await _httpClient.GetFromJsonAsync<List<Link>>("api/links", cancellationToken);
        listOfLinks ??= [];
        return View(listOfLinks);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Link link, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var response = await _httpClient.PostAsJsonAsync("api/shorten", link, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, "An error occurred while creating the link.");
        }
        return RedirectToAction(nameof(Index));
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
}
