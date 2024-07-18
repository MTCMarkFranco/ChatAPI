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
        public async Task<IActionResult> UploadPdfFiles([FromForm] IFormCollection form)
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
                        // Upload file to blob storage
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }

            // 2. Create an Indexer to extract text from PDFs
            CreateIndexerAsync("acsgroundedsearch", this._searchCredential, "YourBlobConnectionString", "YourBlobContainerName").Wait();

            // 3. Run the indexer

            // 4. Call Semantic Kernel Planner and Execute the Plan

            
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

        private async Task CreateIndexerAsync(string searchServiceName, AzureKeyCredential credential, string blobConnectionString, string blobContainerName)
        {
            // Create a SearchIndexClient to send create/update index commands
            Uri serviceEndpoint = new Uri($"https://{searchServiceName}.search.windows.net/");
            SearchIndexerClient indexerClient = new SearchIndexerClient(serviceEndpoint, credential);  

            var skillset = new SearchIndexerSkillset(  
            name: "rfp-skills-definition-skillset",  
            skills: new List<SearchIndexerSkill>  
            {  
               new OcrSkill(
                    inputs: new List<InputFieldMappingEntry>()
                    {
                        new InputFieldMappingEntry(name: "image")
                        {
                            Source = "/document/normalized_images/*"
                        }
                    },
                    outputs: new List<OutputFieldMappingEntry>()
                    {
                        new OutputFieldMappingEntry(name: "text")
                        {
                            TargetName = "text"
                        }
                    })
                    {       
                    Name = "image",
                    Description = "Extracts text (plain and structured) from image.",
                    Context = "/document/normalized_images/*",
                    },  
                new MergeSkill(  
                    inputs: new List<InputFieldMappingEntry>  
                    {  
                        new InputFieldMappingEntry(name: "text")
                        { 
                            Source = "/document/content"
                        },  
                        new InputFieldMappingEntry(name: "itemsToInsert")
                        {
                            Source = "/document/normalized_images/*/text"
                        }, 
                        new InputFieldMappingEntry(name: "offsets")
                        {
                            Source = "/document/normalized_images/*/contentOffset"
                        }  
                    },  
                    outputs: new List<OutputFieldMappingEntry>  
                    {  
                        new OutputFieldMappingEntry(name: "mergedText")
                        {
                            TargetName = "mergedText"
                        }  
                    })  
                {  
                    Name = "#2",  
                    Context = "/document",  
                    InsertPreTag = " ",  
                    InsertPostTag = " "  
                },  
                new SplitSkill(  
                    inputs: new List<InputFieldMappingEntry>  
                    {  
                        new InputFieldMappingEntry(name: "text")
                        {
                            Source = "/document/mergedText"
                        }
                    },  
                    outputs: new List<OutputFieldMappingEntry>  
                    {  
                        new OutputFieldMappingEntry(name: "textItems")
                        {
                            TargetName = "pages"
                        }
                    })  
                {  
                    Name = "#3",  
                    Context = "/document",  
                    DefaultLanguageCode = SplitSkillLanguage.En,  
                    TextSplitMode = TextSplitMode.Pages,  
                    MaximumPageLength = 2000,  
                },  
                new AzureOpenAIEmbeddingSkill(  
                    inputs: new List<InputFieldMappingEntry>  
                    {  
                        new InputFieldMappingEntry(name: "text")
                        {

                        Source = "/document/pages/*"
                        } 
                    },  
                    outputs: new List<OutputFieldMappingEntry>  
                    {  
                        new OutputFieldMappingEntry(name: "embedding")
                        {
                            TargetName = "text_vector"
                        }
                    }  
                    )  
                {  
                    Name = "#4",  
                    Description = null,  
                    Context = "/document/pages/*",  
                    ResourceUri = new Uri("https://openaidevdemo.openai.azure.com"),
                    ApiKey = "<redacted>",
                    DeploymentId = "text-embedding-ada-002",
                    Dimensions = 1536,
                    ModelName = "text-embedding-ada-002",
                    AuthIdentity = null,
                    
                },  
                new VisionVectorizeSkill (  
                    inputs: new List<InputFieldMappingEntry>  
                    {  
                        new InputFieldMappingEntry(name: "image")
                        {
                            Source = "/document/normalized_images/*"
                        }
                    },  
                    outputs: new List<OutputFieldMappingEntry>  
                    {  
                        new OutputFieldMappingEntry(name: "vector")
                        {
                            TargetName = "image_vector"
                        }
                    },
                    modelVersion: "2023-04-15")  
                    {  
                        Name = "#5",  
                        Description = "An AI Services Vision vectorization skill for images",  
                        Context = "/document/normalized_images/*"
                        
                    } 
            })  
        {  
            Description = "Skillset to chunk documents and generate embeddings",  
            
        };  
  
        indexerClient.CreateOrUpdateSkillset(skillset);  
  
        // Now Create the Indexer utilizing the SkillSet
        
        var indexer = new SearchIndexer(  
            name: "rfp-indexer",  
            dataSourceName: "<your-data-source-name>",  
            targetIndexName: "rfp-skills-definition"
            
        );  

            
        var parameters = new IndexingParameters();

        parameters.IndexingParametersConfiguration = new IndexingParametersConfiguration()
        {
            
        };

        


        indexer.FieldMappings.Add(new FieldMapping("text_vector") { TargetFieldName = "text_vector" });
        indexer.FieldMappings.Add(new FieldMapping("chunk") { TargetFieldName = "chunk" });
        indexer.FieldMappings.Add(new FieldMapping("metadata_storage_path") { TargetFieldName = "metadata_storage_path" });
        indexer.FieldMappings.Add(new FieldMapping("title") { TargetFieldName = "title" });

        // Create or update the indexer in Azure Cognitive Search
        await indexerClient.CreateOrUpdateIndexerAsync(indexer);

        }

       private static async Task<IReadOnlyList<float>> GenerateEmbeddings(string text, OpenAIClient openAIClient)
        {
            var response = await openAIClient.GetEmbeddingsAsync("text-embedding-ada-002", new EmbeddingsOptions(text));

            return response.Value.Data[0].Embedding;
        }
    }
}