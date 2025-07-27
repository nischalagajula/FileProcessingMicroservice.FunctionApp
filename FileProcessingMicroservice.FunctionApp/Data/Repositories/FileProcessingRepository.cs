using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.EntityFrameworkCore;




namespace FileProcessingMicroservice.FunctionApp.Data.Repositories
{
    
    public class FileProcessingRepository : IFileProcessingRepository
    {
        private readonly FileProcessingDbContext _context;

        public FileProcessingRepository(FileProcessingDbContext context)
        {
            _context = context;
        }

        public async Task<ProcessedFile?> GetByCorrelationIdAsync(string correlationId)
        {
            return await _context.ProcessedFiles
                .FirstOrDefaultAsync(f => f.CorrelationId == correlationId);
        }

        public async Task<ProcessedFile?> GetByFileNameAsync(string fileName)
        {
            return await _context.ProcessedFiles
                .FirstOrDefaultAsync(f => f.OriginalFileName == fileName);
        }

        public async Task<List<ProcessedFile>> GetByStatusAsync(string status)
        {
            return await _context.ProcessedFiles
                .Where(f => f.Status == status)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProcessedFile> CreateAsync(ProcessedFile processedFile)
        {
            _context.ProcessedFiles.Add(processedFile);
            await _context.SaveChangesAsync();
            return processedFile;
        }

        public async Task<ProcessedFile> UpdateAsync(ProcessedFile processedFile)
        {
            processedFile.UpdatedAt = DateTime.UtcNow;
            _context.ProcessedFiles.Update(processedFile);
            await _context.SaveChangesAsync();
            return processedFile;
        }

        public async Task DeleteAsync(int id)
        {
            var file = await _context.ProcessedFiles.FindAsync(id);
            if (file != null)
            {
                _context.ProcessedFiles.Remove(file);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ProcessedFile>> GetRecentFilesAsync(int count = 10)
        {
            return await _context.ProcessedFiles
                .OrderByDescending(f => f.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task LogProcessingEventAsync(string correlationId, string eventType, string message, string logLevel = "Info", string? additionalData = null)
        {
            var log = new ProcessingLog
            {
                CorrelationId = correlationId,
                EventType = eventType,
                Message = message,
                LogLevel = logLevel,
                AdditionalData = additionalData,
                Timestamp = DateTime.UtcNow
            };

            _context.ProcessingLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ProcessingLog>> GetProcessingLogsAsync(string correlationId)
        {
            return await _context.ProcessingLogs
                .Where(l => l.CorrelationId == correlationId)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }
    }
}
