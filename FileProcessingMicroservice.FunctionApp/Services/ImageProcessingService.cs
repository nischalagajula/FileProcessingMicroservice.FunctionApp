using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Syncfusion.Pdf;



namespace FileProcessingMicroservice.FunctionApp.Services
{
    
    public class ImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<ProcessingOutcome> ConvertToPngAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Starting image to PNG conversion for file: {FileName}", context.FileName);

                context.InputStream.Position = 0;

                // Load image
                using var image = await Image.LoadAsync(context.InputStream);

                // Optional: Resize if image is too large (to save storage space)
                const int maxWidth = 2048;
                const int maxHeight = 2048;

                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    _logger.LogInformation("Resizing image {FileName} from {OriginalWidth}x{OriginalHeight}",
                        context.FileName, image.Width, image.Height);

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(maxWidth, maxHeight),
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));
                }

                // Convert to PNG
                var outputStream = new MemoryStream();
                var pngEncoder = new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression,
                    ColorType = PngColorType.RgbWithAlpha
                };

                await image.SaveAsync(outputStream, pngEncoder);
                outputStream.Position = 0;

                var outputFileName = Path.GetFileNameWithoutExtension(context.FileName) + ".png";

                _logger.LogInformation("Successfully converted image to PNG: {OriginalFile} -> {ConvertedFile} (Size: {Width}x{Height})",
                    context.FileName, outputFileName, image.Width, image.Height);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = "image/png",
                    ProcessorType = "ImageToPng"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert image to PNG for file: {FileName}", context.FileName);
                throw new InvalidOperationException($"Image to PNG conversion failed: {ex.Message}", ex);
            }
        }

        public async Task<ProcessingOutcome> OptimizeImageAsync(FileContext context)
        {
            try
            {
                _logger.LogInformation("Starting image optimization for file: {FileName}", context.FileName);

                context.InputStream.Position = 0;

                using var image = await Image.LoadAsync(context.InputStream);

                // Apply optimization
                image.Mutate(x => x
                    .AutoOrient() // Fix rotation based on EXIF data
                    .Resize(new ResizeOptions
                    {
                        Size = new Size(1920, 1080), // HD resolution
                        Mode = ResizeMode.Max,
                        Sampler = KnownResamplers.Lanczos3
                    }));

                var outputStream = new MemoryStream();
                var extension = Path.GetExtension(context.FileName).ToLowerInvariant();

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        await image.SaveAsJpegAsync(outputStream);
                        break;
                    case ".png":
                        await image.SaveAsPngAsync(outputStream);
                        break;
                    default:
                        // Default to PNG for unknown formats
                        await image.SaveAsPngAsync(outputStream);
                        break;
                }

                outputStream.Position = 0;

                var outputFileName = $"optimized_{context.FileName}";

                _logger.LogInformation("Successfully optimized image: {OriginalFile} -> {ConvertedFile}",
                    context.FileName, outputFileName);

                return new ProcessingOutcome
                {
                    FileName = outputFileName,
                    FileStream = outputStream,
                    ContentType = GetContentType(extension),
                    ProcessorType = "ImageOptimizer"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize image for file: {FileName}", context.FileName);
                throw new InvalidOperationException($"Image optimization failed: {ex.Message}", ex);
            }
        }

        private static string GetContentType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/png"
            };
        }
    }
}
