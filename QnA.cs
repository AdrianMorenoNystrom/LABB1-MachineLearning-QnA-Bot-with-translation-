using Azure;
using Azure.AI.Language.QuestionAnswering;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LABB_1
{
    public class QnA
    {

        private Uri endpoint;
        private AzureKeyCredential credential;
        private string projectName = "LearnFAQ";
        private string deploymentName = "production";

        private QuestionAnsweringClient client;
        private QuestionAnsweringProject project;

        private static string cogSvcKey;
        private static string cogSvcRegion;
        private static string translatorEndpoint = "https://api.cognitive.microsofttranslator.com";

        // Konstruktor
        public QnA()
        {

            // Hämta konfig från appsettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();

            string endpointString = configuration["AZURE_QNA_ENDPOINT"];
            string azureQnaKey = configuration["AZURE_QNA_KEY"];

            endpoint = new Uri(endpointString);
            credential = new AzureKeyCredential(azureQnaKey);

            cogSvcKey = configuration["CognitiveServiceKey"];
            cogSvcRegion = configuration["CognitiveServiceRegion"];

            client = new QuestionAnsweringClient(endpoint, credential);
            project = new QuestionAnsweringProject(projectName, deploymentName);
            Run().GetAwaiter().GetResult();
        }

        public async Task Run()
        {
           
            // Set console encoding to unicode
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Console.WriteLine("Ask a question, type 'exit' to quit.");

            while (true)
            {
                Console.WriteLine("Question: ");
                string question = Console.ReadLine();

                if (question.ToLower() == "exit")
                {
                    break;
                }

                try
                {
                    // Få svaret från QnA servicen
                    Response<AnswersResult> response = await client.GetAnswersAsync(question, project);
                    foreach (KnowledgeBaseAnswer answer in response.Value.Answers)
                    {
                        // Se vilket språk frågan är i
                        string language = await GetLanguage(question);
                        Console.WriteLine("\nQuestion language: " + language+"\n");
                        Console.WriteLine($"Question: {question}\n");
                        Console.WriteLine($"Answer: {answer.Answer}\n");

                        // Om inte frågan är Engelska som är "original" språket, översätt.
                        if (language != "en")
                        {
                            string translatedText = await Translate(answer.Answer, language);
                            Console.WriteLine($"\nTranslation to {language}: " + translatedText+"\n");
                        }
                        else
                        {
                            Console.WriteLine("The input text is already in language of original answer.\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Request error: {ex.Message}");
                }
            }
        }

        private async Task<string> GetLanguage(string text)
        {
            // Språket som svaren redan är i är Engelska.
            string language = "en";

            // Use the Translator detect function
            object[] body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    // Build the request
                    string path = "/detect?api-version=3.0";
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(translatorEndpoint + path);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", cogSvcKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", cogSvcRegion);

                    // Send the request and get response
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    // Read response as a string
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Parse JSON array and get language
                    JArray jsonResponse = JArray.Parse(responseContent);
                    language = (string)jsonResponse[0]["language"];
                }
            }

            // return the language
            return language;
        }

        private async Task<string> Translate(string text, string targetLanguage)
        {
            string translation = "";

            // Use the Translator translate function
            object[] body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    // Build the request
                    string path = $"/translate?api-version=3.0&to={targetLanguage}";
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(translatorEndpoint + path);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", cogSvcKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", cogSvcRegion);

                    // Send the request and get response
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    // Read response as a string
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Parse JSON array and get translation
                    JArray jsonResponse = JArray.Parse(responseContent);
                    translation = (string)jsonResponse[0]["translations"][0]["text"];
                }
            }

            // Return the translation
            return translation;
        }
    }
}
