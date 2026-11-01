using System.Net.Http.Headers;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["ApiEndpoint"] is null)
{
    throw new InvalidOperationException("ApiEndpoint configuration is missing.");
}

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiEndpoint"]!);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient("RedirectClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiEndpoint"]!);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});

builder.Services.AddHealthChecks().AddCheck("ApiEndpoint", () =>
{
    var httpClient = new HttpClient { BaseAddress = new Uri(builder.Configuration["ApiEndpoint"]!) };
    var response = httpClient.GetAsync("/health").Result;
    return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() :
        HealthCheckResult.Unhealthy();
}, tags: ["api"]);

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHealthChecks("/health");

app.Run();
