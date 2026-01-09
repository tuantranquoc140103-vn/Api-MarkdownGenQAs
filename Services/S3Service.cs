using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using MarkdownGenQAs.Interfaces.Repository;
using MarkdownGenQAs.Options;
using Polly;
using Polly.Retry;
using Serilog;
using System.Net;
using System.Text;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    // private readonly AwsOptions _awsOptions;
    private readonly AsyncRetryPolicy _retryPolicy;

    public S3Service(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;

        _retryPolicy = Policy
            .Handle<AmazonS3Exception>(ex => ex.StatusCode == HttpStatusCode.InternalServerError || ex.StatusCode == HttpStatusCode.ServiceUnavailable)
            .Or<TimeoutException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning("Retry {RetryCount} for S3 operation due to {ExceptionMessage}", retryCount, exception.Message);
                });
    }

    public async Task InitializeBucketsAsync()
    {
        var buckets = new[]
        {
            S3Buckets.MarkdownOcr,
            S3Buckets.DocumentSummary,
            S3Buckets.ChunkQa
        };

        foreach (var bucket in buckets)
        {
            await EnsureBucketExistsAsync(bucket);
            await SetPrivateBucketPolicyAsync(bucket);
        }
    }

    private async Task EnsureBucketExistsAsync(string bucketName)
    {
        try
        {
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName))
            {
                Log.Information("Creating bucket {BucketName}...", bucketName);
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = bucketName,
                    CannedACL = S3CannedACL.Private
                };
                await _s3Client.PutBucketAsync(putBucketRequest);
                Log.Information("Bucket {BucketName} created successfully.", bucketName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking or creating bucket {BucketName}", bucketName);
            throw;
        }
    }

    private async Task SetPrivateBucketPolicyAsync(string bucketName)
    {
        try
        {
            Log.Information("Setting private policy for bucket {BucketName}...", bucketName);
            // S3 buckets are private by default, but we can explicitly set a policy if needed.
            // For MinIO/S3, setting CannedACL.Private during creation is usually enough.
            // If we want a strict JSON policy:
            var emptyPolicy = @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": []
            }";

            await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = bucketName,
                Policy = emptyPolicy
            });

            Log.Information("Bucket {BucketName} set to private via empty policy", bucketName);

        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set strict public access block for {BucketName}. This might be a MinIO version limitation.", bucketName);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string bucketName, string contentType = "application/octet-stream")
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));

        fileName = S3Helper.NormalizeObjectKey(fileName);

        // có nên xử lý fileName để phù hợp với name trong objectKey
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileName,
                InputStream = fileStream,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            };

            await _s3Client.PutObjectAsync(request);
            Log.Information("Uploaded {FileName} to {BucketName}", fileName, bucketName);
            return fileName;
        });
    }

    public async Task<Stream?> DownloadFileAsync(string objectKey, string bucketName)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                objectKey = S3Helper.NormalizeObjectKey(objectKey);
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                var response = await _s3Client.GetObjectAsync(request);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Warning("File {FileName} not found in {BucketName}", objectKey, bucketName);
                return null;
            }
        });
    }

    public async Task<bool> FileExistsAsync(string objectKey, string bucketName)
    {
        try
        {
            objectKey = S3Helper.NormalizeObjectKey(objectKey);
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking file existence {FileName} in {BucketName}", objectKey, bucketName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string objectKey, string bucketName)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                objectKey = S3Helper.NormalizeObjectKey(objectKey);
                var request = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                };

                await _s3Client.DeleteObjectAsync(request);
                Log.Information("Deleted {FileName} from {BucketName}", objectKey, bucketName);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Warning("File {FileName} not found in {BucketName} for deletion", objectKey, bucketName);
                return false;
            }
        });
    }

    public async Task<string?> GetFileContentAsync(string objectKey, string bucketName)
    {
        using var stream = await DownloadFileAsync(objectKey, bucketName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
