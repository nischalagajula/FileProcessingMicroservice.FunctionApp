using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;


namespace FileProcessingMicroservice.FunctionApp.Services
{
    

    public class DocumentConversionService
    {
        private readonly ILogger<DocumentConversionService> _logger;

        public DocumentConversionService(ILogger<DocumentConversionService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessingOutcome> ConvertDocxToPdfAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Starting DOCX to PDF conversion for file: {FileName}", context.FileName);

                context.InputStream.Position = 0;

                // Load DOCX document
                using var wordDocument = new WordDocument(context.InputStream, FormatType.Automatic);

                // Create DocIO renderer for converting to PDF
                using var renderer = new DocIORenderer();

                // Convert Word document to PDF
                using var pdfDocument = renderer.ConvertToPDF(wordDocument);

                // Save PDF to memory stream
                var outputStream = new MemoryStream();
                pdfDocument.Save(outputStream);
                outputStream.Position = 0;

                var outputFileName = Path.GetFileNameWithoutExtension(context.FileName) + ".pdf";

                _logger.LogInformation("Successfully converted DOCX to PDF: {OriginalFile} -> {ConvertedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "application/pdf",
                    ProcessorType = "DocxToPdf"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert DOCX to PDF for file: {FileName}", context.FileName);
                throw new InvalidOperationException($"DOCX to PDF conversion failed: {ex.Message}", ex);
            }
        }

        public async Task<ProcessingOutcome> ConvertDocToPdfAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Starting DOC to PDF conversion for file: {FileName}", context.FileName);

                context.InputStream.Position = 0;

                // Load DOC document
                using var wordDocument = new WordDocument(context.InputStream, FormatType.Doc);

                // Create DocIO renderer for converting to PDF
                using var renderer = new DocIORenderer();

                // Convert Word document to PDF
                using var pdfDocument = renderer.ConvertToPDF(wordDocument);

                // Save PDF to memory stream
                var outputStream = new MemoryStream();
                pdfDocument.Save(outputStream);
                outputStream.Position = 0;

                var outputFileName = Path.GetFileNameWithoutExtension(context.FileName) + ".pdf";

                _logger.LogInformation("Successfully converted DOC to PDF: {OriginalFile} -> {ConvertedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "application/pdf",
                    ProcessorType = "DocToPdf"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert DOC to PDF for file: {FileName}", context.FileName);
                throw new InvalidOperationException($"DOC to PDF conversion failed: {ex.Message}", ex);
            }
        }
    }


}
