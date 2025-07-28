using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using FileProcessingMicroservice.FunctionApp.Data;
using FileProcessingMicroservice.FunctionApp.Data.Repositories;
using FileProcessingMicroservice.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.ApplicationInsights;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;

// Register Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
    Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"));
var builder = FunctionsApplication.CreateBuilder(args);

var host = new HostBuilder()
        //.ConfigureFunctionsWebApplication()
        .ConfigureFunctionsWorkerDefaults()

    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // Get connection strings
        string storageConn = config["AzureWebJobsStorage"] ?? throw new ArgumentNullException("AzureWebJobsStorage");
        string serviceBusConn = config["ServiceBusConnection"] ?? throw new ArgumentNullException("ServiceBusConnection");
        string sqlConn = config["AzureSQLConnection"] ?? throw new ArgumentNullException("AzureSQLConnection");

        // Azure services
        services.AddSingleton(_ => new BlobServiceClient(storageConn));
        services.AddSingleton(_ => new ServiceBusClient(serviceBusConn));
        
        // Use Managed Identity for enhanced security
        //services.AddSingleton(serviceProvider =>
        //{
        //    var credential = new DefaultAzureCredential();
        //    var blobServiceUri = $"https://{config["StorageAccountName"]}.blob.core.windows.net";
        //    return new BlobServiceClient(new Uri(blobServiceUri), credential);
        //});

        // Entity Framework
        //services.AddDbContext<FileProcessingDbContext>(options =>
        //    options.UseSqlServer(sqlConn, sqlOptions =>
        //    {
        //        sqlOptions.EnableRetryOnFailure(
        //            maxRetryCount: 5,
        //            maxRetryDelay: TimeSpan.FromSeconds(30),
        //            errorNumbersToAdd: null);
        //        sqlOptions.CommandTimeout(300);
        //    }));

        // Repositories
        //services.AddScoped<IFileProcessingRepository, FileProcessingRepository>();

        // Business services
        services.AddSingleton<BlobService>();
        services.AddScoped<DocumentConversionService>();
        services.AddScoped<ImageProcessingService>();
        services.AddScoped<TextToPdfService>();
        services.AddScoped<ProcessorFactory>();
        services.AddScoped<BlobSasService>();

        //Application Insights
       //services.AddApplicationInsightsTelemetryWorkerService();
        //services.ConfigureFunctionsApplicationInsights();

        // Logging
        services.AddLogging();
        //services.AddLogging(builder =>
        //{
        //    builder.AddConsole();
        //   // builder.AddApplicationInsights();
        //});
    })
    .ConfigureOpenApi()
    .Build();

// Ensure database exists
//try
//{
//    using var scope = host.Services.CreateScope();
//    var context = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();
//    await context.Database.EnsureCreatedAsync();
//}
//catch (Exception ex)
//{
//    var logger = host.Services.GetRequiredService<ILogger<Program>>();
//    logger.LogError(ex, "An error occurred while ensuring the database exists");
//}

host.Run();


//var builder = FunctionsApplication.CreateBuilder(args);

//builder.ConfigureFunctionsWebApplication();

//builder.Services
//    //.AddApplicationInsightsTelemetryWorkerService()
//    .ConfigureFunctionsApplicationInsights();

//builder.Build().Run();

//host.Run();