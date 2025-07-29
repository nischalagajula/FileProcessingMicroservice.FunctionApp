using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Azure.Messaging.ServiceBus;
using FileProcessingMicroservice.FunctionApp.Models;
using System.Text.Json;
using FileProcessingMicroservice.FunctionApp.Data.Repositories;


namespace FileProcessingMicroservice.FunctionApp.Functions

{
public class DeadLetterFunction
{
    private readonly IFileProcessingRepository _repository;
    private readonly ILogger<DeadLetterFunction> _logger;

    public DeadLetterFunction(
        FileProcessingRepository repository,
        ILogger<DeadLetterFunction> logger)
    {
            _repository = repository;
            _logger = logger;
    }

    [Function("FileUploadDeadLetterFunction")]
    public async Task ProcessFileUploadDeadLetter(
        [ServiceBusTrigger("sbq-fileupload/$DeadLetterQueue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var correlationId = message.CorrelationId ?? "unknown";

        try
        {
            _logger.LogWarning("Processing dead letter message from file upload queue. CorrelationId: {CorrelationId}, MessageId: {MessageId}, DeliveryCount: {DeliveryCount}",
                correlationId, message.MessageId, message.DeliveryCount);

            // Try to deserialize the original message
            FileProcessingRequest? originalRequest = null;
            try
            {
                originalRequest = JsonSerializer.Deserialize<FileProcessingRequest>(message.Body.ToString());
                if (originalRequest != null)
                {
                    correlationId = originalRequest.CorrelationId;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize dead letter message body. CorrelationId: {CorrelationId}", correlationId);
            }

            // Extract dead letter reason
            var deadLetterReason = GetDeadLetterReason(message);
            var deadLetterDescription = GetDeadLetterDescription(message);

            _logger.LogError("File upload message dead lettered. CorrelationId: {CorrelationId}, Reason: {Reason}, Description: {Description}, DeliveryCount: {DeliveryCount}",
                correlationId, deadLetterReason, deadLetterDescription, message.DeliveryCount);

            // Update database status if we have the correlation ID
            if (originalRequest != null)
            {
                await UpdateFileStatusToFailed(correlationId, originalRequest.FileName, deadLetterReason, deadLetterDescription);
            }

            // Log detailed information about the dead letter message
            await LogDeadLetterDetails(correlationId, originalRequest, message, deadLetterReason, deadLetterDescription);

            // Send alert/notification about the dead letter
            await SendDeadLetterAlert(correlationId, originalRequest, deadLetterReason, deadLetterDescription);

            // Complete the dead letter message
            await messageActions.CompleteMessageAsync(message);

            _logger.LogInformation("Dead letter message processed successfully. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dead letter message. CorrelationId: {CorrelationId}", correlationId);

            try
            {
                await _repository.LogProcessingEventAsync(correlationId, "DeadLetterProcessingError",
                    $"Failed to process dead letter message: {ex.Message}", "Error", ex.ToString());
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log dead letter processing error. CorrelationId: {CorrelationId}", correlationId);
            }

            // Complete the message anyway to prevent infinite retry
            await messageActions.CompleteMessageAsync(message);
        }
    }

    [Function("FileProcessingDeadLetterFunction")]
    public async Task ProcessFileProcessingDeadLetter(
        [ServiceBusTrigger("sbq-fileprocessing/$DeadLetterQueue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var correlationId = message.CorrelationId ?? "unknown";

        try
        {
            _logger.LogWarning("Processing dead letter message from file processing result queue. CorrelationId: {CorrelationId}, MessageId: {MessageId}",
                correlationId, message.MessageId);

            // Try to deserialize the result message
            FileProcessingResult? processingResult = null;
            try
            {
                processingResult = JsonSerializer.Deserialize<FileProcessingResult>(message.Body.ToString());
                if (processingResult != null)
                {
                    correlationId = processingResult.CorrelationId;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize dead letter result message body. CorrelationId: {CorrelationId}", correlationId);
            }

            var deadLetterReason = GetDeadLetterReason(message);
            var deadLetterDescription = GetDeadLetterDescription(message);

            _logger.LogError("File processing result message dead lettered. CorrelationId: {CorrelationId}, Reason: {Reason}, Description: {Description}",
                correlationId, deadLetterReason, deadLetterDescription);

                // Log the dead letter event
                await _repository.LogProcessingEventAsync(correlationId, "ResultDeadLettered",
                    $"Processing result message was dead lettered. Reason: {deadLetterReason}, Description: {deadLetterDescription}",
                    "Error", JsonSerializer.Serialize(new
                    {
                        MessageId = message.MessageId,
                        DeliveryCount = message.DeliveryCount,
                        DeadLetterReason = deadLetterReason,
                        DeadLetterDescription = deadLetterDescription,
                        OriginalResult = processingResult
                    }));

                // Send alert about the dead lettered result
                await SendResultDeadLetterAlert(correlationId, processingResult, deadLetterReason, deadLetterDescription);

            // Complete the dead letter message
            await messageActions.CompleteMessageAsync(message);

            _logger.LogInformation("Dead letter result message processed successfully. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dead letter result message. CorrelationId: {CorrelationId}", correlationId);

            // Complete the message anyway to prevent infinite retry
            await messageActions.CompleteMessageAsync(message);
        }
    }

    private async Task UpdateFileStatusToFailed(string correlationId, string fileName, string reason, string description)
    {
        try
        {
                var processedFile = await _repository.GetByCorrelationIdAsync(correlationId);
                if (processedFile != null)
                {
                    processedFile.Status = "Failed";
                    processedFile.ErrorMessage = $"Dead lettered: {reason} - {description}";
                    await _repository.UpdateAsync(processedFile);

                    _logger.LogInformation("Updated file status to Failed due to dead letter. CorrelationId: {CorrelationId}, FileName: {FileName}",
                    correlationId, fileName);
                }
                else
                {
                    _logger.LogWarning("Could not find file to update status. CorrelationId: {CorrelationId}, FileName: {FileName}",
                        correlationId, fileName);
                }
            }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file status for dead letter. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private async Task LogDeadLetterDetails(string correlationId, FileProcessingRequest? originalRequest,
        ServiceBusReceivedMessage message, string reason, string description)
    {
        try
        {
            var deadLetterDetails = new
            {
                MessageId = message.MessageId,
                CorrelationId = correlationId,
                DeliveryCount = message.DeliveryCount,
                EnqueuedTime = message.EnqueuedTime,
                ExpiresAt = message.ExpiresAt,
                DeadLetterReason = reason,
                DeadLetterDescription = description,
                OriginalFileName = originalRequest?.FileName,
                OriginalBlobName = originalRequest?.BlobName,
                MessageSize = message.Body.ToMemory().Length,
                ApplicationProperties = message.ApplicationProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
            };

                await _repository.LogProcessingEventAsync(correlationId, "MessageDeadLettered",
                    $"Message dead lettered after {message.DeliveryCount} delivery attempts. File: {originalRequest?.FileName ?? "unknown"}",
                    "Error", JsonSerializer.Serialize(deadLetterDetails));

                _logger.LogInformation("Logged dead letter details. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging dead letter details. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private async Task SendDeadLetterAlert(string correlationId, FileProcessingRequest? originalRequest,
        string reason, string description)
    {
        try
        {
            // TODO: Implement actual alerting mechanism
            // This could include:
            // - Sending emails to administrators
            // - Posting to Slack/Teams channels
            // - Creating support tickets
            // - Updating monitoring dashboards
            // - Triggering PagerDuty alerts

            _logger.LogWarning("ALERT: Dead letter detected for file processing. CorrelationId: {CorrelationId}, FileName: {FileName}, Reason: {Reason}",
                correlationId, originalRequest?.FileName ?? "unknown", reason);

            // Placeholder for actual alert implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending dead letter alert. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private async Task SendResultDeadLetterAlert(string correlationId, FileProcessingResult? processingResult,
        string reason, string description)
    {
        try
        {
            // TODO: Implement result dead letter alerting
            _logger.LogWarning("ALERT: Processing result dead letter detected. CorrelationId: {CorrelationId}, OriginalFileName: {OriginalFileName}, Reason: {Reason}",
                correlationId, processingResult?.OriginalFileName ?? "unknown", reason);

            // Placeholder for actual alert implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending result dead letter alert. CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private static string GetDeadLetterReason(ServiceBusReceivedMessage message)
    {
        return message.ApplicationProperties.TryGetValue("DeadLetterReason", out var reason)
            ? reason?.ToString() ?? "Unknown"
            : "Unknown";
    }

    private static string GetDeadLetterDescription(ServiceBusReceivedMessage message)
    {
        return message.ApplicationProperties.TryGetValue("DeadLetterErrorDescription", out var description)
            ? description?.ToString() ?? "No description available"
            : "No description available";
    }
}
}