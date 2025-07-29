using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace FileProcessingMicroservice.FunctionApp.Models
{

    [Table("ProcessedFiles")]
    public class ProcessedFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string CorrelationId { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string OriginalFileName { get; set; } = string.Empty;

        [StringLength(500)]
        public string ProcessedFileName { get; set; } = string.Empty;

        //[Required]
        //[StringLength(1000)]
        //public string OriginalBlobUrl { get; set; } = string.Empty;

        //[StringLength(1000)]
        //public string ProcessedBlobUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;

        [StringLength(100)]
        public string ProcessorType { get; set; } = string.Empty;

        [StringLength(500)]
        public string ContentType { get; set; } = string.Empty;

        public long OriginalFileSize { get; set; }

        public long ProcessedFileSize { get; set; }

        [StringLength(2000)]
        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("ProcessingLogs")]
    public class ProcessingLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string CorrelationId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EventType { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [StringLength(50)]
        public string LogLevel { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(4000)]
        public string? AdditionalData { get; set; }
    }
}
