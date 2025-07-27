using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using System.Drawing;


namespace FileProcessingMicroservice.FunctionApp.Services
{
    
    public class TextToPdfService
    {
        private readonly ILogger<TextToPdfService> _logger;

        public TextToPdfService(ILogger<TextToPdfService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessingOutcome> ConvertTextToPdfAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Starting text to PDF conversion for file: {FileName}", context.FileName);

                context.InputStream.Position = 0;

                // Read text content
                using var reader = new StreamReader(context.InputStream);
                string textContent = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(textContent))
                {
                    throw new InvalidOperationException("Text file is empty or contains only whitespace");
                }

                // Create PDF document
                using var pdfDocument = new PdfDocument();
                var page = pdfDocument.Pages.Add();
                var graphics = page.Graphics;

                // Set up fonts and layout
                var titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
                var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 11);
                var margin = 40f;
                var pageWidth = page.GetClientSize().Width;
                var pageHeight = page.GetClientSize().Height;
                var currentY = margin;

                // Add title
                var fileName = Path.GetFileNameWithoutExtension(context.FileName);
                graphics.DrawString($"Document: {fileName}", titleFont, PdfBrushes.Black,
                    new Syncfusion.Drawing.PointF(margin, currentY));
                currentY += 30;

                // Add creation date
                graphics.DrawString($"Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", bodyFont, PdfBrushes.Gray,
                    new Syncfusion.Drawing.PointF(margin, currentY));
                currentY += 25;

                // Draw separator line
                graphics.DrawLine(new PdfPen(PdfBrushes.LightGray),
                    new Syncfusion.Drawing.PointF(margin, currentY), new Syncfusion.Drawing.PointF(pageWidth - margin, currentY));
                currentY += 20;

                // Split text into lines and pages
                var lines = textContent.Split('\n');
                const float lineHeight = 14f;
                const float bottomMargin = 40f;

                foreach (var line in lines)
                {
                    // Check if we need a new page
                    if (currentY + lineHeight > pageHeight - bottomMargin)
                    {
                        page = pdfDocument.Pages.Add();
                        graphics = page.Graphics;
                        currentY = margin;
                    }

                    // Handle long lines that might need wrapping
                    var processedLine = line.Replace('\r', ' ').Trim();
                    if (string.IsNullOrEmpty(processedLine))
                    {
                        currentY += lineHeight / 2; // Empty line spacing
                        continue;
                    }

                    // Simple word wrapping
                    var words = processedLine.Split(' ');
                    var currentLine = "";

                    foreach (var word in words)
                    {
                        var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                        var textSize = bodyFont.MeasureString(testLine);

                        if (textSize.Width > pageWidth - (2 * margin))
                        {
                            // Draw current line and start new one
                            if (!string.IsNullOrEmpty(currentLine))
                            {
                                graphics.DrawString(currentLine, bodyFont, PdfBrushes.Black,
                                    new Syncfusion.Drawing.PointF(margin, currentY));
                                currentY += lineHeight;

                                // Check for new page
                                if (currentY + lineHeight > pageHeight - bottomMargin)
                                {
                                    page = pdfDocument.Pages.Add();
                                    graphics = page.Graphics;
                                    currentY = margin;
                                }
                            }
                            currentLine = word;
                        }
                        else
                        {
                            currentLine = testLine;
                        }
                    }

                    // Draw remaining text
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        graphics.DrawString(currentLine, bodyFont, PdfBrushes.Black,
                            new Syncfusion.Drawing.PointF(margin, currentY));
                        currentY += lineHeight;
                    }
                }

                // Save to stream
                var outputStream = new MemoryStream();
                pdfDocument.Save(outputStream);
                outputStream.Position = 0;

                var outputFileName = Path.GetFileNameWithoutExtension(context.FileName) + ".pdf";

                _logger.LogInformation("Successfully converted text to PDF: {OriginalFile} -> {ConvertedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "application/pdf",
                    ProcessorType = "TextToPdf"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert text to PDF for file: {FileName}", context.FileName);
                throw new InvalidOperationException($"Text to PDF conversion failed: {ex.Message}", ex);
            }
        }
    }
}
