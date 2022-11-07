using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using BaGet.Core;
using Microsoft.Extensions.Options;

using Neon.Common;
using Neon.IO;

namespace BaGet.Aws
{
    public class S3StorageService : IStorageService
    {
        private const string Separator = "/";
        private readonly string _bucket;
        private readonly string _prefix;
        private readonly AmazonS3Client _client;

        public S3StorageService(IOptionsSnapshot<S3StorageOptions> options, AmazonS3Client client)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _bucket = options.Value.Bucket;
            _prefix = options.Value.Prefix;
            _client = client ?? throw new ArgumentNullException(nameof(client));

            if (!string.IsNullOrEmpty(_prefix) && !_prefix.EndsWith(Separator))
                _prefix += Separator;
        }

        private string PrepareKey(string path)
        {
            return _prefix + path.Replace("\\", Separator);
        }

        public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            var stream = new MemoryStream();

            try
            {
                using (var request = await _client.GetObjectAsync(_bucket, PrepareKey(path), cancellationToken))
                {
                    await request.ResponseStream.CopyToAsync(stream);
                }

                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception)
            {
                stream.Dispose();

                // TODO
                throw;
            }

            return stream;
        }

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
        {
            var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = PrepareKey(path)
            });

            return Task.FromResult(new Uri(url));
        }

        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using (var tempFile = new TempFile())
            {
                using (var tempStream = System.IO.File.Create(tempFile.Name))
                {
                    await content.CopyToAsync(tempStream, 4096, cancellationToken);

                    tempStream.Seek(0, SeekOrigin.Begin);

                    var metadata = new MetadataCollection();
                    var key = PrepareKey(path);

                    // Create list to store upload part responses.
                    var uploadResponses = new List<UploadPartResponse>();

                    // Setup information required to initiate the multipart upload.
                    var initiateRequest = new InitiateMultipartUploadRequest
                    {
                        BucketName = _bucket,
                        Key = key
                    };

                    // Initiate the upload.
                    var initResponse =
                        await _client.InitiateMultipartUploadAsync(initiateRequest);

                    // Upload parts.
                    var contentLength = tempStream.Length;
                    var partSize = 5 * (long)Math.Pow(2, 20); // 5 MB

                    try
                    {
                        Console.WriteLine("Uploading parts");

                        long filePosition = 0;
                        for (var i = 1; filePosition < contentLength; i++)
                        {
                            var uploadRequest = new UploadPartRequest
                            {
                                BucketName = _bucket,
                                Key = key,
                                UploadId = initResponse.UploadId,
                                PartNumber = i,
                                PartSize = partSize,
                                InputStream = tempStream
                            };

                            // Track upload progress.
                            uploadRequest.StreamTransferProgress +=
                                new EventHandler<StreamTransferProgressArgs>(UploadPartProgressEventCallback);

                            // Upload a part and add the response to our list.
                            uploadResponses.Add(await _client.UploadPartAsync(uploadRequest));

                            filePosition += partSize;
                        }

                        // Setup to complete the upload.
                        var completeRequest = new CompleteMultipartUploadRequest
                        {
                            BucketName = _bucket,
                            Key = key,
                            UploadId = initResponse.UploadId
                        };
                        completeRequest.AddPartETags(uploadResponses);

                        // Complete the upload.
                        var completeUploadResponse =
                            await _client.CompleteMultipartUploadAsync(completeRequest);

                        return StoragePutResult.Success;
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("An AmazonS3Exception was thrown: { 0}", exception.Message);

                        // Abort the upload.
                        var abortMPURequest = new AbortMultipartUploadRequest
                        {
                            BucketName = _bucket,
                            Key = key,
                            UploadId = initResponse.UploadId
                        };
                        await _client.AbortMultipartUploadAsync(abortMPURequest);

                        throw;
                    }

                    //var request = new PutObjectRequest
                    //{
                    //    BucketName = _bucket,
                    //    Key = PrepareKey(path),
                    //    InputStream = tempStream,
                    //    ContentType = contentType,
                    //    AutoResetStreamPosition = false,
                    //    AutoCloseStream = false
                    //};

                    //var result = await _client.PutObjectAsync(request);

                    //if (result.HttpStatusCode != HttpStatusCode.OK)
                    //{
                    //    throw new Exception(result.HttpStatusCode.ToString());
                    //}

                }
            }
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            await _client.DeleteObjectAsync(_bucket, PrepareKey(path), cancellationToken);
        }

        public static void UploadPartProgressEventCallback(object sender, StreamTransferProgressArgs e)
        {
            // Process event. 
            Console.WriteLine("{0}/{1}", e.TransferredBytes, e.TotalBytes);
        }
    }
}
