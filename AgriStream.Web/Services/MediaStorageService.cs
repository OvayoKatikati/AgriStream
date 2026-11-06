using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

/// <summary>
/// Stores drone images in Azure Blob Storage. Files are streamed straight to Blob Storage and never
/// written to the web server's disk, so every App Service instance behaves identically (stateless).
/// </summary>
public class MediaStorageService
{
    public const string ContainerName = "drone-images";

    private readonly BlobContainerClient _container;

    public MediaStorageService(BlobServiceClient serviceClient)
    {
        _container = serviceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task EnsureContainerAsync() => await _container.CreateIfNotExistsAsync();

    public async Task UploadAsync(Stream content, string blobName, string contentType, IDictionary<string, string> metadata)
    {
        var blob = _container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata
        });
    }

    public async Task<List<MediaItemViewModel>> ListAsync(string? prefix)
    {
        var items = new List<MediaItemViewModel>();

        await foreach (var item in _container.GetBlobsAsync(traits: BlobTraits.Metadata, prefix: prefix))
        {
            item.Metadata.TryGetValue("uploadedby", out var uploadedBy);
            items.Add(new MediaItemViewModel
            {
                Name = item.Name,
                SizeBytes = item.Properties.ContentLength,
                UploadedUtc = item.Properties.CreatedOn,
                UploadedBy = uploadedBy ?? string.Empty
            });
        }

        return items.OrderByDescending(i => i.UploadedUtc).ToList();
    }

    /// <summary>Returns the blob stream and content type, or null when the blob does not exist.</summary>
    public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string blobName)
    {
        var blob = _container.GetBlobClient(blobName);
        try
        {
            var props = await blob.GetPropertiesAsync();
            var stream = await blob.OpenReadAsync();
            return (stream, props.Value.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
