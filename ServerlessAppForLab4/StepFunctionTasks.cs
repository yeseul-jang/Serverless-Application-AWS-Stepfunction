using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;


using Amazon.Rekognition;
using Amazon.Rekognition.Model;

using Amazon.S3;
using Amazon.S3.Model;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace ServerlessAppForLab4
{
    public class StepFunctionTasks
    {
        DynamoDB dynamoDB = new DynamoDB();

        IAmazonS3 S3Client;
        IAmazonRekognition RekognitionClient;

        HashSet<string> SupportedImageTypes = new HashSet<string> { ".png", ".jpg", ".jpeg" };
        public const float DEFAULT_MIN_CONFIDENCE = 90f;

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public StepFunctionTasks()
        {
            //dynamoDB.CreateTable();

            S3Client = new AmazonS3Client();
            RekognitionClient = new AmazonRekognitionClient();
        }


        public async Task FaceDetection(CloudWatchEvent<RequestParameters> parameter, ILambdaContext context)
        {

            var bucketName = parameter.Detail.requestParameters["bucketName"];
            var key = parameter.Detail.requestParameters["key"];

            if (!SupportedImageTypes.Contains(Path.GetExtension(key)))
            {
                Console.WriteLine($"Object {bucketName}:{key} is not a supported image type");

            }

            Console.WriteLine($"Looking for labels in image {bucketName}:{key}");
            var detectResponses = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = DEFAULT_MIN_CONFIDENCE,
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = bucketName,
                        Name = key
                    }
                }
            });

            var tags = new List<Tag>();
            foreach (var label in detectResponses.Labels)
            {
                if (tags.Count < 10)
                {
                    Console.WriteLine($"\tFound Label {label.Name} with confidence {label.Confidence}");
                    tags.Add(new Tag { Key = label.Name, Value = label.Confidence.ToString() });


                    await dynamoDB.InsertLabelAsync(key, label.Name, label.Confidence);
                }
                else
                {
                    Console.WriteLine($"\tSkipped label {label.Name} with confidence {label.Confidence} because the maximum number of tags has been reached");
                }
            }

            await this.S3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = bucketName,
                Key = key,
                Tagging = new Tagging
                {
                    TagSet = tags
                }
            });
        }
        // Working on Thumnbnail
        public async Task<string> FunctionHandler(CloudWatchEvent<RequestParameters> parameter, ILambdaContext context)
        {
            var bucketName = parameter.Detail.requestParameters["bucketName"];
            var key = parameter.Detail.requestParameters["key"];

            try
            {
                var rs = await this.S3Client.GetObjectMetadataAsync(
                    bucketName,
                    key);

                if (rs.Headers.ContentType.StartsWith("image/"))
                {
                    using (GetObjectResponse response = await S3Client.GetObjectAsync(
                        bucketName,
                        key))


                    {
                        using (Stream responseStream = response.ResponseStream)
                        {
                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                using (var memstream = new MemoryStream())
                                {
                                    var buffer = new byte[512];
                                    var bytesRead = default(int);
                                    while ((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                        memstream.Write(buffer, 0, bytesRead);
                                    // Perform image manipulation 
                                    var transformedImage = Thumbnail.GetConvertedImage(memstream.ToArray());
                                    PutObjectRequest putRequest = new PutObjectRequest()
                                    {
                                        BucketName = bucketName,
                                        Key = key,
                                        ContentType = rs.Headers.ContentType,
                                        ContentBody = transformedImage
                                    };
                                    await S3Client.PutObjectAsync(putRequest);
                                }
                            }
                        }
                    }
                }
                return rs.Headers.ContentType;
            }
            catch (Exception e)
            {
                throw;
            }
        }

    }
}
