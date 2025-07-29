using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using System.ComponentModel;




namespace FileProcessingMicroservice.FunctionApp.Services
{

    public class BlobService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobService> _logger;

        public BlobService(BlobServiceClient blobServiceClient, ILogger<BlobService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<string> UploadAsync(string containerName, string blobName, Stream data, string? contentType = null)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobClient = containerClient.GetBlobClient(blobName);

                data.Position = 0;

                var uploadOptions = new BlobUploadOptions();
                if(containerName == "upload")
                {
                    uploadOptions.HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType ?? "application/octet-stream"
                    };


                    uploadOptions.Metadata = new Dictionary<string, string>
                    {
                        ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                        ["OriginalName"] = blobName,

                    };
                }
                else
                {
                    //blobName = $"processed_{blobName}";
                    uploadOptions = new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = GetContentType(blobName)
                        },
                        Metadata = new Dictionary<string, string>
                        {
                            ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                            ["OriginalName"] = blobName,
                            ["ProcessedFile"] = "true"
                        }
                    };

                }

                await blobClient.UploadAsync(data, uploadOptions);

                _logger.LogInformation("Successfully uploaded blob {BlobName} to container {ContainerName}", blobName, containerName);

                 return blobClient.Uri.ToString();
                //var uploadedUri = await GenerateUploadSasUri(containerName, blobName);
                //return uploadedUri;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload blob {BlobName} to container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<Stream> DownloadAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Blob {blobName} not found in container {containerName}");
                }

                var response = await blobClient.DownloadStreamingAsync();
                var memoryStream = new MemoryStream();

                await response.Value.Content.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                _logger.LogInformation("Successfully downloaded blob {BlobName} from container {ContainerName}", blobName, containerName);

                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download blob {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.ExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check existence of blob {BlobName} in container {ContainerName}", blobName, containerName);
                return false;
            }
        }

        public async Task DeleteAsync(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                await blobClient.DeleteIfExistsAsync();

                _logger.LogInformation("Successfully deleted blob {BlobName} from container {ContainerName}", blobName, containerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete blob {BlobName} from container {ContainerName}", blobName, containerName);
                throw;
            }
        }

        public async Task<string> UploadIntoSubFolderAsync(string containerName, Stream fileContent, string originalFileName, string subFolder)
        {
            try
            {
                var container = _blobServiceClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync(PublicAccessType.None);

                string blobName = $"{subFolder.TrimEnd('/')}/processed_{originalFileName}";

                // Optional: check if "directory" already has any blobs
                bool folderExists = false;
                await foreach (var item in container.GetBlobsAsync(prefix: $"{subFolder.TrimEnd('/')}/"))
                {
                    folderExists = true;
                    break;
                }

                var blobClient = container.GetBlobClient(blobName);
                fileContent.Position = 0;

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = GetContentType(originalFileName)
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                        ["OriginalName"] = originalFileName,
                        ["ProcessedFile"] = "true"
                    }
                };

                await blobClient.UploadAsync(fileContent, uploadOptions);

                _logger.LogInformation(folderExists
                    ? "Folder '{SubFolder}' already existed; uploaded {BlobName}"
                    : "Folder '{SubFolder}' created implicitly; uploaded {BlobName}", subFolder, blobName);

                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to subfolder {SubFolder} in container {ContainerName}", subFolder, containerName);
                throw;
            }
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".txt" => "text/plain",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    
    }
}
