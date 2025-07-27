using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;


using Azure.Core;
using Azure.Messaging.ServiceBus;
using global::FileProcessingMicroservice.FunctionApp.Data.Repositories;
using global::FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;



namespace FileProcessingMicroservice.FunctionApp.Functions
{
    public class UploadApiFunction
    {
        private readonly BlobService _blobService;
        private readonly ServiceBusClient _serviceBusClient;
        //private readonly IFileProcessingRepository _repository;
        private readonly ProcessorFactory _processorFactory;
        private readonly ILogger<UploadApiFunction> _logger;

        public UploadApiFunction(
            BlobService blobService,
            ServiceBusClient serviceBusClient,
            //IFileProcessingRepository repository,
            ProcessorFactory processorFactory,
            ILogger<UploadApiFunction> logger)
        {
            _blobService = blobService;
            _serviceBusClient = serviceBusClient;
            //_repository = repository;
            _processorFactory = processorFactory;
            _logger = logger;
        }

        [Function("UploadFile")]
        [OpenApiOperation(operationId: "UploadFile", tags: new[] { "File Upload" },
                          Summary = "Upload a file for processing",
                          Description = "Uploads a file via multipart/form-data and queues it for processing.")]
        //[OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(object), Required = true,
        [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(FileUploadRequest), Required = true,
                           Description = "Form data with file and optional fields")]
        [OpenApiResponseWithBody(HttpStatusCode.Accepted, "application/json", typeof(object),
                                Description = "Returns the correlation ID and file upload status")]
        [OpenApiResponseWithoutBody(HttpStatusCode.BadRequest, Description = "Invalid request or file format")]
        [OpenApiResponseWithoutBody(HttpStatusCode.InternalServerError, Description = "Server error during upload")]
        public async Task<HttpResponseData> UploadFile(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "upload")] HttpRequestData req)
        {
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Starting file upload process. CorrelationId: {CorrelationId}", correlationId);

                // 1. Validate Content-Type header
                if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
                {
                    _logger.LogWarning("Content-Type header missing. CorrelationId: {CorrelationId}", correlationId);
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Content-Type header missing.");
                }

                var contentType = contentTypeValues.FirstOrDefault();
                if (string.IsNullOrEmpty(contentType) || !contentType.Contains("boundary="))
                {
                    _logger.LogWarning("Invalid Content-Type header. CorrelationId: {CorrelationId}, ContentType: {ContentType}",
                        correlationId, contentType);
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid Content-Type header; missing boundary.");
                }

                var boundary = contentType.Split("boundary=", StringSplitOptions.RemoveEmptyEntries)[1].Trim(' ', '"');

                // 2. Parse multipart form data using Azure.Core.MultipartReader
                var reader = new MultipartReader(boundary, req.Body);
                MultipartSection? section;
                string? fileName = null;
                MemoryStream? fileData = null;
                string? originalContentType = null;

                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    var contentDisposition = section.GetContentDispositionHeader();
                    if (contentDisposition != null && contentDisposition.FileName.HasValue)
                    {
                        fileName = contentDisposition.FileName.Value;
                        originalContentType = section.ContentType;
                        fileData = new MemoryStream();
                        await section.Body.CopyToAsync(fileData);
                        fileData.Position = 0;
                        break;
                    }
                }

                if (fileName == null || fileData == null)
                {
                    _logger.LogWarning("No file provided in form-data. CorrelationId: {CorrelationId}", correlationId);
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "No file provided in the form-data.");
                }

                // 3. Validate file type
                if (!_processorFactory.IsSupported(fileName))
                {
                    _logger.LogWarning("Unsupported file type. CorrelationId: {CorrelationId}, FileName: {FileName}",
                        correlationId, fileName);
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                        $"File type '{Path.GetExtension(fileName)}' is not supported.");
                }

                // 4. Validate file size (max 50MB)
                const long maxFileSize = 50 * 1024 * 1024; // 50MB
                if (fileData.Length > maxFileSize)
                {
                    _logger.LogWarning("File too large. CorrelationId: {CorrelationId}, FileName: {FileName}, Size: {Size}",
                        correlationId, fileName, fileData.Length);
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest,
                        $"File size ({fileData.Length / (1024 * 1024)}MB) exceeds maximum allowed size (50MB).");
                }

                // 5. Upload to Blob Storage
                
                var blobUrl = await _blobService.UploadAsync("upload", fileName, fileData, originalContentType);

                var processedFile = new ProcessedFile
                {
                    CorrelationId = correlationId,
                    OriginalFileName = fileName,
                    OriginalBlobUrl = blobUrl,
                    Status = "Queued",
                    ContentType = originalContentType ?? "application/octet-stream",
                    OriginalFileSize = fileData.Length,
                    ProcessorType = _processorFactory.GetProcessorType(fileName),
                    CreatedAt = DateTime.UtcNow
                };
                // 6. Create database record
                //var processedFile = new ProcessedFile
                //{
                //    CorrelationId = correlationId,
                //    OriginalFileName = fileName,
                //    OriginalBlobUrl = blobUrl,
                //    Status = "Queued",
                //    ContentType = originalContentType ?? "application/octet-stream",
                //    OriginalFileSize = fileData.Length,
                //    ProcessorType = _processorFactory.GetProcessorType(fileName),
                //    CreatedAt = DateTime.UtcNow
                //};

                //await _repository.CreateAsync(processedFile);
                //await _repository.LogProcessingEventAsync(correlationId, "FileUploaded",
                // $"File {fileName} uploaded successfully to blob storage", "Info");

                // 7. Send message to Service Bus queue
                var message = new FileProcessingRequest
                {
                    BlobName = fileName,
                    FileName = fileName,
                    CorrelationId = correlationId,
                    ContentType = originalContentType ?? "application/octet-stream",
                    FileSize = fileData.Length,
                    CreatedAt = DateTime.UtcNow
                };

                var sender = _serviceBusClient.CreateSender("sbq-fileupload");
                var serviceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(message))
                {
                    CorrelationId = correlationId,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };

                await sender.SendMessageAsync(serviceBusMessage);
                //await _repository.LogProcessingEventAsync(correlationId, "MessageQueued",
                    //"File processing message sent to queue", "Info");

                // 8. Return success response
                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(new
                {
                    correlationId = correlationId,
                    fileName = fileName,
                    fileSize = fileData.Length,
                    blobUrl = blobUrl,
                    status = "Queued",
                    processorType = _processorFactory.GetProcessorType(fileName),
                    message = "File uploaded successfully and queued for processing"
                });

                _logger.LogInformation("File upload completed successfully. CorrelationId: {CorrelationId}, FileName: {FileName}",
                    correlationId, fileName);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file upload. CorrelationId: {CorrelationId}", correlationId);

                try
                {
                //    await _repository.LogProcessingEventAsync(correlationId, "UploadError",
                //        $"Upload failed: {ex.Message}", "Error", ex.ToString());
                }
                catch
                {
                    // Ignore logging errors to avoid cascading failures
                }

                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                    "Internal server error during file upload.");
            }
        }

        private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var errorResponse = req.CreateResponse(statusCode);
            await errorResponse.WriteAsJsonAsync(new { error = message, timestamp = DateTime.UtcNow });
            return errorResponse;
        }
    }
}

//public class UploadApiFunction
//{
//    private readonly ILogger<UploadApiFunction> _logger;

//    public UploadApiFunction(ILogger<UploadApiFunction> logger)
//    {
//        _logger = logger;
//    }

//    [Function(nameof(UploadApiFunction))]
//    public async Task Run([BlobTrigger("samples-workitems/{name}", Connection = "")] Stream stream, string name)
//    {
//        using var blobStreamReader = new StreamReader(stream);
//        var content = await blobStreamReader.ReadToEndAsync();
//        _logger.LogInformation("C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}", name, content);
//    }

//}