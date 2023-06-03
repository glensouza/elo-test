using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class NewEloFunction
    {
        private readonly ILogger logger;
        private readonly TableClient pictureTableClient;
        private readonly BlobContainerClient blobContainerClient;

        public NewEloFunction(ILoggerFactory loggerFactory, PictureTable pictureTable, BlobContainerClient blobClient)
        {
            this.logger = loggerFactory.CreateLogger<NewEloFunction>();
            this.pictureTableClient = pictureTable.Client;
            this.blobContainerClient = blobClient;
        }

        [Function("NewElo")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            // get form-body
            MultipartFormDataParser parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
            if (parsedFormBody.Files.Count == 0)
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                return badRequestResponse;
            }

            FilePart audioFile = parsedFormBody.Files[0];
            string filename = audioFile.FileName;
            Stream stream = audioFile.Data;

            PictureEntity pictureEntity = new()
            {
                Name = parsedFormBody.HasParameter("name") ? parsedFormBody.GetParameterValues("name").First() : string.Empty
            };

            PictureEntity? existingEloEntity = await this.pictureTableClient.GetEntityAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            while (existingEloEntity != null)
            {
                pictureEntity.RowKey = Guid.NewGuid().ToString();
                existingEloEntity = await this.pictureTableClient.GetEntityAsync<PictureEntity>(pictureEntity.PartitionKey, pictureEntity.RowKey);
            }
            
            BlobClient? cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{pictureEntity.RowKey}.{Path.GetExtension(filename)}");
            bool fileExists = await cloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                await cloudBlockBlob.DeleteAsync();
                fileExists = await cloudBlockBlob.ExistsAsync();
            }

            await cloudBlockBlob.UploadAsync(stream);

            string uri = cloudBlockBlob.Uri.AbsoluteUri;
            if (cloudBlockBlob.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new()
                {
                    BlobContainerName = cloudBlockBlob.GetParentBlobContainerClient().Name,
                    BlobName = cloudBlockBlob.Name,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                Uri sasUri = cloudBlockBlob.GenerateSasUri(sasBuilder);
                this.logger.LogInformation("SAS URI for blob is: {0}", sasUri);

                uri = sasUri.AbsoluteUri;
            }
            else
            {
                this.logger.LogError("BlobClient must be authorized with Shared Key credentials to create a service SAS.");
                //return new ExceptionResult(new Exception("BlobClient must be authorized with Shared Key credentials to create a service SAS."), false);
            }

            pictureEntity.PictureUri = uri;
            await this.pictureTableClient.AddEntityAsync(pictureEntity);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
    }
}
/*
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TranscribeTranslateDemo.API.Entities;

namespace TranscribeTranslateDemo.API
{
    public class Transcribe
    {
        private readonly ILogger logger;
        private readonly TableClient tableClient;
        private readonly BlobContainerClient blobContainerClient;
        private readonly QueueClient queueClient;

        public Transcribe(ILoggerFactory loggerFactory, TableClient tableClient, BlobContainerClient blobClient, QueueClient queueClient)
        {
            this.logger = loggerFactory.CreateLogger<Transcribe>();
            this.tableClient = tableClient;
            this.blobContainerClient = blobClient;
            this.queueClient = queueClient;
        }

        [Function("Transcribe")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            this.logger.LogInformation("C# HTTP trigger function processed a request.");

            // get form-body
            MultipartFormDataParser parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
            if (parsedFormBody.Files.Count == 0)
            {
                HttpResponseData badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                return badRequestResponse;
            }

            FilePart audioFile = parsedFormBody.Files.First();
            string filename = audioFile.FileName;
            Stream stream = audioFile.Data;

            string rowKey = Guid.NewGuid().ToString();

            string? localRoot = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
            string azureRoot = $"{Environment.GetEnvironmentVariable("HOME")}/site/wwwroot";
            string rootPath = localRoot ?? azureRoot;

            FFmpeg.ExecutablesPath = rootPath;
            string outputPath = Path.ChangeExtension(Path.GetTempFileName(), FileExtensions.Mp3);
            string directoryName = Path.GetDirectoryName(outputPath);
            string filePath = $"{directoryName}\\{rowKey}.mp3";
            if (!File.Exists(filePath))
            {
                await using FileStream file = new(filePath, FileMode.Create, FileAccess.Write);
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                file.Write(bytes, 0, bytes.Length);
                stream.Close();
            }

            BlobClient? cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{directoryName}\\{rowKey}.mp3");
            bool fileExists = await cloudBlockBlob.ExistsAsync();
            while (fileExists)
            {
                rowKey = Guid.NewGuid().ToString();
                cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{directoryName}\\{rowKey}.mp3");
                fileExists = await cloudBlockBlob.ExistsAsync();
            }

            await cloudBlockBlob.UploadAsync(filePath);
            string uri = cloudBlockBlob.Uri.AbsoluteUri;
            // TODO: SignalR this to client

            try
            {
                IMediaInfo inputFile = await MediaInfo.Get(filePath).ConfigureAwait(false);

                IAudioStream audioStream = inputFile.AudioStreams.First();

                //Debugger.Break();
                int sampleRate = audioStream.SampleRate;
                int channels = audioStream.Channels;
                //CodecType codec = audioStream.CodecType;
                //Debugger.Break();

                if (sampleRate < 41100)
                {
                    audioStream.SetSampleRate(41100);
                }

                if (channels != 1)
                {
                    audioStream.SetChannels(1);
                }

                await Conversion.New().AddStream(audioStream).SetOutput(outputPath).Start().ConfigureAwait(false);

                await using Mp3FileReader mp3 = new(outputPath);
                await using WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3);
                WaveFileWriter.CreateWaveFile(outputPath + ".flac", pcm);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Debugger.Break();
            }

            cloudBlockBlob = this.blobContainerClient.GetBlobClient($"{rowKey}.flac");
            fileExists = await cloudBlockBlob.ExistsAsync();
            if (fileExists)
            {
                await cloudBlockBlob.DeleteAsync();
            }

            await cloudBlockBlob.UploadAsync(outputPath + ".flac");
            File.Delete(filePath);
            File.Delete(outputPath + ".flac");
            File.Delete(outputPath);

            DemoEntity demo = new()
            {
                PartitionKey = "Demo",
                RowKey = rowKey,
                //UserId = userId,
                AudioFileUrl = uri,
                LanguageFrom = "en-US", // TODO: languageFrom,
                LanguageTo = "es-MX", // TODO: languageTo,
                Sentiment = string.Empty,
                Transcription = string.Empty,
                Translation = string.Empty
            };
            await this.tableClient.AddEntityAsync(demo);

















            FileInfo fileInfo = new(Assembly.GetExecutingAssembly().Location);
            string path = fileInfo.Directory.Parent.FullName;
            FileStream objfilestream = new(Path.Combine(path, filename + ".mp3"), FileMode.Create, FileAccess.ReadWrite);

            using (MemoryStream memoryStream = new())
            {
                await stream.CopyToAsync(memoryStream);
                objfilestream.Write(memoryStream.ToArray(), 0, (int)memoryStream.Length);
                objfilestream.Close();
            }


            //objfilestream.Write(stream, 0, stream.Length);
            //objfilestream.Close();

            
            
            
            ////DemoEntity demo = new()
            ////{
            ////    PartitionKey = "Hello",
            ////    RowKey = "World",
            ////    Text = "Hello World!"
            ////};
            ////this.tableClient.AddEntity(demo);





            ////string filePath = "sample-file";

            ////// Get a reference to a blob named "sample-file" in a container named "sample-container"
            ////BlobClient blobClient = this.blobContainerClient.GetBlobClient(blobName);

            ////// Upload local file
            ////blobClient.Upload(filePath);





            ////// Get a temporary path on disk where we can download the file
            ////string downloadPath = "hello.jpg";

            ////// Download the public blob at https://aka.ms/bloburl
            ////new BlobClient(new Uri("https://aka.ms/bloburl")).DownloadTo(downloadPath);
            ////// Download the public blob at https://aka.ms/bloburl
            ////await new BlobClient(new Uri("https://aka.ms/bloburl")).DownloadToAsync(downloadPath);





            // Print out all the blob names
            foreach (BlobItem blob in this.blobContainerClient.GetBlobs())
            {
                Console.WriteLine(blob.Name);
            }






            this.queueClient.SendMessage("Hello World!");


            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            //response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            //response.WriteString("Welcome to Azure Functions!");

            return response;
        }

        //[Function("FunctionQ")]
        //public void Run([QueueTrigger("Demo", Connection = "AzureWebJobsStorage")] string myQueueItem)
        //{
        //    this.logger.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
        //}
    }
}
 */