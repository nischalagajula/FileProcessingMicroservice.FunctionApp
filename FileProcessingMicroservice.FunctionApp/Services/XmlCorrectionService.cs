using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
// Services/XmlCorrectionService.cs
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FileProcessingMicroservice.FunctionApp.Services
{
    
    public interface IXmlCorrectionService
    {
        Task<XmlCorrectionResult> CorrectXmlStructureAsync(string xmlContent, CancellationToken cancellationToken = default);
    }

    public class XmlCorrectionResult
    {
        public string CorrectedXml { get; set; }
        public List<XmlValidationError> Corrections { get; set; } = new();
        public bool RequiredCorrections { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    public class XmlCorrectionService : IXmlCorrectionService
    {
        private readonly ILogger<XmlCorrectionService> _logger;
        private readonly Dictionary<string, string> _commonTagCorrections;

        // Common misspellings and their corrections
        private static readonly Dictionary<string, string> DefaultTagCorrections = new()
    {
        { "titl", "title" },
        { "autor", "author" },
        { "adress", "address" },
        { "recieve", "receive" },
        { "seperate", "separate" },
        { "occured", "occurred" },
        { "begining", "beginning" },
        { "calender", "calendar" },
        { "lenght", "length" },
        { "widht", "width" }
    };

        public XmlCorrectionService(ILogger<XmlCorrectionService> logger,
            Dictionary<string, string> customTagCorrections = null)
        {
            _logger = logger;
            _commonTagCorrections = DefaultTagCorrections
                .Concat(customTagCorrections ?? new Dictionary<string, string>())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public async Task<XmlCorrectionResult> CorrectXmlStructureAsync(string xmlContent,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new XmlCorrectionResult();

            try
            {
                _logger.LogInformation("Starting XML structure correction");

                // Step 1: Basic syntax corrections
                var basicCorrected = await PerformBasicCorrectionsAsync(xmlContent, result, cancellationToken);

                // Step 2: Tag spelling corrections
                var spellingCorrected = await CorrectTagSpellingAsync(basicCorrected, result, cancellationToken);

                // Step 3: Structure validation and repair
                var structureCorrected = await RepairXmlStructureAsync(spellingCorrected, result, cancellationToken);

                // Step 4: Final validation
                var finalValidated = await ValidateAndFinalizeAsync(structureCorrected, result, cancellationToken);

                result.CorrectedXml = finalValidated;
                result.RequiredCorrections = result.Corrections.Any();

                _logger.LogInformation("XML correction completed. Applied {CorrectionCount} corrections",
                    result.Corrections.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during XML correction process");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
            }
        }

        private async Task<string> PerformBasicCorrectionsAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            var corrected = xmlContent;

            // Fix common encoding issues
            corrected = FixEncodingIssues(corrected, result);

            // Fix unclosed self-closing tags
            corrected = FixSelfClosingTags(corrected, result);

            // Fix mismatched quotes in attributes
            corrected = FixAttributeQuotes(corrected, result);

            // Escape invalid characters
            corrected = EscapeInvalidCharacters(corrected, result);

            return await Task.FromResult(corrected);
        }

        private async Task<string> CorrectTagSpellingAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            var corrected = xmlContent;

            foreach (var correction in _commonTagCorrections)
            {
                // Match opening tags
                var openingPattern = $@"<{Regex.Escape(correction.Key)}(\s[^>]*)?>";
                var openingMatches = Regex.Matches(corrected, openingPattern, RegexOptions.IgnoreCase);

                foreach (Match match in openingMatches)
                {
                    var correctedTag = match.Value.Replace(correction.Key, correction.Value);
                    corrected = corrected.Replace(match.Value, correctedTag);

                    result.Corrections.Add(new XmlValidationError
                    {
                        ErrorType = "TagSpelling",
                        Description = $"Corrected misspelled tag '{correction.Key}' to '{correction.Value}'",
                        OriginalText = match.Value,
                        CorrectedText = correctedTag,
                        Severity = "Warning"
                    });
                }

                // Match closing tags
                var closingPattern = $@"</{Regex.Escape(correction.Key)}>";
                var closingMatches = Regex.Matches(corrected, closingPattern, RegexOptions.IgnoreCase);

                foreach (Match match in closingMatches)
                {
                    var correctedTag = $"</{correction.Value}>";
                    corrected = corrected.Replace(match.Value, correctedTag);

                    result.Corrections.Add(new XmlValidationError
                    {
                        ErrorType = "TagSpelling",
                        Description = $"Corrected misspelled closing tag '{correction.Key}' to '{correction.Value}'",
                        OriginalText = match.Value,
                        CorrectedText = correctedTag,
                        Severity = "Warning"
                    });
                }
            }

            return await Task.FromResult(corrected);
        }

        private async Task<string> RepairXmlStructureAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Try to parse and automatically fix structure issues
                var doc = XDocument.Parse(xmlContent, LoadOptions.SetLineInfo);
                return doc.ToString();
            }
            catch (XmlException ex)
            {
                _logger.LogWarning("XML parsing failed, attempting structural repairs: {Message}", ex.Message);

                var corrected = xmlContent;

                // Fix unclosed tags using stack-based approach
                corrected = await FixUnClosedTagsAsync(corrected, result, cancellationToken);

                // Fix mismatched tags
                corrected = await FixMismatchedTagsAsync(corrected, result, cancellationToken);

                // Fix incorrect nesting
                corrected = await FixIncorrectNestingAsync(corrected, result, cancellationToken);

                return corrected;
            }
        }

        private async Task<string> FixUnClosedTagsAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            var lines = xmlContent.Split('\n');
            var stack = new Stack<(string tagName, int lineNumber)>();
            var correctedLines = new List<string>();
            var selfClosingTags = new HashSet<string> { "br", "hr", "img", "input", "meta", "link" };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var openTags = Regex.Matches(line, @"<([a-zA-Z][a-zA-Z0-9]*)[^>]*?(?<!/)>");
                var closeTags = Regex.Matches(line, @"</([a-zA-Z][a-zA-Z0-9]*)>");

                // Process opening tags
                foreach (Match tag in openTags)
                {
                    var tagName = tag.Groups[1].Value.ToLower();
                    if (!selfClosingTags.Contains(tagName))
                    {
                        stack.Push((tagName, i + 1));
                    }
                }

                // Process closing tags
                foreach (Match tag in closeTags)
                {
                    var tagName = tag.Groups[1].Value.ToLower();
                    if (stack.Count > 0 && stack.Peek().tagName == tagName)
                    {
                        stack.Pop();
                    }
                }

                correctedLines.Add(line);
            }

            // Add missing closing tags
            while (stack.Count > 0)
            {
                var (tagName, lineNumber) = stack.Pop();
                correctedLines.Add($"</{tagName}>");

                result.Corrections.Add(new XmlValidationError
                {
                    ErrorType = "MissingClosingTag",
                    Description = $"Added missing closing tag for '{tagName}' opened at line {lineNumber}",
                    OriginalText = $"<{tagName}>",
                    CorrectedText = $"<{tagName}>...</{tagName}>",
                    Severity = "Error"
                });
            }

            return await Task.FromResult(string.Join('\n', correctedLines));
        }

        private async Task<string> FixMismatchedTagsAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            var corrected = xmlContent;

            // Find patterns like <tag1>content</tag2>
            var mismatchPattern = @"<([a-zA-Z][a-zA-Z0-9]*)[^>]*>([^<]*)</([a-zA-Z][a-zA-Z0-9]*)>";
            var matches = Regex.Matches(corrected, mismatchPattern);

            foreach (Match match in matches)
            {
                var openTag = match.Groups[1].Value;
                var content = match.Groups[2].Value;
                var closeTag = match.Groups[3].Value;

                if (!openTag.Equals(closeTag, StringComparison.OrdinalIgnoreCase))
                {
                    var correctedMatch = match.Value.Replace($"</{closeTag}>", $"</{openTag}>");
                    corrected = corrected.Replace(match.Value, correctedMatch);

                    result.Corrections.Add(new XmlValidationError
                    {
                        ErrorType = "MismatchedTags",
                        Description = $"Fixed mismatched tags: opening '{openTag}' with closing '{closeTag}'",
                        OriginalText = match.Value,
                        CorrectedText = correctedMatch,
                        Severity = "Error"
                    });
                }
            }

            return await Task.FromResult(corrected);
        }

        private async Task<string> FixIncorrectNestingAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            // This would require a more sophisticated parser
            // For now, we'll implement basic nested tag validation
            var corrected = xmlContent;

            // Pattern to find incorrectly nested tags like <a><b></a></b>
            var nestingPattern = @"<([a-zA-Z][a-zA-Z0-9]*)[^>]*>([^<]*<([a-zA-Z][a-zA-Z0-9]*)[^>]*>[^<]*)</\1>([^<]*)</\3>";
            var matches = Regex.Matches(corrected, nestingPattern);

            foreach (Match match in matches)
            {
                var outerTag = match.Groups[1].Value;
                var innerTag = match.Groups[3].Value;

                // Reconstruct with proper nesting
                var properNesting = $"<{outerTag}>{match.Groups[2].Value}</{innerTag}></{outerTag}>{match.Groups[4].Value}";
                corrected = corrected.Replace(match.Value, properNesting);

                result.Corrections.Add(new XmlValidationError
                {
                    ErrorType = "IncorrectNesting",
                    Description = $"Fixed incorrect nesting of tags '{outerTag}' and '{innerTag}'",
                    OriginalText = match.Value,
                    CorrectedText = properNesting,
                    Severity = "Error"
                });
            }

            return await Task.FromResult(corrected);
        }

        private async Task<string> ValidateAndFinalizeAsync(string xmlContent,
            XmlCorrectionResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Final validation attempt
                var doc = XDocument.Parse(xmlContent);

                // Pretty print the XML
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "    ",
                    NewLineChars = "\n",
                    Encoding = Encoding.UTF8,
                    OmitXmlDeclaration = false
                };

                using var stringWriter = new StringWriter();
                using var xmlWriter = XmlWriter.Create(stringWriter, settings);
                doc.Save(xmlWriter);

                return await Task.FromResult(stringWriter.ToString());
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, "Final XML validation failed");
                result.Corrections.Add(new XmlValidationError
                {
                    ErrorType = "ValidationFailure",
                    Description = $"Final validation failed: {ex.Message}",
                    Severity = "Error",
                    LineNumber = ex.LineNumber,
                    LinePosition = ex.LinePosition
                });

                return await Task.FromResult(xmlContent);
            }
        }

        #region Helper Methods

        private string FixEncodingIssues(string xmlContent, XmlCorrectionResult result)
        {
            // Add XML declaration if missing
            if (!xmlContent.TrimStart().StartsWith("<?xml"))
            {
                xmlContent = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xmlContent;
                result.Corrections.Add(new XmlValidationError
                {
                    ErrorType = "MissingDeclaration",
                    Description = "Added missing XML declaration",
                    CorrectedText = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                    Severity = "Warning"
                });
            }

            return xmlContent;
        }

        private string FixSelfClosingTags(string xmlContent, XmlCorrectionResult result)
        {
            // Fix tags like <tag/> that should be <tag />
            var pattern = @"<([a-zA-Z][a-zA-Z0-9]*[^/\s])(/?)>";
            return Regex.Replace(xmlContent, pattern, match =>
            {
                if (match.Groups[2].Value == "/" && !match.Value.EndsWith(" />"))
                {
                    var corrected = match.Value.Replace("/>", " />");
                    result.Corrections.Add(new XmlValidationError
                    {
                        ErrorType = "SelfClosingTag",
                        Description = "Fixed self-closing tag spacing",
                        OriginalText = match.Value,
                        CorrectedText = corrected,
                        Severity = "Info"
                    });
                    return corrected;
                }
                return match.Value;
            });
        }

        private string FixAttributeQuotes(string xmlContent, XmlCorrectionResult result)
        {
            // Fix unquoted attribute values
            var pattern = @"(\w+)=([^'""\s>]+)(?=\s|>)";
            return Regex.Replace(xmlContent, pattern, match =>
            {
                var attr = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                var corrected = $"{attr}=\"{value}\"";

                result.Corrections.Add(new XmlValidationError
                {
                    ErrorType = "UnquotedAttribute",
                    Description = $"Added quotes to attribute '{attr}'",
                    OriginalText = match.Value,
                    CorrectedText = corrected,
                    Severity = "Warning"
                });

                return corrected;
            });
        }

        private string EscapeInvalidCharacters(string xmlContent, XmlCorrectionResult result)
        {
            var replacements = new Dictionary<string, string>
        {
            { "&", "&amp;" },
            { "<", "&lt;" },
            { ">", "&gt;" },
            { "\"", "&quot;" },
            { "'", "&apos;" }
        };

            // Only escape characters that are not already properly escaped
            foreach (var replacement in replacements)
            {
                var pattern = $@"(?<!&\w*){Regex.Escape(replacement.Key)}(?!\w*;)";
                if (Regex.IsMatch(xmlContent, pattern))
                {
                    xmlContent = Regex.Replace(xmlContent, pattern, replacement.Value);
                    result.Corrections.Add(new XmlValidationError
                    {
                        ErrorType = "InvalidCharacter",
                        Description = $"Escaped invalid character '{replacement.Key}'",
                        CorrectedText = replacement.Value,
                        Severity = "Warning"
                    });
                }
            }

            return xmlContent;
        }

        #endregion
    }

}
