//using FileProcessingMicroservice.FunctionApp.Services;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
//using Microsoft.Extensions.Logging;
//using Microsoft.OpenApi.Models;
//using System.Net;


//namespace FileProcessingMicroservice.FunctionApp.Functions;

//public class SasTokenFunction
//{
//    private readonly ILogger<SasTokenFunction> _logger;
//    private readonly SasTokenService _sasTokenService;


//    public SasTokenFunction(ILogger<SasTokenFunction> logger)
//    {
//        _logger = logger;
//    }
      

//    public SasTokenFunction(SasTokenService sasTokenService, ILogger<SasTokenFunction> logger)
//    {
//        _sasTokenService = sasTokenService;
//        _logger = logger;
//    }

//    [Function("GenerateDownloadSasToken")]
//    [OpenApiOperation("GenerateDownloadSasToken", Summary = "Generate SAS token for file download")]
//    [OpenApiParameter(name: "containerName", In = ParameterLocation.Path, Required = true)]
//    [OpenApiParameter(name: "fileName", In = ParameterLocation.Path, Required = true)]
//    //[OpenApiParameter(name: "year", In = ParameterLocation.Query, Required = false)]
//    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SasTokenResponse))]
//    public async Task<HttpResponseData> GenerateDownloadSasToken(
//        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sas/{containerName}/{fileName}")] HttpRequestData req,
//        string containerName,
//        string fileName)
//    {
//        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
//        //var year = queryParams["year"] ?? DateTime.UtcNow.Year.ToString();

//        try
//        {
//            //var sasToken = await _sasTokenService.GenerateReadTokenAsync(containerName, fileName, year);
//            var sasToken = await _sasTokenService.GenerateReadTokenAsync(containerName, fileName);

//            var response = req.CreateResponse(HttpStatusCode.OK);
//            await response.WriteAsJsonAsync(sasToken);
//            return response;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to generate SAS token for {FileName}", fileName);
//            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
//            await errorResponse.WriteStringAsync("Failed to generate SAS token");
//            return errorResponse;
//        }
//    }

//    [Function("GenerateUploadSasToken")]
//    [OpenApiOperation("GenerateUploadSasToken", Summary = "Generate SAS token for file upload")]
//    [OpenApiRequestBody("application/json", typeof(UploadSasRequest))]
//    public async Task<HttpResponseData> GenerateUploadSasToken(
//        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sas/upload")] HttpRequestData req)
//    {
//        var uploadRequest = await req.ReadFromJsonAsync<UploadSasRequest>();

//        if (string.IsNullOrEmpty(uploadRequest?.FileName))
//        {
//            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
//            await badRequest.WriteStringAsync("FileName is required");
//            return badRequest;
//        }

//        try
//        {
//            var sasToken = await _sasTokenService.GenerateUploadTokenAsync(
//                uploadRequest.ContainerName ?? "upload",
//                uploadRequest.FileName);

//            var response = req.CreateResponse(HttpStatusCode.OK);
//            await response.WriteAsJsonAsync(sasToken);
//            return response;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Failed to generate upload SAS token");
//            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
//            await errorResponse.WriteStringAsync("Failed to generate upload SAS token");
//            return errorResponse;
//        }
//    }
//}

//public class UploadSasRequest
//{
//    public string FileName { get; set; }
//    public string ContainerName { get; set; }
//}
