using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Net.Http.Headers;
using System.Diagnostics;
using edu_poc_app.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.CognitiveServices.Speech;
using Azure.Storage.Files.Shares;
using Azure.Storage;
using Azure.Storage.Blobs;
using static System.Net.WebRequestMethods;
using Microsoft.AspNetCore.Components;
using System.Reflection.Metadata;




namespace edu_poc_app.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        [BindProperty]
        public int Number { get; set; }
        public string test1 = string.Empty;
        const string GPT4V_KEY = "<your gpt4-openAI-key>"; 
        const string DALLEKey = "<your Dall-e key here>";
        List<string> imageList = new List<string>();
  

        private const string GPT4V_ENDPOINT = "<your gpt4 engpoint here>";
        private const string dallefunctionUrl = "<your dalle-e endpoint here>";
        private const string sttfunctionUrl = "<your Function App url here>";
        public async Task OnPostAsync()
        {
            var bookType = Request.Form["booktype"];
            var items = Request.Form["SelectItems"];
            var gender = Request.Form["sGender"];
            var situation = Request.Form["sSituation"];
            int pages = Convert.ToInt32(Request.Form["sPages"]);

            List<string> imageUrl = new List<string>();
            if (!string.IsNullOrEmpty(bookType))
            {
                string strContext = "You are going to help me create " + pages + " pages of stories for toddlers, I can give primary object for my story, you will add more fantasy to the output. Also, I want my story to be like Eric Carle. Make sure to give a nice attractive name for the story at the very beginning. Don't add any extra contents in the output, I just need only name of the story first and the actual story with page numbers. Make sure you are not adding adult contents as the end audiences are toddlers.";
                string strPrompt = "Can you tell me a " + situation + " " + bookType + " story around object like " + items + " for 5-year-old " + gender + ".";
                
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("api-key", GPT4V_KEY);
                    var payload = new
                    {
                        messages = new object[]
                        {
                  new {
                      role = "system",
                      content = new object[] {
                          new {
                              type = "text",
                              text = strContext
                          }
                      }
                  },
                  new {
                      role = "user",
                      content = new object[] {
                          new {
                              type = "text",
                              text = strPrompt
                          }
                      }
                  }
                        },
                        temperature = 0.7,
                        top_p = 0.95,
                        max_tokens = 4096,
                        stream = false
                    };

                    var response = await httpClient.PostAsync(GPT4V_ENDPOINT, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {

                        var responseData = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                        Console.WriteLine(responseData);
                        ViewData["confirmation"] = responseData.choices[0].message.content;
                        String output = responseData.choices[0].message.content;


                        for (int i = 1; i <= pages; i++)
                        {
                            int pFrom = output.IndexOf("Page " + i) + ("Page " + i).Length;
                            int pTo = 0;
                            if (i != pages)
                                pTo = output.LastIndexOf("Page " + (i + 1));
                            else
                                pTo = output.Length;
                            imageList.Add(output.Substring(pFrom, pTo - pFrom).Trim(':', '\n'));

                        }

                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode}, {response.ReasonPhrase}");
                    }
                }

                using (var httpClient = new HttpClient())
                {

                    string strAudioInput = string.Empty;
                    for (int i = 0; i < imageList.Count; i++)
                    {
                        strAudioInput += imageList[i].ToString();
                    }
                    var payload = new
                    {
                        name = strAudioInput
                    };
                    var response = await httpClient.PostAsync(sttfunctionUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {

                        
                        Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                        string strAudioPath= @"https://<your storage account>.blob.core.windows.net/speech/"+response.Content.ReadAsStringAsync().Result + "?<your storage account SAS";
                        ViewData["audioPath"] = strAudioPath;
                    }
                    





                }

                using (var httpClient = new HttpClient())
                {


                    for (int i = 0; i < imageList.Count; i++)
                    {
                        var payload = new
                        {
                            prompt = imageList[i].ToString()
                        };
                        var response = await httpClient.PostAsync(dallefunctionUrl, new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                        if (response.IsSuccessStatusCode)
                        {

                            ViewData["image" + (i + 1)] = response.Content.ReadAsStringAsync().Result;
                            Console.WriteLine(response.Content.ReadAsStringAsync().Result);

                        }


                    }
                }

            }
        }


        public string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
        }

    }
}

