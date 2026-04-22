using MedInsights.AzureServices;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Configurations;
using MedInsights.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xabe.FFmpeg;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        configBuilder
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddHttpClient("TranscriptionAPI", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(100);
        });

        // ---- Config sections ----
        services.Configure<AzureStorageSettings>(configuration.GetSection("AzureStorageSettings"));
        services.Configure<AzureSpeechSettings>(configuration.GetSection("AzureSpeechSettings"));

        // ---- Azure Speech ----
        services.AddSingleton<IAzureSpeechService, AzureSpeechService>();
        // ---- Azure Blob Storage ----
        services.AddSingleton<IAzureBlobStorageService, AzureBlobStorageService>();

        // ---- FFmpeg path ----
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Tools", "ffmpeg");
        FFmpeg.SetExecutablesPath(ffmpegPath);
    })
    .Build();

host.Run();
