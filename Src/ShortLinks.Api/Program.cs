using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShortLinks.Shared.Data;
using ShortLinks.Shared.Models;
using ShortLinks.Api.Services;
using ShortLinks.Shared.ViewModels;
using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<ShortLinksDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ShortlinksConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShortLinksDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddValidation();

builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString =
        builder.Configuration.GetConnectionString("ShortlinksConnection")
        ?? throw new InvalidOperationException("CacheConnection is missing.");

    options.SchemaName = "dbo";
    options.TableName = "DistributedCache";
});

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddOpenApi();
builder.Services.AddSingleton<UrlShortenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapPost("/api/shorten", async ([FromServices] ShortLinksDbContext dbContext,
    [FromBody] Link link, [FromServices] UrlShortenService urlShortenService, CancellationToken cancellationToken) =>
{
    if (!Uri.TryCreate(link.LongUrl, UriKind.Absolute, out var uriResult) ||
        (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new { Message = "Invalid long URL." });
    }

    var invalidDate = link.ExpiresAt.HasValue
        && link.ExpiresAt.Value <= DateTime.UtcNow;
    if (invalidDate)
    {
        return Results.BadRequest(new { Message = "Invalid expiration date." });
    }

    var expiresAt = link.ExpiresAt ?? DateTime.UtcNow.AddDays(30);

    var linkEntity = new LinkEntity
    {
        LongUrl = link.LongUrl,
        Alias = link.Alias ?? await urlShortenService.ShortenUrl(link.LongUrl, cancellationToken),
        ExpiresAt = expiresAt,
        CreatedAt = DateTime.UtcNow,
        IsCustom = link.Alias != null
    };

    var shortCodeExists =
        await dbContext.Links.AnyAsync(l => l.Alias == linkEntity.Alias, cancellationToken);
    if (shortCodeExists)
    {
        return Results.Conflict(new { Message = "Short code already exists." });
    }

    dbContext.Links.Add(linkEntity);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new Link
    {
        Alias = linkEntity.Alias,
        LongUrl = linkEntity.LongUrl,
        ExpiresAt = linkEntity.ExpiresAt
    });
})
.WithDisplayName("Shorten URL")
.WithDescription("Shortens a given long URL into a short link.");

app.MapGet("/api/analytics/{alias}", async ([FromRoute] string alias,
    [FromServices] ShortLinksDbContext dbContext, HttpContext httpContext, [FromQuery] int? page = 1,
    [FromQuery] int? pageSize = 10, CancellationToken cancellationToken = default) =>
{
    var linkEntity = await dbContext.Links.FirstOrDefaultAsync(l => l.Alias == alias, cancellationToken);
    if (linkEntity == null)
    {
        return Results.NotFound(new { Message = "Short code not found." });
    }
    page ??= 1;
    pageSize ??= 10;
    var totalRows = await dbContext.LinkClicks.CountAsync(c => c.Alias == alias, cancellationToken);
    var clicks = await dbContext.LinkClicks
        .Where(c => c.Alias == alias)
        .OrderByDescending(c => c.ClickedAt)
        .Skip((page.Value - 1) * pageSize.Value)
        .Take(pageSize.Value)
        .Select(c => new LinkClick
        {
            Alias = c.Alias,
            ClickedAt = c.ClickedAt,
            IpAddress = c.IpAddress,
            UserAgent = c.UserAgent
        })
        .ToListAsync(cancellationToken);

    httpContext.Response.Headers["PageNumber"] = page.Value.ToString(CultureInfo.InvariantCulture);
    httpContext.Response.Headers["Size"] = pageSize.Value.ToString(CultureInfo.InvariantCulture);
    httpContext.Response.Headers["Total"] = totalRows.ToString(CultureInfo.InvariantCulture);

    return Results.Ok(clicks);
})
.WithDisplayName("Get Short URL Analytics")
.WithDescription("Retrieves analytics for a given short code.");

app.MapGet("/api/{alias}", async ([FromRoute] string alias, [FromServices] ShortLinksDbContext dbContext,
    [FromServices] IHttpContextAccessor httpContextAccessor,
    [FromServices] HybridCache cache,
    [FromServices] IDbContextFactory<ShortLinksDbContext> contextFactory,
    CancellationToken cancellationToken) =>
{
    var link = await cache.GetOrCreateAsync<CachedLink?>(
    $"link:v1:{alias}",
    async token =>
    {
        await using var lookupContext =
            await contextFactory.CreateDbContextAsync(token);

        return await lookupContext.Links
            .AsNoTracking()
            .Where(l => l.Alias == alias)
            .Select(l => new CachedLink(
                l.LongUrl, l.ExpiresAt, l.IsExpired))
            .FirstOrDefaultAsync(token);
    },
    cancellationToken: cancellationToken);

    if (link is null)
    {
        return Results.NotFound(new { Message = "Short code not found." });
    }

    if (link.IsExpired ||
        (link.ExpiresAt.HasValue && link.ExpiresAt.Value <= DateTime.UtcNow))
    {
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    var linkClick = new LinkClickEntity
    {
        Alias = alias,
        ClickedAt = DateTime.UtcNow,
        IpAddress = httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        UserAgent = httpContextAccessor?.HttpContext?.Request.Headers.UserAgent.ToString()
    };
    dbContext.LinkClicks.Add(linkClick);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Redirect(link.LongUrl);
})
.WithDisplayName("Redirect Short URL")
.WithDescription("Redirects to the original long URL for a given short code.");

app.MapGet("/api/links", async ([FromServices] ShortLinksDbContext dbContext, HttpContext httpContext, [FromQuery] int? page = 1,
    [FromQuery] int? pageSize = 10, CancellationToken cancellationToken = default) =>
{
    page ??= 1;
    pageSize ??= 10;
    var totalRows = await dbContext.Links.CountAsync(cancellationToken);
    var linkEntities = await dbContext.Links
        .OrderByDescending(l => l.CreatedAt)
        .ThenBy(l => l.Alias)
        .Skip((page.Value - 1) * pageSize.Value)
        .Take(pageSize.Value)
        .ToListAsync(cancellationToken);

    var links = linkEntities.Select(l => new Link
    {
        Alias = l.Alias,
        LongUrl = l.LongUrl,
        ExpiresAt = l.ExpiresAt,
    }).ToList();

    httpContext.Response.Headers["PageNumber"] = page.Value.ToString(CultureInfo.InvariantCulture);
    httpContext.Response.Headers["Size"] = pageSize.Value.ToString(CultureInfo.InvariantCulture);
    httpContext.Response.Headers["Total"] = totalRows.ToString(CultureInfo.InvariantCulture);

    return Results.Ok(links);
})
.WithDisplayName("Get All Links")
.WithDescription("Retrieves all shortened links.");

app.MapHealthChecks("/health");

await app.RunAsync();


public sealed record CachedLink(
    string LongUrl,
    DateTime? ExpiresAt,
    bool IsExpired);