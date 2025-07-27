//using Azure.Storage.Sas;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Concurrent;
//using System.Threading.Tasks;

//namespace FileProcessingMicroservice.FunctionApp.Services
//{
    

//    public class SasTokenService
//    {
//        private readonly BlobService _blobService;
//        private readonly IConfiguration _configuration;
//        private readonly ILogger<SasTokenService> _logger;
//        private readonly ConcurrentDictionary<string, CachedSasToken> _tokenCache;

//        public SasTokenService(
//            BlobService blobService,
//            IConfiguration configuration,
//            ILogger<SasTokenService> logger)
//        {
//            _blobService = blobService;
//            _configuration = configuration;
//            _logger = logger;
//            _tokenCache = new ConcurrentDictionary<string, CachedSasToken>();
//        }

//        public async Task<SasTokenResponse> GenerateReadTokenAsync(
//            string containerName,
//            string fileName//,
//            //string year = null
//            )
//        {
//            //year ??= DateTime.UtcNow.Year.ToString();
//            //var cacheKey = $"{containerName}:{year}:{fileName}:read";
//            var cacheKey = $"{containerName}:{fileName}:read";

//            // Check cache first
//            if (_tokenCache.TryGetValue(cacheKey, out var cachedToken) &&
//                cachedToken.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(10))
//            {
//                _logger.LogInformation("Returning cached SAS token for {FileName}", fileName);
//                return cachedToken.Response;
//            }

//            try
//            {
//                // Generate User Delegation SAS (preferred)
//                var sasUrl = await _blobService.GenerateUserDelegationSasUrlAsync(
//                    //containerName, fileName, year,
//                    containerName, fileName,
//                    BlobSasPermissions.Read, TimeSpan.FromHours(2));

//                var response = new SasTokenResponse
//                {
//                    SasUrl = sasUrl,
//                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
//                    TokenType = "UserDelegation",
//                    FileName = fileName,
//                    ContainerName = containerName,
//                    //Year = year
//                };

//                // Cache the token
//                _tokenCache[cacheKey] = new CachedSasToken
//                {
//                    Response = response,
//                    ExpiresAt = response.ExpiresAt
//                };

//                _logger.LogInformation("Generated User Delegation SAS token for {FileName}", fileName);
//                return response;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, "Failed to generate User Delegation SAS, falling back to Service SAS");

//                // Fallback to Service SAS
//                var sasUrl = _blobService.GenerateServiceSasUrl(
//                    containerName, fileName,// year,
//                    BlobSasPermissions.Read, TimeSpan.FromHours(1));

//                var response = new SasTokenResponse
//                {
//                    SasUrl = sasUrl,
//                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
//                    TokenType = "Service",
//                    FileName = fileName,
//                    ContainerName = containerName,
//                    //Year = year
//                };

//                _logger.LogInformation("Generated Service SAS token for {FileName}", fileName);
//                return response;
//            }
//        }

//        public async Task<SasTokenResponse> GenerateUploadTokenAsync(string containerName, string fileName)
//        {
//            var year = DateTime.UtcNow.Year.ToString();

//            var sasUrl = await _blobService.GenerateUserDelegationSasUrlAsync(
//                containerName, fileName, //year,
//                BlobSasPermissions.Create | BlobSasPermissions.Write,
//                TimeSpan.FromMinutes(30)); // Shorter expiry for uploads

//            return new SasTokenResponse
//            {
//                SasUrl = sasUrl,
//                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
//                TokenType = "UserDelegation",
//                FileName = fileName,
//                ContainerName = containerName,
//               // Year = year
//            };
//        }

//        // Clean expired tokens from cache
//        public void CleanExpiredTokens()
//        {
//            var expiredKeys = new List<string>();
//            var now = DateTimeOffset.UtcNow;

//            foreach (var kvp in _tokenCache)
//            {
//                if (kvp.Value.ExpiresAt <= now)
//                    expiredKeys.Add(kvp.Key);
//            }

//            foreach (var key in expiredKeys)
//            {
//                _tokenCache.TryRemove(key, out _);
//            }

//            if (expiredKeys.Count > 0)
//                _logger.LogInformation("Cleaned {Count} expired SAS tokens from cache", expiredKeys.Count);
//        }
//    }

//    public class SasTokenResponse
//    {
//        public string SasUrl { get; set; }
//        public DateTimeOffset ExpiresAt { get; set; }
//        public string TokenType { get; set; }
//        public string FileName { get; set; }
//        public string ContainerName { get; set; }
//       // public string Year { get; set; }
//    }

//    public class CachedSasToken
//    {
//        public SasTokenResponse Response { get; set; }
//        public DateTimeOffset ExpiresAt { get; set; }
//    }

//}
