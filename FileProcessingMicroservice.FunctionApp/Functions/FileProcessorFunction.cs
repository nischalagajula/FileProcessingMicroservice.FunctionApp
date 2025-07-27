using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using global::FileProcessingMicroservice.FunctionApp.Data.Repositories;
using global::FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
//using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.ServiceBus;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FileProcessingMicroservice.FunctionApp.Data.Repositories;
using FileProcessingMicroservice.FunctionApp.Models;
using FileProcessingMicroservice.FunctionApp.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;


namespace FileProcessingMicroservice.FunctionApp.Functions
{


    public class FileProcessorFunction
    {
        private readonly BlobService _blobService;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ProcessorFactory _processorFactory;
        //private readonly IFileProcessingRepository _repository;
        private readonly ILogger<FileProcessorFunction> _logger;

        public FileProcessorFunction(
            BlobService blobService,
            ServiceBusClient serviceBusClient,
            ProcessorFactory processorFactory,
           // IFileProcessingRepository repository,
            ILogger<FileProcessorFunction> logger)
        {
            _blobService = blobService;
            _serviceBusClient = serviceBusClient;
            _processorFactory = processorFactory;
            //_repository = repository;
            _logger = logger;
        }

        [Function("FileProcessorFunction")]
        public async Task Run(
            [ServiceBusTrigger("sbq-fileupload", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            var correlationId = message.CorrelationId ?? "unknown";

            try
            {
                _logger.LogInformation("Starting file processing. CorrelationId: {CorrelationId}, MessageId: {MessageId}",
                    correlationId, message.MessageId);

                // 1. Deserialize message
                var request = JsonSerializer.Deserialize<FileProcessingRequest>(message.Body.ToString());
                if (request == null)
                {
                    throw new InvalidOperationException("Failed to deserialize file processing request");
                }

                correlationId = request.CorrelationId; // Update with actual correlation ID
                //await _repository.LogProcessingEventAsync(correlationId, "ProcessingStarted",
                //    $"Started processing file {request.FileName}", "Info");

                // 2. Update database status
                //var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
                //if (processedFile != null)
                //{
                //    processedFile.Status = "Processing";
                //    await _repository.UpdateAsync(processedFile);
                //}

                // 3. Download file from blob storage
                await using var inputStream = await _blobService.DownloadAsync("upload", request.BlobName);

                _logger.LogInformation("Downloaded file from blob storage. CorrelationId: {CorrelationId}, BlobName: {BlobName}",
                    correlationId, request.BlobName);

                // 4. Process the file using the appropriate processor
                var fileContext = new FileContext(inputStream, request.FileName, request.ContentType);
                var outcome = await _processorFactory.ProcessAsync(fileContext);

                _logger.LogInformation("File processing completed. CorrelationId: {CorrelationId}, ProcessorType: {ProcessorType}",
                    correlationId, outcome.ProcessorType);

                // 5. Upload processed file to blob storage with date-based folder structure
                //var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
                //var dateFolder = DateTime.UtcNow.Year.ToString();
                //var processedBlobUrl = await _blobService.UploadIntoSubFolderAsync(
                //    "processed", outcome.FileStream, outcome.FileName, dateFolder);
                var processedFileName = "processed_" + outcome.FileName;
                var processedBlobUrl = await _blobService.UploadAsync(
                    "processed", processedFileName, outcome.FileStream, request.ContentType);


                _logger.LogInformation("Uploaded processed file to blob storage. CorrelationId: {CorrelationId}, ProcessedBlobUrl: {ProcessedBlobUrl}",
                    correlationId, processedBlobUrl);

                //// 6. Update database with results
                //if (processedFile != null)
                //{
                //    processedFile.Status = "Processed";
                //    processedFile.ProcessedFileName = outcome.FileName;
                //    processedFile.ProcessedBlobUrl = processedBlobUrl;
                //    processedFile.ProcessedAt = DateTime.UtcNow;
                //    processedFile.ProcessedFileSize = outcome.FileStream.Length;
                //    processedFile.ProcessorType = outcome.ProcessorType;

                //    //await _repository.UpdateAsync(processedFile);
                //}

                //await _repository.LogProcessingEventAsync(correlationId, "ProcessingCompleted",
                //    $"File processed successfully: {outcome.FileName}", "Info",
                //    JsonSerializer.Serialize(new { ProcessorType = outcome.ProcessorType, OutputFileName = outcome.FileName }));

                // 7. Send result message to output queue
                var resultMessage = new FileProcessingResult
                {
                    CorrelationId = correlationId,
                    OriginalFileName = request.FileName,
                    ProcessedFileName = outcome.FileName,
                    ResultBlobUrl = processedBlobUrl,
                    Status = "Processed",
                    Message = "File processed successfully",
                    ProcessedAt = DateTime.UtcNow,
                    ProcessorType = outcome.ProcessorType
                };

                var sender = _serviceBusClient.CreateSender("sbq-fileprocessing");
                var resultServiceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(resultMessage))
                {
                    CorrelationId = correlationId,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };

                await sender.SendMessageAsync(resultServiceBusMessage);

                _logger.LogInformation("File processing completed successfully. CorrelationId: {CorrelationId}, OriginalFile: {OriginalFile}, ProcessedFile: {ProcessedFile}",
                    correlationId, request.FileName, outcome.FileName);

                // 8. Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Unsupported file type. CorrelationId: {CorrelationId}", correlationId);

                await HandleProcessingError(correlationId, message, messageActions, ex, "UnsupportedFileType",
                    "File type is not supported for processing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file processing. CorrelationId: {CorrelationId}", correlationId);

                await HandleProcessingError(correlationId, message, messageActions, ex, "ProcessingError",
                    $"File processing failed: {ex.Message}");
            }
        }

        private async Task HandleProcessingError(
            string correlationId,
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            Exception exception,
            string errorType,
            string errorMessage)
        {
            try
            {
                // Update database status
                //var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
                //if (processedFile != null)
                //{
                //    processedFile.Status = "Failed";
                //    processedFile.ErrorMessage = errorMessage;
                //    await _repository.UpdateAsync(processedFile);
                //}

                //// Log the error
                //await _repository.LogProcessingEventAsync(correlationId, errorType, errorMessage, "Error", exception.ToString());

                //// Send error result to output queue
                var errorResult = new FileProcessingResult
                {
                    CorrelationId = correlationId,
                    OriginalFileName = GetFileNameFromMessage(message),
                    Status = "Failed",
                    Message = errorMessage,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessorType = "Error"
                };

                var sender = _serviceBusClient.CreateSender("sbq-fileprocessing");
                var errorServiceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(errorResult))
                {
                    CorrelationId = correlationId,
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json"
                };

                await sender.SendMessageAsync(errorServiceBusMessage);

                // Determine if we should retry or dead letter the message
                if (message.DeliveryCount >= 3 || exception is NotSupportedException)
                {
                    _logger.LogWarning("Message will be dead lettered. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
                        correlationId, message.DeliveryCount);

                    await messageActions.DeadLetterMessageAsync(message, null, errorType, errorMessage);
                }
                else
                {
                    _logger.LogInformation("Message will be abandoned for retry. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
                        correlationId, message.DeliveryCount);

                    await messageActions.AbandonMessageAsync(message);
                }
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Error while handling processing error. CorrelationId: {CorrelationId}", correlationId);

                // Last resort: abandon the message
                try
                {
                    await messageActions.AbandonMessageAsync(message);
                }
                catch (Exception abandonEx)
                {
                    _logger.LogError(abandonEx, "Failed to abandon message. CorrelationId: {CorrelationId}", correlationId);
                }
            }
        }

        private static string GetFileNameFromMessage(ServiceBusReceivedMessage message)
        {
            try
            {
                var request = JsonSerializer.Deserialize<FileProcessingRequest>(message.Body.ToString());
                return request?.FileName ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }

    //public class FileProcessorFunction
    //{
    //    private readonly BlobService _blobService;
    //    private readonly ServiceBusClient _serviceBusClient;
    //    private readonly ProcessorFactory _processorFactory;
    //    private readonly IFileProcessingRepository _repository;
    //    private readonly ILogger<FileProcessorFunction> _logger;

    //    public FileProcessorFunction(
    //        BlobService blobService,
    //        ServiceBusClient serviceBusClient,
    //        ProcessorFactory processorFactory,
    //        IFileProcessingRepository repository,
    //        ILogger<FileProcessorFunction> logger)
    //    {
    //        _blobService = blobService;
    //        _serviceBusClient = serviceBusClient;
    //        _processorFactory = processorFactory;
    //        _repository = repository;
    //        _logger = logger;
    //    }

    //    [Function("FileProcessorFunction")]
    //    public async Task Run(
    //        [ServiceBusTrigger("sbq-fileupload", Connection = "ServiceBusConnection")]
    //    ServiceBusReceivedMessage message,
    //        ServiceBusMessageActions messageActions)
    //    {
    //        var correlationId = message.CorrelationId ?? "unknown";

    //        try
    //        {
    //            _logger.LogInformation("Starting file processing. CorrelationId: {CorrelationId}, MessageId: {MessageId}",
    //                correlationId, message.MessageId);

    //            // 1. Deserialize message
    //            var request = JsonSerializer.Deserialize<FileProcessingRequest>(message.Body.ToString());
    //            if (request == null)
    //            {
    //                throw new InvalidOperationException("Failed to deserialize file processing request");
    //            }

    //            correlationId = request.CorrelationId; // Update with actual correlation ID
    //            await _repository.LogProcessingEventAsync(correlationId, "ProcessingStarted",
    //                $"Started processing file {request.FileName}", "Info");

    //            // 2. Update database status
    //            var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
    //            if (processedFile != null)
    //            {
    //                processedFile.Status = "Processing";
    //                await _repository.UpdateAsync(processedFile);
    //            }

    //            // 3. Download file from blob storage
    //            await using var inputStream = await _blobService.DownloadAsync("upload", request.BlobName);

    //            _logger.LogInformation("Downloaded file from blob storage. CorrelationId: {CorrelationId}, BlobName: {BlobName}",
    //                correlationId, request.BlobName);

    //            // 4. Process the file using the appropriate processor
    //            var fileContext = new FileContext(inputStream, request.FileName, request.ContentType);
    //            var outcome = await _processorFactory.ProcessAsync(fileContext);

    //            _logger.LogInformation("File processing completed. CorrelationId: {CorrelationId}, ProcessorType: {ProcessorType}",
    //                correlationId, outcome.ProcessorType);

    //            // 5. Upload processed file to blob storage with date-based folder structure
    //            var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
    //            var processedBlobUrl = await _blobService.UploadIntoSubFolderAsync(
    //                "processed", outcome.FileStream, outcome.FileName, dateFolder);

    //            _logger.LogInformation("Uploaded processed file to blob storage. CorrelationId: {CorrelationId}, ProcessedBlobUrl: {ProcessedBlobUrl}",
    //                correlationId, processedBlobUrl);

    //            // 6. Update database with results
    //            if (processedFile != null)
    //            {
    //                processedFile.Status = "Processed";
    //                processedFile.ProcessedFileName = outcome.FileName;
    //                processedFile.ProcessedBlobUrl = processedBlobUrl;
    //                processedFile.ProcessedAt = DateTime.UtcNow;
    //                processedFile.ProcessedFileSize = outcome.FileStream.Length;
    //                processedFile.ProcessorType = outcome.ProcessorType;

    //                await _repository.UpdateAsync(processedFile);
    //            }

    //            await _repository.LogProcessingEventAsync(correlationId, "ProcessingCompleted",
    //                $"File processed successfully: {outcome.FileName}", "Info",
    //                JsonSerializer.Serialize(new { ProcessorType = outcome.ProcessorType, OutputFileName = outcome.FileName }));

    //            // 7. Send result message to output queue
    //            var resultMessage = new FileProcessingResult
    //            {
    //                CorrelationId = correlationId,
    //                OriginalFileName = request.FileName,
    //                ProcessedFileName = outcome.FileName,
    //                ResultBlobUrl = processedBlobUrl,
    //                Status = "Processed",
    //                Message = "File processed successfully",
    //                ProcessedAt = DateTime.UtcNow,
    //                ProcessorType = outcome.ProcessorType
    //            };

    //            var sender = _serviceBusClient.CreateSender("sbq-fileprocessing");
    //            var resultServiceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(resultMessage))
    //            {
    //                CorrelationId = correlationId,
    //                MessageId = Guid.NewGuid().ToString(),
    //                ContentType = "application/json"
    //            };

    //            await sender.SendMessageAsync(resultServiceBusMessage);

    //            _logger.LogInformation("File processing completed successfully. CorrelationId: {CorrelationId}, OriginalFile: {OriginalFile}, ProcessedFile: {ProcessedFile}",
    //                correlationId, request.FileName, outcome.FileName);

    //            // 8. Complete the message
    //            await messageActions.CompleteMessageAsync(message);
    //        }
    //        catch (NotSupportedException ex)
    //        {
    //            _logger.LogError(ex, "Unsupported file type. CorrelationId: {CorrelationId}", correlationId);

    //            await HandleProcessingError(correlationId, message, messageActions, ex, "UnsupportedFileType",
    //                "File type is not supported for processing");
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Error during file processing. CorrelationId: {CorrelationId}", correlationId);

    //            await HandleProcessingError(correlationId, message, messageActions, ex, "ProcessingError",
    //                $"File processing failed: {ex.Message}");
    //        }
    //    }

    //    private async Task HandleProcessingError(
    //        string correlationId,
    //        ServiceBusReceivedMessage message,
    //        ServiceBusMessageActions messageActions,
    //        Exception exception,
    //        string errorType,
    //        string errorMessage)
    //    {
    //        try
    //        {
    //            // Update database status
    //            var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
    //            if (processedFile != null)
    //            {
    //                processedFile.Status = "Failed";
    //                processedFile.ErrorMessage = errorMessage;
    //                await _repository.UpdateAsync(processedFile);
    //            }

    //            // Log the error
    //            await _repository.LogProcessingEventAsync(correlationId, errorType, errorMessage, "Error", exception.ToString());

    //            // Send error result to output queue
    //            var errorResult = new FileProcessingResult
    //            {
    //                CorrelationId = correlationId,
    //                OriginalFileName = GetFileNameFromMessage(message),
    //                Status = "Failed",
    //                Message = errorMessage,
    //                ProcessedAt = DateTime.UtcNow,
    //                ProcessorType = "Error"
    //            };

    //            var sender = _serviceBusClient.CreateSender("sbq-fileprocessing");
    //            var errorServiceBusMessage = new ServiceBusMessage(JsonSerializer.Serialize(errorResult))
    //            {
    //                CorrelationId = correlationId,
    //                MessageId = Guid.NewGuid().ToString(),
    //                ContentType = "application/json"
    //            };

    //            await sender.SendMessageAsync(errorServiceBusMessage);

    //            // Determine if we should retry or dead letter the message
    //            if (message.DeliveryCount >= 3 || exception is NotSupportedException)
    //            {
    //                _logger.LogWarning("Message will be dead lettered. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
    //                    correlationId, message.DeliveryCount);

    //                await messageActions.DeadLetterMessageAsync(message, null,errorType, errorMessage);
    //            }
    //            else
    //            {
    //                _logger.LogInformation("Message will be abandoned for retry. CorrelationId: {CorrelationId}, DeliveryCount: {DeliveryCount}",
    //                    correlationId, message.DeliveryCount);

    //                await messageActions.AbandonMessageAsync(message);
    //            }
    //        }
    //        catch (Exception logEx)
    //        {
    //            _logger.LogError(logEx, "Error while handling processing error. CorrelationId: {CorrelationId}", correlationId);

    //            // Last resort: abandon the message
    //            try
    //            {
    //                await messageActions.AbandonMessageAsync(message);
    //            }
    //            catch (Exception abandonEx)
    //            {
    //                _logger.LogError(abandonEx, "Failed to abandon message. CorrelationId: {CorrelationId}", correlationId);
    //            }
    //        }
    //    }

    //    private static string GetFileNameFromMessage(ServiceBusReceivedMessage message)
    //    {
    //        try
    //        {
    //            var request = JsonSerializer.Deserialize<FileProcessingRequest>(message.Body.ToString());
    //            return request?.FileName ?? "unknown";
    //        }
    //        catch
    //        {
    //            return "unknown";
    //        }
    //    }
    //}


}
