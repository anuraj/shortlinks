using Microsoft.EntityFrameworkCore;
using ShortLinks.Shared.Models;

namespace ShortLinks.Shared.Data;

public class ShortLinksDbContext : DbContext
{
    public ShortLinksDbContext(DbContextOptions options) : base(options)
    {
    }

    protected ShortLinksDbContext()
    {
    }

    public DbSet<LinkEntity> Links { get; set; }
    public DbSet<LinkClickEntity> LinkClicks { get; set; }
}