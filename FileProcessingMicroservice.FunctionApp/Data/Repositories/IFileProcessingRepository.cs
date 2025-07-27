using FileProcessingMicroservice.FunctionApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileProcessingMicroservice.FunctionApp.Data.Repositories
{
    public interface IFileProcessingRepository
    {
        Task<ProcessedFile?> GetByCorrelationIdAsync(string correlationId);
        Task<ProcessedFile?> GetByFileNameAsync(string fileName);
        Task<List<ProcessedFile>> GetByStatusAsync(string status);
        //Task<ProcessedFile> CreateAsync(ProcessedFile processedFile);
        Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile);
        Task DeleteAsync(int id);
        Task<List<ProcessedFile>> GetRecentFilesAsync(int count = 10);
        Task LogProcessingEventAsync(string correlationId, string eventType, string message, string logLevel = "Info", string? additionalData = null);
        Task<List<ProcessingLog>> GetProcessingLogsAsync(string correlationId);
    }

}
