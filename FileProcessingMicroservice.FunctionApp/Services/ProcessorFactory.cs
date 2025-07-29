using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;



namespace FileProcessingMicroservice.FunctionApp.Services
{

    public class ProcessorFactory
    {
        private readonly DocumentConversionService _documentConverter;
        private readonly TextToPdfService _textToPdfService;
        private readonly ImageProcessingService _imageProcessor;
        private readonly ILogger<ProcessorFactory> _logger;

        public ProcessorFactory(
            DocumentConversionService documentConverter,
            TextToPdfService textToPdfService,
            ImageProcessingService imageProcessor,
            ILogger<ProcessorFactory> logger)
        {
            _documentConverter = documentConverter;
            _textToPdfService = textToPdfService;
            _imageProcessor = imageProcessor;
            _logger = logger;
        }

        public async Task<ProcessingOutcome> ProcessAsync(FileContext context)
        {
            var extension = Path.GetExtension(context.FileName).ToLowerInvariant();

            _logger.LogInformation("Processing file {FileName} with extension {Extension}", context.FileName, extension);

            try
            {
                return extension switch
                {
                    // Document conversions to PDF
                    ".docx" => await _documentConverter.ConvertDocxToPdfAsync(context),
                    ".doc" => await _documentConverter.ConvertDocToPdfAsync(context),

                    // Text to PDF conversion
                    ".txt" => await _textToPdfService.ConvertTextToPdfAsync(context),

                    // Image conversions to PNG
                    ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif" or ".gif"
                        => await _imageProcessor.ConvertToPngAsync(context),

                    // Structured data validation (pass-through for now)
                    ".json" => await ProcessJsonAsync(context),
                    ".xml" => await ProcessXmlAsync(context),

                    // Default: unsupported file type
                    _ => throw new NotSupportedException($"File type '{extension}' is not supported for processing")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process file {FileName} with extension {Extension}", context.FileName, extension);
                throw;
            }
        }

        public bool IsSupported(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".docx" or ".doc" or ".txt" or
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif" or ".gif" or
                ".json" or ".xml" => true,
                _ => false
            };
        }

        public string GetProcessorType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".docx" => "DocxToPdf",
                ".doc" => "DocToPdf",
                ".txt" => "TextToPdf",
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" or ".tif" or ".gif" => "ImageToPng",
                ".json" => "JsonValidator",
                ".xml" => "XmlValidator",
                _ => "Unknown"
            };
        }

        public string GetExpectedOutputExtension(string inputFileName)
        {
            var extension = Path.GetExtension(inputFileName).ToLowerInvariant();
            return extension switch
            {
                ".docx" or ".doc" or ".txt" => ".pdf",
                ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".tif" or ".gif" => ".png",
                ".png" => ".png", // PNG to PNG (optimization)
                ".json" => ".json", // Validated JSON
                ".xml" => ".xml", // Validated XML
                _ => inputFileName // Fallback to original extension
            };
        }

        private async Task<ProcessingOutcome> ProcessJsonAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Validating JSON file: {FileName}", context.FileName);

                context.InputStream.Position = 0;
                using var reader = new StreamReader(context.InputStream);
                string jsonContent = await reader.ReadToEndAsync();

                // Validate JSON by attempting to parse it
                System.Text.Json.JsonDocument.Parse(jsonContent);

                // Create validated output
                var outputStream = new MemoryStream();
                await using var writer = new StreamWriter(outputStream, leaveOpen: true);
                await writer.WriteAsync(jsonContent);
                await writer.FlushAsync();
                outputStream.Position = 0;

                //var outputFileName = $"validated_{context.FileName}";
                var outputFileName = $"{context.FileName}";

                _logger.LogInformation("Successfully validated JSON file: {OriginalFile} -> {ValidatedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "application/json",
                    ProcessorType = "JsonValidator"
                };
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in file: {FileName}", context.FileName);
                throw new InvalidOperationException($"JSON validation failed: {ex.Message}", ex);
            }
        }

        private async Task<ProcessingOutcome> ProcessXmlAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Validating XML file: {FileName}", context.FileName);

                context.InputStream.Position = 0;
                using var reader = new StreamReader(context.InputStream);
                string xmlContent = await reader.ReadToEndAsync();

                // Validate XML by attempting to parse it
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.LoadXml(xmlContent);

                // Create validated output
                var outputStream = new MemoryStream();
                await using var writer = new StreamWriter(outputStream, leaveOpen: true);
                await writer.WriteAsync(xmlContent);
                await writer.FlushAsync();
                outputStream.Position = 0;

                var outputFileName = $"{context.FileName}";
                //var outputFileName = $"validated_{context.FileName}";

                _logger.LogInformation("Successfully validated XML file: {OriginalFile} -> {ValidatedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "application/xml",
                    ProcessorType = "XmlValidator"
                };
            }
            catch (System.Xml.XmlException ex)
            {
                _logger.LogError(ex, "Invalid XML in file: {FileName}", context.FileName);
                throw new InvalidOperationException($"XML validation failed: {ex.Message}", ex);
            }
        }

        //private async Task<ProcessingOutcome> ProcessXmlAsync(FileContext context)
        //{
        //    // Assume context.ContentStream contains the raw XML
        //    try
        //    {
        //        _logger.LogInformation("validating xml file: {filename}", context.FileName);

        //        // Load the XML document
        //        //XDocument doc;
        //        XmlDocument doc;
        //        using (var reader = XmlReader.Create(context.ContentStream))
        //        {
        //            doc = XDocument.Load(reader);
        //        }

        //        // Validate structure: ensure proper root, tags are present, and spelled correctly
        //        // (You need a list or schema of valid tags, e.g.)
        //        var validTags = new HashSet<string> { "Invoice", "Header", "Item", "Amount", "Footer" };
        //        var errors = new List<string>();

        //        // Recursive validation method
        //        void ValidateElement(XElement element)
        //        {
        //            if (!validTags.Contains(element.Name.LocalName))
        //            {
        //                errors.Add($"Invalid tag: {element.Name.LocalName}");
        //                // Optionally, fix misspelled tags here
        //            }

        //            foreach (var child in element.Elements())
        //            {
        //                ValidateElement(child);
        //            }
        //        }

        //        // Enforce proper root and validate tags
        //        if (doc.Root == null)
        //            throw new Exception("XML has no root element");
        //        ValidateElement(doc.Root);

        //        // Optionally, fix invalid tags:
        //        // For simplicity, suppose we only log errors
        //        if (errors.Count > 0)
        //        {
        //            // Decide whether to throw exception, log, or fix tags
        //            // For now, just throw
        //            throw new Exception("XML validation errors: " + string.Join("; ", errors));
        //        }

        //        // Save the validated and corrected XML (if fixes are made)
        //        var outputStream = new MemoryStream();
        //        using (var writer = XmlWriter.Create(outputStream, new XmlWriterSettings { Indent = true }))
        //        {
        //            doc.WriteTo(writer);
        //        }
        //        outputStream.Position = 0;

        //        return new ProcessingOutcome
        //        {
        //            Success = true,
        //            ValidatedXmlStream = outputStream,
        //            Message = "XML validated and structure maintained"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log and handle errors
        //        return new ProcessingOutcome
        //        {
        //            Success = false,
        //            Message = "XML validation failed: " + ex.Message
        //        };
        //    }
        //}

    }
}
