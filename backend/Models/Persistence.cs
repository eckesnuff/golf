using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Models
{
    public class Persistence
    {
        private readonly string _databaseId = "GolferServiceDB";
        private readonly string _collectionName = "Golfers";
        private readonly string _endpointUri;
        private readonly string _primaryKey;
        private CosmosClient _client;
        private Container _container;
        private Container _containerV2;

        public Persistence(Uri endpointUri, string primaryKey)
        {
            _endpointUri = endpointUri.ToString();
            _primaryKey = primaryKey;
        }

        public async Task EnsureSetupAsync()
        {
            _client = new CosmosClient(_endpointUri, _primaryKey);
            var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(_databaseId);
            var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(_collectionName, "/id");
            _container = containerResponse.Container;
            var containerV2Response = await dbResponse.Database.CreateContainerIfNotExistsAsync(_collectionName + "_v2", "/id");
            _containerV2 = containerV2Response.Container;
        }

        public async Task SaveGolferAsync(GolferDoc sample)
        {
            await _container.UpsertItemAsync(sample, new PartitionKey(sample.Id));
        }

        public async Task<GolferDoc> GetGolferAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<GolferDoc>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task SaveGolferV2Async(GolferDocV2 doc)
        {
            await _containerV2.UpsertItemAsync(doc, new PartitionKey(doc.Id));
        }

        public async Task<GolferDocV2> GetGolferV2Async(string id)
        {
            try
            {
                var response = await _containerV2.ReadItemAsync<GolferDocV2>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<GolferDoc>> GetGolfersAsync()
        {
            var results = new List<GolferDoc>();
            using var feedIterator = _container.GetItemQueryIterator<GolferDoc>("SELECT * FROM c");
            while (feedIterator.HasMoreResults)
            {
                var response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }
            return results;
        }
    }
}
