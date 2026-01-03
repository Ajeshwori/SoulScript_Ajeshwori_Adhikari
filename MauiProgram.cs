using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using SoulScript.Data;
using SoulScript.Services;
using QuestPDF.Infrastructure;




namespace SoulScript
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "coursework.db");
               
                System.Diagnostics.Debug.WriteLine($"DB PATH: {dbPath}");
           

                options.UseSqlite($"Data Source={dbPath}");
            });
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddScoped<JournalEntryService>();
            builder.Services.AddScoped<EntryQueryService>();
            builder.Services.AddScoped<StreakService>();
            builder.Services.AddScoped<SecurityService>();



            builder.Services.AddMudServices();
            builder.Services.AddMauiBlazorWebView();


#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();

            builder.Logging.SetMinimumLevel(LogLevel.Debug); // Set minimum log level
#endif
            var app = builder.Build();
            Task.Run(async () =>
            {
                using var scope = app.Services.CreateScope();
                var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                await dbService.InitializeAsync();
            }).GetAwaiter().GetResult();

            return app;
        }
    }
}