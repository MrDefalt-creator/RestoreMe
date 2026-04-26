using System.Net.Http.Headers;
using Backup.Agent.Worker.DTOs;
using Backup.Agent.Worker.Interfaces;

namespace Backup.Agent.Worker.Services;

public class MinioStorageClient : IMinioStorageClient
{
    
    private readonly HttpClient _httpClient;
    private readonly ILogger<MinioStorageClient> _logger;
    private readonly IChecksumService _checksumService;

    public MinioStorageClient(HttpClient httpClient, ILogger<MinioStorageClient> logger, IChecksumService checksumService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _checksumService = checksumService;
    }

    public async Task<UploadObjectResult> UploadFileAsync(
        string uploadUrl,
        string filePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var checksum = await _checksumService.ComputeSha256Async(filePath, cancellationToken);

        await using var fileStream = File.OpenRead(filePath);
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        using var streamContent = new StreamContent(fileStream);

        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        streamContent.Headers.ContentLength = fileInfo.Length;

        request.Content = streamContent;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Upload failed. StatusCode: {response.StatusCode}. Body: {body}");
        }
        
        
        _logger.LogInformation(
            "Upload completed successfully. File: {FilePath}, Size: {Size}, Checksum: {Checksum}",
            filePath,
            fileInfo.Length,
            checksum);
        
        return new UploadObjectResult(fileInfo.Length, checksum);
    }
    
}