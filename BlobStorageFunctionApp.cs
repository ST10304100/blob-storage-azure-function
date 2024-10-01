using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BlobStorageFunctionApp
{
    public class BlobStorageFunction
    {
        private readonly BlobServiceClient _blobServiceClient;

        public BlobStorageFunction()
        {
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");


            _blobServiceClient = new BlobServiceClient(connectionString);
        }


        [Function("UploadToBlob")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("UploadToBlob");
            logger.LogInformation("Uploading to Blob Storage...");


            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient("products");


                await containerClient.CreateIfNotExistsAsync();


                if (!req.Headers.TryGetValues("file-name", out var fileNameValues))
                {
                    throw new Exception("File name is missing in the request headers.");
                }

                string originalFileName = fileNameValues.FirstOrDefault();
                if (string.IsNullOrEmpty(originalFileName))
                {
                    throw new Exception("Invalid file name.");
                }

                // Get a BlobClient for the specific blob using the original filename
                var blobClient = containerClient.GetBlobClient(originalFileName);

                // Upload the HTTP request body stream to the blob storage, overwriting any existing blob with the same name
                using (var stream = req.Body)
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // Create a success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Blob '{originalFileName}' uploaded successfully.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error uploading to Blob Storage: {ex.Message}");

                // Create an error response
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to upload blob.");
                return errorResponse;
            }
        }

        // Function that handles HTTP DELETE requests to remove a blob from Blob Storage
        [Function("DeleteBlob")]
        public async Task<HttpResponseData> DeleteBlobAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete")] HttpRequestData req,
            FunctionContext executionContext)
        {
            // Get a Logger instance to log information about the function's execution
            var logger = executionContext.GetLogger("DeleteBlob");
            logger.LogInformation("Deleting from Blob Storage...");

            try
            {
                // Extract the Blob URI from the query string
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string blobUri = query["blobUri"];

                if (string.IsNullOrEmpty(blobUri))
                {
                    throw new Exception("Blob URI is missing from the query string.");
                }

                // Parse the URI to extract the blob name
                Uri uri = new Uri(blobUri);
                string blobName = uri.Segments[^1]; // Get the blob name from the URI

                // Get a BlobContainerClient for the specific container (replace "products" with your actual container name)
                var containerClient = _blobServiceClient.GetBlobContainerClient("products");
                var blobClient = containerClient.GetBlobClient(blobName);

                // Delete the blob if it exists (including snapshots)
                await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                // Create a success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Blob '{blobName}' deleted successfully.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error deleting blob from Blob Storage: {ex.Message}");

                // Create an error response
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to delete blob.");
                return errorResponse;
            }
        }
    }
}