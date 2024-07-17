using System.Reflection.Metadata;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PdfProcessingApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PdfProcessController : ControllerBase
    {
        private static int ModelDimensions = 1536;
        private static string SemanticSearchConfigName = "my-semantic-config";
        private readonly Uri _serviceEndpoint;
        private readonly AzureKeyCredential _searchCredential;
        private readonly OpenAIClient _openAIClient;

        public PdfProcessController(IConfiguration configuration)
        {
            string serviceName = "ww-proto-search"; // replace with your search service name
            //string indexName = "YourIndexName"; // replace with your index name
            string searchApiKey = configuration.GetValue<string>("SearchApiKey"); // replace with your search service api key
            string openAiApiKey = configuration.GetValue<string>("OpenAiApiKey"); // replace with your search service api key

            _serviceEndpoint = new Uri($"https://{serviceName}.search.windows.net/");
            _searchCredential = new AzureKeyCredential(searchApiKey);

            // Initialize OpenAI client
            var credential = new AzureKeyCredential(openAiApiKey);
            _openAIClient = new OpenAIClient(new Uri("https://ww-proto-openai.openai.azure.com"), credential);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPdfFiles([FromForm] IFormCollection form)
        {
            // Setup index for RFP.
            var indexName = Guid.NewGuid().ToString();
            var searchClient = new SearchClient(_serviceEndpoint, indexName, _searchCredential);
            var indexClient = new SearchIndexClient(_serviceEndpoint, _searchCredential);
             indexClient.CreateOrUpdateIndex(GetSampleIndex(indexName));

            // Process files.
            var files = form.Files;
            long size = files.Sum(f => f.Length);
            var documents = new List<SearchDocument>();
            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    try
                    {
                        // Assuming the file content is directly readable and indexable
                        // For PDFs, consider using a library to extract text content
                        using (var reader = new StreamReader(formFile.OpenReadStream()))
                        {
                            var documentKeyPairs = new Dictionary<string, object>();

                            string id = formFile.Name;
                            string title = formFile.FileName;
                            string content = await reader.ReadToEndAsync();

                            float[] titleEmbeddings = (await GenerateEmbeddings(title, _openAIClient)).ToArray();
                            float[] contentEmbeddings = (await GenerateEmbeddings(content, _openAIClient)).ToArray();

                            documentKeyPairs["id"] = id;
                            documentKeyPairs["title"] = title;
                            documentKeyPairs["content"] = content;
                            documentKeyPairs["titleVector"] = titleEmbeddings;
                            documentKeyPairs["contentVector"] = contentEmbeddings;

                            documents.Add(new SearchDocument(documentKeyPairs));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }

            try
            {
                var result = await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents));

                // call openapi

                return Ok(new { count = files.Count, size, indexed = result.Value.Results.Count });
            }
            catch (Exception e)
            {
                throw;
            }
        }

        internal static SearchIndex GetSampleIndex(string name)
        {
            string vectorSearchProfile = "my-vector-profile";
            string vectorSearchHnswConfig = "my-hnsw-vector-config";

            SearchIndex searchIndex = new(name)
            {
                VectorSearch = new()
                {
                    Profiles =
                    {
                        new VectorSearchProfile(vectorSearchProfile, vectorSearchHnswConfig)
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration(vectorSearchHnswConfig)
                    }
                },
                SemanticSearch = new()
                {
                    Configurations =
                    {
                       new SemanticConfiguration(SemanticSearchConfigName, new()
                       {
                           TitleField = new SemanticField("title"),
                           ContentFields =
                           {
                               new SemanticField("content")
                           }
                       })
                    },
                },
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("title") { IsFilterable = true, IsSortable = true },
                    new SearchableField("content") { IsFilterable = true },
                    new SearchField("titleVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = ModelDimensions,
                        VectorSearchProfileName = vectorSearchProfile
                    },
                    new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = ModelDimensions,
                        VectorSearchProfileName = vectorSearchProfile
                    }
                }
            };

            return searchIndex;
        }

        private static async Task<IReadOnlyList<float>> GenerateEmbeddings(string text, OpenAIClient openAIClient)
        {
            var response = await openAIClient.GetEmbeddingsAsync("embedding", new EmbeddingsOptions(text));

            return response.Value.Data[0].Embedding;
        }
    }
}