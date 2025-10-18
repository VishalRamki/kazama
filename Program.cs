using Kazama;
using Kazama.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs/kazama-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
    .CreateLogger();


try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseWindowsService(options =>
        {
            options.ServiceName = "Kazama";
        })
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var tomlPath = Path.Combine(AppContext.BaseDirectory, Constants.ConfigurationFile);
            if (!File.Exists(tomlPath))
                throw new FileNotFoundException($"Configuration file not found: {tomlPath}");

            // Build config manually
            var configRoot = new FolderSyncRoot();

            services.AddSingleton(configRoot);
            services.AddHostedService<MultiFolderSyncWorker>();
        });
   
    var host = builder.Build();
    // This line is correct:
    await host.RunAsync(); // Must be awaited; does not block SCM
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

