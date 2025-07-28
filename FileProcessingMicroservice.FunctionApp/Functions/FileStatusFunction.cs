using Azure.Core;
using global::FileProcessingMicroservice.FunctionApp.Data.Repositories;
using global::FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.AspNetCore.OpenApi;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using System.Net;
//using System.Net;
//using FileProcessingMicroservice.FunctionApp.Data.Repositories;
//using FileProcessingMicroservice.FunctionApp.Models;
//using FileProcessingMicroservice.FunctionApp.Services;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
//using Microsoft.Extensions.Logging;



namespace FileProcessingMicroservice.FunctionApp.Functions {


    public class FileStatusFunction
    {
        private readonly BlobService _blobService;
        // private readonly IFileProcessingRepository _repository;
        private readonly BlobSasService _blobSasService;

        private readonly ILogger<FileStatusFunction> _logger;
        //private readonly SasTokenService _sasTokenService;

        public FileStatusFunction(
            BlobService blobService,
                        //IFileProcessingRepository repository,
                        BlobSasService blobSasService,
            ILogger<FileStatusFunction> logger)//,
            //SasTokenService sasTokenService)
        {
            _blobService = blobService;
            //_repository = repository;
            _blobSasService = blobSasService;

            _logger = logger;
            //_sasTokenService = sasTokenService;
        }
        public FileStatusFunction(ILogger<FileStatusFunction> logger)
        {
            _logger = logger;
        }

        [Function("GetFileStatus")]
        [OpenApiOperation(operationId: "GetFileStatus", tags: new[] { "File Status" },
                          Summary = "Get file processing status",
                          Description = "Retrieves the current processing status of an uploaded file by filename.")]
        [OpenApiParameter(name: "fileName", In = Microsoft.OpenApi.Models.ParameterLocation.Path, Required = true,
                         Type = typeof(string), Description = "Name of the file to check")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
                                bodyType: typeof(FileStatusResponse), Description = "File status information")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "File not found")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Server error")]
        public async Task<HttpResponseData> GetFileStatus(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "files/{fileName}/status")] HttpRequestData req,
            string fileName)
        {
            try
            {
                _logger.LogInformation("Getting status for file: {FileName}", fileName);

                //// 1. Check if file exists in database
                //var processedFile = await _repository.GetByFileNameAsync(fileName);
                //if (processedFile == null)
                //{
                //    _logger.LogWarning("File not found in database: {FileName}", fileName);

                //    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                //    await notFoundResponse.WriteAsJsonAsync(new
                //    {
                //        fileName = fileName,
                //        status = "NotFound",
                //        message = "File not found in the system",
                //        timestamp = DateTime.UtcNow
                //    });
                //    return notFoundResponse;
                //}

                // 2. Check blob existence
                var uploadBlob = $"upload/{fileName}";
                bool uploadedExists = await _blobService.ExistsAsync("upload", fileName);//, DateTime.UtcNow.Year.ToString());

                if (!uploadedExists)
                {
                    _logger.LogWarning("Original file not found in blob storage: {FileName}", fileName);

                    var missingBlobResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await missingBlobResponse.WriteAsJsonAsync(new
                    {
                        fileName = fileName,
                        status = "MissingBlob",
                        message = "Original file not found in storage",
                        timestamp = DateTime.UtcNow
                    });
                    return missingBlobResponse;
                }
                 ProcessorFactory processor = new ProcessorFactory();
                var outputExtension = processor.GetExpectedOutputExtension(fileName);
                 var originalFileName = Path.GetFileNameWithoutExtension(fileName);
                var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
//                var processedBlob = $"processed_{Path.GetFileNameWithoutExtension(fileName)}{OutputExtension}";

                string processedBlob = $"{dateFolder.TrimEnd('/')}/processed_{originalFileName}{outputExtension}";
               processedBlob = $"processed_{originalFileName}{outputExtension}";

                bool processedExists = await _blobService.ExistsAsync("processed", processedBlob);

//                blobName = $"{dateFolder.TrimEnd('/')}/{blobName}";




                // 3. Build response based on processing status
                var response = req.CreateResponse(HttpStatusCode.OK);
                await using var outputStream = await _blobService.DownloadAsync("processed", processedBlob);

                

                //// Add processed file information if available
                //if (processedFile.Status == "Processed" && !string.IsNullOrEmpty(processedFile.ProcessedBlobUrl))
                //{
                //    statusResponse.ProcessedUrl = processedFile.ProcessedBlobUrl;
                //    statusResponse.ProcessedFileName = processedFile.ProcessedFileName;
                //    statusResponse.ProcessedAt = processedFile.ProcessedAt;
                //    statusResponse.ProcessorType = processedFile.ProcessorType;
                //}
                //else if (processedFile.Status == "Failed")
                //{
                //    statusResponse.ProcessedUrl = null;
                //    // Include error information in a more detailed response
                //    await response.WriteAsJsonAsync(new
                //    {
                //        fileName = statusResponse.FileName,
                //        uploadedUrl = statusResponse.UploadedUrl,
                //        processedUrl = (string?)null,
                //        status = statusResponse.Status,
                //        errorMessage = processedFile.ErrorMessage,
                //        processorType = processedFile.ProcessorType,
                //        createdAt = processedFile.CreatedAt,
                //        updatedAt = processedFile.UpdatedAt,
                //        timestamp = DateTime.UtcNow
                //    });

                //_logger.LogInformation("Returned failed status for file: {FileName}", fileName);
                //return response;
                // }

                // For successful or in-progress files
                //await response.WriteAsJsonAsync(new
                //{
                //    fileName = statusResponse.FileName,
                //    uploadedUrl = statusResponse.UploadedUrl,
                //    processedUrl = statusResponse.ProcessedUrl,
                //    status = statusResponse.Status,
                //    processedFileName = statusResponse.ProcessedFileName,
                //    processedAt = statusResponse.ProcessedAt,
                //    processorType = statusResponse.ProcessorType,
                //    originalFileSize = processedFile.OriginalFileSize,
                //    processedFileSize = processedFile.ProcessedFileSize,
                //    createdAt = processedFile.CreatedAt,
                //    updatedAt = processedFile.UpdatedAt,
                //    timestamp = DateTime.UtcNow
                //});
                // Generate SAS URL for the uploaded file
                var ProcessedSasUrl = await _blobSasService.GenerateReadSasUrlAsync("processed", processedBlob);

                _logger.LogInformation("Successfully returned status for file: {FileName}, Status: {Status}",
                    fileName, "TEST STATUS");
                var statusResponse = new FileStatusResponse
                {
                    FileName = fileName,
                    UploadedUrl = "TEST URL",//processedFile.OriginalBlobUrl,
                    Status = processedExists.ToString(),// "TEST Status"//processedFile.Status
                    ProcessedUrl = ProcessedSasUrl
                };
                //var BlobResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await response.WriteAsJsonAsync(statusResponse);
                return response;
                //return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for file: {FileName}", fileName);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    fileName = fileName,
                    status = "Error",
                    message = "Internal server error while retrieving file status",
                    timestamp = DateTime.UtcNow
                });
                return errorResponse;
            }
        }

        [Function("GetFileStatusByCorrelationId")]
        [OpenApiOperation(operationId: "GetFileStatusByCorrelationId", tags: new[] { "File Status" },
                          Summary = "Get file processing status by correlation ID",
                          Description = "Retrieves the current processing status of an uploaded file by correlation ID.")]
        [OpenApiParameter(name: "correlationId", In = Microsoft.OpenApi.Models.ParameterLocation.Path, Required = true,
                         Type = typeof(string), Description = "Correlation ID of the file processing request")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json",
                                bodyType: typeof(FileStatusResponse), Description = "File status information")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "File not found")]
        public async Task<HttpResponseData> GetFileStatusByCorrelationId(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{correlationId}")] HttpRequestData req,
            string correlationId)
        {
            try
            {
                _logger.LogInformation("Getting status for correlation ID: {CorrelationId}", correlationId);

                //var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
                //if (processedFile == null)
                //{
                //    _logger.LogWarning("File not found for correlation ID: {CorrelationId}", correlationId);

                //    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                //    await notFoundResponse.WriteAsJsonAsync(new
                //    {
                //        correlationId = correlationId,
                //        status = "NotFound",
                //        message = "No file found with the specified correlation ID",
                //        timestamp = DateTime.UtcNow
                //    });
                //    return notFoundResponse;
                //}

                // Get processing logs for this correlation ID
                //var logs = await _repository.GetProcessingLogsAsync(correlationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    correlationId = correlationId,
                    //fileName = processedFile.OriginalFileName,
                    //uploadedUrl = processedFile.OriginalBlobUrl,
                    //processedUrl = processedFile.ProcessedBlobUrl,
                    //processedFileName = processedFile.ProcessedFileName,
                    //status = processedFile.Status,
                    //processorType = processedFile.ProcessorType,
                    //originalFileSize = processedFile.OriginalFileSize,
                    //processedFileSize = processedFile.ProcessedFileSize,
                    //errorMessage = processedFile.ErrorMessage,
                    //createdAt = processedFile.CreatedAt,
                    //processedAt = processedFile.ProcessedAt,
                    //updatedAt = processedFile.UpdatedAt,
                    //processingLogs = logs.Select(log => new
                    //{
                    //    eventType = log.EventType,
                    //    message = log.Message,
                    //    logLevel = log.LogLevel,
                    //    timestamp = log.Timestamp,
                    //    additionalData = log.AdditionalData
                    //}).ToList(),
                    timestamp = DateTime.UtcNow,
                    status = "NotFound",
                    message = "No file found with the specified correlation ID"
                    
                });

                _logger.LogInformation("Successfully returned detailed status for correlation ID: {CorrelationId}", correlationId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for correlation ID: {CorrelationId}", correlationId);

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    correlationId = correlationId,
                    status = "Error",
                    message = "Internal server error while retrieving file status",
                    timestamp = DateTime.UtcNow
                });
                return errorResponse;
            }
        }

    //    [Function("GetFileStatusWithSas")]
    //    [OpenApiOperation("GetFileStatusWithSas", Summary = "Get file status with SAS URLs")]
    //    public async Task<HttpResponseData> GetFileStatusWithSas(
    //[HttpTrigger(AuthorizationLevel.Function, "get", Route = "files/{fileName}/status-with-sas")] HttpRequestData req,
    //string fileName)
    //    {
    //        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
    //        //var year = queryParams["year"] ?? DateTime.UtcNow.Year.ToString();

    //        var uploadExists = await _blobService.ExistsAsync("upload", fileName);

    //        if (!uploadExists)
    //        {
    //            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
    //            await notFound.WriteAsJsonAsync(new { fileName, status = "NotFound" });
    //            return notFound;
    //        }

    //        // Generate SAS URLs for existing files
    //        string uploadSasUrl = null;
    //        string processedSasUrl = null;

    //        try
    //        {
    //            // Generate SAS for uploaded file
    //            var uploadToken = await _sasTokenService.GenerateReadTokenAsync("upload", fileName);
    //            uploadSasUrl = uploadToken.SasUrl;

    //            // Check if processed version exists and generate SAS
    //            var processedFileName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";
    //            var currentYear = DateTime.UtcNow.Year.ToString();

    //            if (await _blobService.ExistsAsync("processed", processedFileName))
    //            {
    //                var processedToken = await _sasTokenService.GenerateReadTokenAsync("processed", processedFileName);

    //                //var processedToken = await _sasTokenService.GenerateReadTokenAsync("processed", processedFileName, currentYear);
    //                processedSasUrl = processedToken.SasUrl;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogWarning(ex, "Failed to generate some SAS URLs for {FileName}", fileName);
    //        }

    //        var response = req.CreateResponse(HttpStatusCode.OK);
    //        await response.WriteAsJsonAsync(new
    //        {
    //            fileName,
    //            //uploadYear = year,
    //            status = processedSasUrl != null ? "Processed" : "Pending",
    //            uploadSasUrl,
    //            processedSasUrl,
    //            sasTokensExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
    //        });

    //        return response;
    //    }

    }
}