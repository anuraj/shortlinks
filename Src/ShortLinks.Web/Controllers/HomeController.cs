using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ShortLinks.Shared.ViewModels;
using ShortLinks.Web.ViewModels;
using X.PagedList;

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
    public async Task<IActionResult> Index(int? page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page ?? 1, 1);
        var pageSize = 10;
        using var response = await _httpClient.GetAsync($"api/links?page={page}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var listOfLinks = await response.Content.ReadFromJsonAsync<List<Link>>(cancellationToken)
            ?? throw new InvalidOperationException("The links API returned a null response.");
        var pageNumberHeader = int.Parse(response.Headers.GetValues("PageNumber").Single(), CultureInfo.InvariantCulture);
        var pageSizeHeader = int.Parse(response.Headers.GetValues("Size").Single(), CultureInfo.InvariantCulture);
        var totalHeader = int.Parse(response.Headers.GetValues("Total").Single(), CultureInfo.InvariantCulture);

        // The API has already paged the results; preserve them and use its total count for navigation.
        var pagedList = new StaticPagedList<Link>(listOfLinks, pageNumberHeader, pageSizeHeader, totalHeader);
        return View(pagedList);
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

        ModelState.AddModelError(string.Empty, "An error occurred while creating the link.");
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
