using System.Reflection.Metadata;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.SemanticKernel.Text;

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
            string serviceName = "acsgroundedsearch"; // replace with your search service name
            //string indexName = "YourIndexName"; // replace with your index name
            string searchApiKey = configuration.GetValue<string>("SearchApiKey"); // replace with your search service api key
            string openAiApiKey = configuration.GetValue<string>("OpenAiApiKey"); // replace with your search service api key

            _serviceEndpoint = new Uri($"https://{serviceName}.search.windows.net/");
            _searchCredential = new AzureKeyCredential(searchApiKey);

            // Initialize OpenAI client
            var credential = new AzureKeyCredential(openAiApiKey);
            _openAIClient = new OpenAIClient(new Uri("https://openaidevdemo.openai.azure.com"), credential);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public Task<IActionResult> UploadPdfFiles([FromForm] IFormCollection form)
        {
            // Setup index for RFP.
            var indexName = Guid.NewGuid().ToString();
            var searchClient = new SearchClient(_serviceEndpoint, indexName, _searchCredential);
            var indexClient = new SearchIndexClient(_serviceEndpoint, _searchCredential);
             indexClient.CreateOrUpdateIndex(GetSampleIndex(indexName));

            // 1. Upload Files to Blob Storage
            var files = form.Files;
            long size = files.Sum(f => f.Length);
            var documents = new List<SearchDocument>();
            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    try
                    {
                        // 1. Chnk the File Using SK
                        #pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        using (var reader = new StreamReader(formFile.OpenReadStream()))
                        {
                            var content = reader.ReadToEnd();
                            // load the content into a pdf reader
                            
                            var paragraphs = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            var chunks = TextChunker.SplitPlainTextParagraphs(paragraphs, 2000, 200);
                        }
                        #pragma warning restore SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                        // 2. vectorize Chunks

                        // 3. populate index

                        // reutn an IActionResult of Ok here
                        return Task.FromResult<IActionResult>(Ok());

                    }
                    catch (Exception ex)
                    {
                           throw;                 
                    }
                    
                }
            }

            return Task.FromResult<IActionResult>(StatusCode(500, "Something Happemed"));

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
            var response = await openAIClient.GetEmbeddingsAsync("text-embedding-ada-002", new EmbeddingsOptions(text));

            return response.Value.Data[0].Embedding;
        }
    }
}