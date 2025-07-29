using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileProcessingMicroservice.FunctionApp.Models
{
    
    public class FileProcessingRequest
    {
        public string BlobName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FileProcessingResult
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ProcessedFileName { get; set; } = string.Empty;
        //public string ResultBlobUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string ProcessorType { get; set; } = string.Empty;
    }

    public class FileStatusResponse
    {
        public string FileName { get; set; } = string.Empty;
        //public string UploadedUrl { get; set; } = string.Empty;
        //public string? ProcessedUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ProcessedFileName { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ProcessorType { get; set; }
    }

    public class FileUploadRequest
    {
        public string? FileName { get; set; }
        public byte[]? FileContent { get; set; }
    }

    //public class ProcessingOutcome
    //{
    //    public string FileName { get; set; } = string.Empty;
    //    public Stream FileStream { get; set; } = Stream.Null;
    //    public string ContentType { get; set; } = string.Empty;
    //    public string ProcessorType { get; set; } = string.Empty;
    //    public string ResultUrl { get; set; } = string.Empty;
    //}
    #region ProcessingOutCome.cs
    public class ProcessingOutcome
    {
        //public bool? IsSuccess { get; }
        //public string Message { get; }
        //public List<string> Errors { get; }
        //public List<string> Warnings { get; }
        //public string ProcessedContent { get; }
        //public Dictionary<string, object> Metadata { get; }
        //public TimeSpan ProcessingTime { get; }
        public string FileName { get; set; } = string.Empty;
        public Stream FileStream { get; set; } = Stream.Null;
        public string ContentType { get; set; } = string.Empty;
        public string ProcessorType { get; set; } = string.Empty;
        public string ResultUrl { get; set; } = string.Empty;
      //  }
        //private ProcessingOutcome(bool isSuccess, string message, List<string> errors,
        //    List<string> warnings, string processedContent, Dictionary<string, object> metadata,
        //    TimeSpan processingTime)
        //{
        //    IsSuccess = isSuccess;
        //    Message = message;
        //    Errors = errors ?? new List<string>();
        //    Warnings = warnings ?? new List<string>();
        //    ProcessedContent = processedContent;
        //    Metadata = metadata ?? new Dictionary<string, object>();
        //    ProcessingTime = processingTime;
        //}

        //public static ProcessingOutcome Success(string processedContent, string message = "Processing completed successfully",
        //    List<string> warnings = null, Dictionary<string, object> metadata = null, TimeSpan processingTime = default)
        //    => new(true, message, null, warnings, processedContent, metadata, processingTime);

        //public static Processing message = "Processing failed",
        //    Dictionary<string, object> metadata = null, TimeSpan processingTime = default)
        //=> new (false, message, errors, null, null, metadata, processingTime);

    //public static ProcessingOutcome Failure(string error, string message = "Processing failed",
    //    Dictionary<string, object> metadata = null, TimeSpan processingTime = default)
    //    => new(false, message, new List<string> { error }, null, null, metadata, processingTime);
    }
    #endregion

    //public class FileContext
    //{
    //    public Stream InputStream { get; set; }
    //    public string FileName { get; set; }
    //    public string ContentType { get; set; }

    //    public FileContext(Stream inputStream, string fileName, string contentType = "")
    //    {
    //        InputStream = inputStream;
    //        FileName = fileName;
    //        ContentType = contentType;
    //    }
    //}

    #region filecontext.cs
    public class FileContext
    {
        public Stream InputStream { get; }
        public string FileName { get; }
        public string ContentType { get; }
        public Dictionary<string, object> Properties { get; }
        public string CorrelationId { get; }

        public FileContext(Stream inputStream, string fileName, string contentType = null,
            Dictionary<string, object> properties = null, string correlationId = null)
        {
            InputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            ContentType = contentType ?? "application/xml";
            Properties = properties ?? new Dictionary<string, object>();
            CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        }
    }
    #endregion

    #region Models/XmlValidationError.cs
    public class XmlValidationError
    {
        public string ErrorType { get; set; }
        public string Description { get; set; }
        public int LineNumber { get; set; }
        public int LinePosition { get; set; }
        public string OriginalText { get; set; }
        public string CorrectedText { get; set; }
        public string Severity { get; set; } // Error, Warning, Info
    }
    #endregion
}
