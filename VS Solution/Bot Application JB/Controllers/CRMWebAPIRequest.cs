using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace CustomVisionBot
{
    public class Utilities
    {

        // Take a CRM Web API request string (and optionally json data), issue it, and return the HTTP Response:
        public static async Task<HttpResponseMessage> CRMWebAPIRequest(string apiRequest, HttpContent requestContent, string requestType)
        {
            AuthenticationContext authContext = new AuthenticationContext(WebConfigurationManager.AppSettings["adOath2AuthEndpoint"], false);
            UserCredential credentials = new UserCredential(WebConfigurationManager.AppSettings["dynamicsUsername"], WebConfigurationManager.AppSettings["dynamicsPassword"]);
            AuthenticationResult tokenResult = authContext.AcquireToken(WebConfigurationManager.AppSettings["dynamicsUri"], WebConfigurationManager.AppSettings["adClientId"], credentials);

            HttpResponseMessage apiResponse;

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(WebConfigurationManager.AppSettings["dynamicsUri"]);
                httpClient.Timeout = new TimeSpan(0, 2, 0);
                httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                if (requestType == "retrieve")
                {
                    apiResponse = await httpClient.GetAsync(apiRequest);
                }
                else if (requestType == "create")
                {
                    apiResponse = await httpClient.PostAsync(apiRequest, requestContent);
                }
                else if (requestType == "update")
                {
                    HttpRequestMessage message = new HttpRequestMessage();
                    message.Content = requestContent;
                    message.Method = new HttpMethod("PATCH");
                    Uri baseUri = new Uri(WebConfigurationManager.AppSettings["dynamicsUri"]);
                    Uri myFullUri = new Uri(baseUri, apiRequest);
                    message.RequestUri = myFullUri;
                    apiResponse = await httpClient.SendAsync(message);
                }
                else
                {
                    apiResponse = null;
                }
            }
            return apiResponse;
        }
    }
}