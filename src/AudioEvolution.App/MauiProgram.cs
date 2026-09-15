using AudioEvolution.Data;
using AudioEvolution.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AudioEvolution.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "audioevolution.db");
        builder.Services.AddDbContext<ProjectDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddScoped<ProjectRepository>();

        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectDbContext>();
            db.Database.EnsureCreated();
        }

        return app;
    }
}
