using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SoulScript.Data;

namespace SoulScript.Services
{
    public class DatabaseService
    {
        private readonly ApplicationDbContext _context;
        public DatabaseService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Hotfix for missing migration: manually add IsAppLocked column if missing
                try
                {
                    // Attempt to add the column. If it exists, this will throw, so we catch and ignore.
                    await _context.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN IsAppLocked INTEGER NOT NULL DEFAULT 0;");
                }
                catch
                {
                    // Column likely already exists
                }

                // Uses migrations and creates DB if missing
                await _context.Database.MigrateAsync();

                // Seed required data (moods + prebuilt tags)
                await DbInitializer.InitializeAsync(_context);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Database initialization failed.", ex);
            }
        }



    }
}
