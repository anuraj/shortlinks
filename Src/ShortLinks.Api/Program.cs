using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShortLinks.Shared.Data;
using ShortLinks.Shared.Models;
using ShortLinks.Api.Services;
using ShortLinks.Shared.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShortLinksDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ShortlinksConnection"))
);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShortLinksDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddValidation();

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

app.MapGet("/api/analytics/{alias}", async ([FromRoute] string alias, [FromServices] ShortLinksDbContext dbContext, CancellationToken cancellationToken) =>
{
    var linkEntity = await dbContext.Links.FirstOrDefaultAsync(l => l.Alias == alias, cancellationToken);
    if (linkEntity == null)
    {
        return Results.NotFound(new { Message = "Short code not found." });
    }

    var clicks = await dbContext.LinkClicks
        .Where(c => c.Alias == alias)
        .Select(c => new LinkClick
        {
            Alias = c.Alias,
            ClickedAt = c.ClickedAt,
            IpAddress = c.IpAddress,
            UserAgent = c.UserAgent
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(clicks);
})
.WithDisplayName("Get Short URL Analytics")
.WithDescription("Retrieves analytics for a given short code.");

app.MapGet("/api/{alias}", async ([FromRoute] string alias, [FromServices] ShortLinksDbContext dbContext, 
    [FromServices]IHttpContextAccessor httpContextAccessor, CancellationToken cancellationToken) =>
{
    var linkEntity = await dbContext.Links.FirstOrDefaultAsync(l => l.Alias == alias, cancellationToken);
    if (linkEntity == null)
    {
        return Results.NotFound(new { Message = "Short code not found." });
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

    return Results.Redirect(linkEntity.LongUrl);
})
.WithDisplayName("Redirect Short URL")
.WithDescription("Redirects to the original long URL for a given short code.");

app.MapGet("/api/links", async ([FromServices] ShortLinksDbContext dbContext, CancellationToken cancellationToken) =>
{
    var linkEntities = await dbContext.Links.ToListAsync(cancellationToken);
    var links = linkEntities.Select(l => new Link
    {
        Alias = l.Alias,
        LongUrl = l.LongUrl,
        ExpiresAt = l.ExpiresAt,
    });
    return Results.Ok(links);
})
.WithDisplayName("Get All Links")
.WithDescription("Retrieves all shortened links.");

app.MapHealthChecks("/health");

await app.RunAsync();