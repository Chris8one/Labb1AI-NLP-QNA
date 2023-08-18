using System;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.AI.Language.QuestionAnswering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    private static string translatorEndpoint = "https://api.cognitive.microsofttranslator.com";
    private static string cogSvcKey;
    private static readonly string cogSvcRegion = "westeurope";
    private static string cogSvcEndpoint;

    private static string botKey;
    private static string botEndpoint;
    private static string deploymentName = "production";
    private static string projectName = "LabNLPQNA";

    private static bool exit = false;

    static void Main(string[] args)
    {
        try
        {
            // Get config settings from AppSettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            cogSvcEndpoint = configuration["CognitiveServiceEndpoint"];
            cogSvcKey = configuration["CognitiveServiceKey"];

            // Get config settings from Appsettings for Bot
            botEndpoint = configuration["BotEndpoint"];
            botKey = configuration["BotKey"];


            // Set console encoding to unicode
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        try
        {
            Console.WriteLine("Please ask a question about mental illness (exit to quit)");

            while (!exit)
            {
                Console.Write("You: ");
                string userInput = Console.ReadLine();

                if (userInput.ToLower() == "exit")
                {
                    exit = true;
                    break;
                }
                else
                {
                    Translation(userInput);
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    static async void Translation(string question)
    {
        try
        {
            // Create client using endpoint and key for the translation
            AzureKeyCredential credentials = new AzureKeyCredential(cogSvcKey);
            Uri endpoint = new Uri(cogSvcEndpoint);
            TextAnalyticsClient CogClient = new TextAnalyticsClient(endpoint, credentials);

            // Get language
            DetectedLanguage detectedLanguage = CogClient.DetectLanguage(question);
            var language = detectedLanguage.Iso6391Name;

            // Translate if not already in English
            Console.Clear();

            if (language != "en")
            {
                string translatedText = await Translate(question, language);
                Console.WriteLine("(translated from " + language + ") " + translatedText);
                Console.Write("You: ");
                BotReply(translatedText);
            }
            else
            {
                Console.Write("You: " + question + "\n");
                BotReply(question);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    static async Task<string> Translate(string text, string sourceLanguage)
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
                string path = "/translate?api-version=3.0&from=" + sourceLanguage + "&to=en";
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

    static void BotReply(string question)
    {
        // Create client using endpoint and key for the bot
        AzureKeyCredential credentialbot = new AzureKeyCredential(botKey);
        Uri botEndpointUri = new Uri(botEndpoint);

        QuestionAnsweringClient client = new QuestionAnsweringClient(botEndpointUri, credentialbot);
        QuestionAnsweringProject project = new QuestionAnsweringProject(projectName, deploymentName);

        Response<AnswersResult> respone = client.GetAnswers(question, project);

        foreach (KnowledgeBaseAnswer answer in respone.Value.Answers)
        {
            Console.WriteLine($"\nBot: {answer.Answer}\n");
        }
    }
}