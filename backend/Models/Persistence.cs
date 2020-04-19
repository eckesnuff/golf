using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Models
{
    public class Persistence
    {
        private string _databaseId;
        private string _collectionName;
        private Uri _endpointUri;
        private string _primaryKey;
        private DocumentClient _client;

        public Persistence(Uri endpointUri, string primaryKey)
        {
            _databaseId = "GolferServiceDB";
            _collectionName="Golfers";
            _endpointUri = endpointUri;
            _primaryKey = primaryKey;
        }

        public async Task EnsureSetupAsync()
        {
            if (_client == null)
            {
                _client = new DocumentClient(_endpointUri, _primaryKey);
            }

            await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = _databaseId });
            var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);

            // Samples
            await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, new DocumentCollection() { Id = _collectionName });
        }

        public async Task SaveGolferAsync(GolferDoc sample)
        {
            await EnsureSetupAsync();

            var documentCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionName);
            await _client.UpsertDocumentAsync(documentCollectionUri, sample);
        }

        public async Task<GolferDoc> GetGolferAsync(string Id)
        {
            try{
            await EnsureSetupAsync();

            var documentUri = UriFactory.CreateDocumentUri(_databaseId, _collectionName, Id);
            var result = await _client.ReadDocumentAsync<GolferDoc>(documentUri);
            return result.Document;
            }
            catch(Exception){
                return null;
            }
        }

        public async Task<List<GolferDoc>> GetGolfersAsync()
        {
            await EnsureSetupAsync();

            var documentCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, _collectionName);

            // build the query
            var feedOptions = new FeedOptions() { MaxItemCount = -1 };
            var query = _client.CreateDocumentQuery<GolferDoc>(documentCollectionUri, "SELECT * FROM GolferDoc", feedOptions);
            var queryAll = query.AsDocumentQuery();

            // combine the results
            var results = new List<GolferDoc>();
            while (queryAll.HasMoreResults)
            {
                results.AddRange(await queryAll.ExecuteNextAsync<GolferDoc>());
            }

            return results;
        }
    }
}