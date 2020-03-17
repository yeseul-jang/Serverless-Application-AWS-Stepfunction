
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using System;
using System.Threading.Tasks;

namespace ServerlessAppForLab4
{
    public class DynamoDB
    {
        AmazonDynamoDBClient _client;
        //DynamoDBContext _context;
        BasicAWSCredentials _credentials;
        Table _labelTable;

        public DynamoDB()
        {
            _credentials = new BasicAWSCredentials("AKIASQEXHDQA2Q6MANO6", "IuKWdjsDKcGeq8dvMWcqHIBgb9iQXsji4KhT1vpS");
            _client = new AmazonDynamoDBClient(_credentials, Amazon.RegionEndpoint.USEast1);
            
            //_context = new DynamoDBContext(_client);
            _labelTable = Table.LoadTable(_client, "BucketLabel", DynamoDBEntryConversion.V2);
        }

        public void CreateTable()
        {
            CreateTableRequest request = new CreateTableRequest
            {
                TableName = "BucketLabel",
                AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName="key",
                    AttributeType="S"
                },
                    new AttributeDefinition
                {
                    AttributeName="labelName",
                    AttributeType="S"
                }
            },
                KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName="key",
                    KeyType="HASH"
                },
                new KeySchemaElement
                {
                    AttributeName="labelName",
                    KeyType="RANGE"
                }
            },
                BillingMode = BillingMode.PROVISIONED,
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 2,
                    WriteCapacityUnits = 1
                }

            };

            var response = _client.CreateTableAsync(request);
        }

        public async Task InsertLabelAsync(string key, string labelName, float confidence)
        {
            Document document = new Document
            {
                ["key"] = key,
                ["labelName"] = labelName,
                ["confidence"] = confidence
            };
            await _labelTable.PutItemAsync(document);
        }
    }
}
