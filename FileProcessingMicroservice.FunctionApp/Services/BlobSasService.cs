using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace FileProcessingMicroservice.FunctionApp.Services
{
   

    public class BlobSasService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BlobSasService> _logger;
        private readonly int _sasExpiryHours;

        public BlobSasService(
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<BlobSasService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _logger = logger;
            _sasExpiryHours = int.Parse(_configuration["SasTokenExpiryHours"] ?? "2");
        }

        /// <summary>
        /// Generates a SAS URL for reading a blob
        /// </summary>
        public async Task<string> GenerateReadSasUrlAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    throw new FileNotFoundException($"Blob '{blobName}' not found in container '{containerName}'");
                }

                if (!blobClient.CanGenerateSasUri)
                {
                    throw new InvalidOperationException(
                        "BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                }

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                    Resource = "b", // blob resource
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow for clock skew
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(_sasExpiryHours)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                _logger.LogInformation("Generated SAS URL for blob '{BlobName}' in container '{ContainerName}', expires at {ExpiresOn}",
                    blobName, containerName, sasBuilder.ExpiresOn);

                return sasUri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate SAS URL for blob '{BlobName}' in container '{ContainerName}'",
                    blobName, containerName);
                throw;
            }
        }

        /// <summary>
        /// Generates a SAS URL for writing to a blob
        /// </summary>
        public async Task<string> GenerateWriteSasUrlAsync(string containerName, string blobName, TimeSpan? expiry = null)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!blobClient.CanGenerateSasUri)
                {
                    throw new InvalidOperationException(
                        "BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                }

                var expiryTime = expiry ?? TimeSpan.FromMinutes(30); // Short expiry for uploads

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = blobName,
                    Resource = "b",
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.Add(expiryTime)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                _logger.LogInformation("Generated write SAS URL for blob '{BlobName}' in container '{ContainerName}', expires at {ExpiresOn}",
                    blobName, containerName, sasBuilder.ExpiresOn);

                return sasUri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate write SAS URL for blob '{BlobName}' in container '{ContainerName}'",
                    blobName, containerName);
                throw;
            }
        }

        /// <summary>
        /// Generates a container-level SAS URL for listing blobs
        /// </summary>
        public async Task<string> GenerateContainerListSasUrlAsync(string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

                if (!containerClient.CanGenerateSasUri)
                {
                    throw new InvalidOperationException(
                        "BlobContainerClient must be authorized with Shared Key credentials to create a service SAS.");
                }

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    Resource = "c", // container resource
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(_sasExpiryHours)
                };

                sasBuilder.SetPermissions(BlobContainerSasPermissions.List | BlobContainerSasPermissions.Read);

                var sasUri = containerClient.GenerateSasUri(sasBuilder);

                _logger.LogInformation("Generated container list SAS URL for container '{ContainerName}', expires at {ExpiresOn}",
                    containerName, sasBuilder.ExpiresOn);

                return sasUri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate container list SAS URL for container '{ContainerName}'",
                    containerName);
                throw;
            }
        }
    }
}
