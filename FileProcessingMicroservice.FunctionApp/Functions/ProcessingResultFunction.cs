using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;


using Azure.Messaging.ServiceBus;
using FileProcessingMicroservice.FunctionApp.Data.Repositories;
using FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Data.Repositories;
using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;



namespace FileProcessingMicroservice.FunctionApp.Functions
{
public class ProcessingResultFunction
{
        private readonly IFileProcessingRepository _repository;
        private readonly ILogger<ProcessingResultFunction> _logger;

    public ProcessingResultFunction(
        IFileProcessingRepository repository,
        ILogger<ProcessingResultFunction> logger)
    {
            _repository = repository;
            _logger = logger;
    }

    [Function("ProcessingResultFunction")]
    public async Task Run(
        [ServiceBusTrigger("sbq-fileprocessing", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var correlationId = message.CorrelationId ?? "unknown";

        try
        {
            _logger.LogInformation("Processing result message. CorrelationId: {CorrelationId}, MessageId: {MessageId}",
                correlationId, message.MessageId);

            // Deserialize the result message
            var result = JsonSerializer.Deserialize<FileProcessingResult>(message.Body.ToString());
            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize file processing result");
            }

            correlationId = result.CorrelationId;

            // Log the result based on status
            if (result.Status == "Processed")
            {
                await HandleSuccessfulProcessing(result);
            }
            else if (result.Status == "Failed")
            {
                await HandleFailedProcessing(result);
            }
            else
            {
                _logger.LogWarning("Received result with unknown status. CorrelationId: {CorrelationId}, Status: {Status}",
                    correlationId, result.Status);
            }

            // Complete the message
            await messageActions.CompleteMessageAsync(message);

            _logger.LogInformation("Result message processed successfully. CorrelationId: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing result message. CorrelationId: {CorrelationId}", correlationId);

                try
                {
                    await _repository.LogProcessingEventAsync(correlationId, "ResultProcessingError",
                        $"Failed to process result message: {ex.Message}", "Error", ex.ToString());
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log result processing error. CorrelationId: {CorrelationId}", correlationId);
                }

                //Abandon the message for retry
               await messageActions.AbandonMessageAsync(message);
        }
    }

    private async Task HandleSuccessfulProcessing(FileProcessingResult result)
    {
        try
        {
            _logger.LogInformation("Handling successful processing result. CorrelationId: {CorrelationId}, OriginalFile: {OriginalFile}, ProcessedFile: {ProcessedFile}",
                result.CorrelationId, result.OriginalFileName, result.ProcessedFileName);

                // Log the successful completion
                await _repository.LogProcessingEventAsync(result.CorrelationId, "ProcessingCompleted",
                    $"File processing completed successfully. Original: {result.OriginalFileName}, Processed: {result.ProcessedFileName}, Processor: {result.ProcessorType}",
                    "Info",
                    JsonSerializer.Serialize(new
                    {
                        OriginalFileName = result.OriginalFileName,
                        ProcessedFileName = result.ProcessedFileName,
                        ProcessorType = result.ProcessorType,
                        ProcessedAt = result.ProcessedAt
                    }));

                // TODO: Add additional success handling here, such as:
                // - Send notifications to external systems
                // - Update external databases
                // - Trigger downstream workflows
                // - Send email notifications to users
                // - Update dashboards or monitoring systems

                await SendNotificationIfNeeded(result);
            await UpdateExternalSystemsIfNeeded(result);

            _logger.LogInformation("Successfully handled processing completion. CorrelationId: {CorrelationId}", result.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling successful processing result. CorrelationId: {CorrelationId}", result.CorrelationId);
            throw;
        }
    }

    private async Task HandleFailedProcessing(FileProcessingResult result)
    {
        try
        {
            _logger.LogWarning("Handling failed processing result. CorrelationId: {CorrelationId}, OriginalFile: {OriginalFile}, Error: {ErrorMessage}",
                result.CorrelationId, result.OriginalFileName, result.Message);

                // Log the failure
                await _repository.LogProcessingEventAsync(result.CorrelationId, "ProcessingFailed",
                    $"File processing failed. Original: {result.OriginalFileName}, Error: {result.Message}",
                    "Error",
                    JsonSerializer.Serialize(new
                    {
                        OriginalFileName = result.OriginalFileName,
                        ErrorMessage = result.Message,
                        ProcessorType = result.ProcessorType,
                        ProcessedAt = result.ProcessedAt
                    }));

                // TODO: Add additional failure handling here, such as:
                // - Send error notifications to administrators
                // - Update monitoring dashboards
                // - Trigger alerting systems
                // - Queue for manual review
                // - Send user notifications about failure

                await SendErrorNotificationIfNeeded(result);
            await UpdateMonitoringSystemsIfNeeded(result);

            _logger.LogInformation("Successfully handled processing failure. CorrelationId: {CorrelationId}", result.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling failed processing result. CorrelationId: {CorrelationId}", result.CorrelationId);
            throw;
        }
    }

    private async Task SendNotificationIfNeeded(FileProcessingResult result)
    {
        try
        {
            // TODO: Implement notification logic
            // This could include:
            // - Sending emails via SendGrid or similar
            // - Posting to webhook endpoints
            // - Updating user interfaces via SignalR
            // - Sending SMS notifications
            // - Publishing to other message queues

            _logger.LogInformation("Notification would be sent for successful processing. CorrelationId: {CorrelationId}", result.CorrelationId);

            // Placeholder for actual notification implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending success notification. CorrelationId: {CorrelationId}", result.CorrelationId);
            // Don't rethrow - notification failures shouldn't fail the entire process
        }
    }

    private async Task SendErrorNotificationIfNeeded(FileProcessingResult result)
    {
        try
        {
            // TODO: Implement error notification logic
            // This could include:
            // - Sending error alerts to administrators
            // - Posting to error tracking systems like Sentry
            // - Updating status dashboards
            // - Creating support tickets

            _logger.LogWarning("Error notification would be sent for failed processing. CorrelationId: {CorrelationId}", result.CorrelationId);

            // Placeholder for actual error notification implementation
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending failure notification. CorrelationId: {CorrelationId}", result.CorrelationId);
            // Don't rethrow - notification failures shouldn't fail the entire process
        }
    }

    private async Task UpdateExternalSystemsIfNeeded(FileProcessingResult result)
    {
        try
        {
            // TODO: Implement external system updates
            // This could include:
            // - Updating CRM systems
            // - Posting to REST APIs
            // - Updating document management systems
            // - Publishing events to event streaming platforms

            _logger.LogInformation("External systems would be updated for successful processing. CorrelationId: {CorrelationId}", result.CorrelationId);

            // Placeholder for actual external system integration
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating external systems. CorrelationId: {CorrelationId}", result.CorrelationId);
            // Don't rethrow - external system failures shouldn't fail the entire process
        }
    }

    private async Task UpdateMonitoringSystemsIfNeeded(FileProcessingResult result)
    {
        try
        {
            // TODO: Implement monitoring system updates
            // This could include:
            // - Updating Prometheus metrics
            // - Posting to application monitoring dashboards
            // - Updating business intelligence systems
            // - Recording failure statistics

            _logger.LogInformation("Monitoring systems would be updated for failed processing. CorrelationId: {CorrelationId}", result.CorrelationId);

            // Placeholder for actual monitoring system integration
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating monitoring systems. CorrelationId: {CorrelationId}", result.CorrelationId);
            // Don't rethrow - monitoring failures shouldn't fail the entire process
        }
    }
}
}