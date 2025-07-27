//using FileProcessingMicroservice.FunctionApp.Data;
//using Microsoft.Extensions.Configuration;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using FileProcessingMicroservice.FunctionApp.Data;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;
//using Microsoft.Extensions.Configuration;
//using System.IO;


//namespace FileProcessingMicroservice.FunctionApp
//{
    
//   public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FileProcessingDbContext>
//   {
//        public FileProcessingDbContext CreateDbContext(string[] args)
//        {
//            // Build configuration from local.settings.json and environment variables
//            var configuration = new ConfigurationBuilder()
//                .SetBasePath(Directory.GetCurrentDirectory())
//                .AddJsonFile("local.settings.json", optional: true)
//                .AddJsonFile("appsettings.json", optional: true)
//                .AddEnvironmentVariables()
//                .Build();

//            // Get connection string
//            var connectionString = configuration["AzureSQLConnection"]
//                ?? configuration.GetConnectionString("AzureSQLConnection")
//                ?? throw new InvalidOperationException(
//                    "Connection string 'AzureSQLConnection' not found in local.settings.json or environment variables.");

//            // Configure DbContext options
//            var optionsBuilder = new DbContextOptionsBuilder<FileProcessingDbContext>();
//            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
//            {
//                sqlOptions.EnableRetryOnFailure(
//                    maxRetryCount: 5,
//                    maxRetryDelay: TimeSpan.FromSeconds(30),
//                    errorNumbersToAdd: null);
//                sqlOptions.CommandTimeout(300);
//            });

//            return new FileProcessingDbContext(optionsBuilder.Options);
//        }
//    }
//}
