using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OscarBot.Classes
{
    public class EntityContext : DbContext
    {
        public DbSet<ModerationActionCollection> ModerationActions { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<Prefix> Prefixes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=database.db");
        }

        public EntityContext()
        {
            Database.EnsureCreated();
        }
    }
}
