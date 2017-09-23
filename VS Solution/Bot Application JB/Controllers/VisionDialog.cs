using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Web.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace CustomVisionBot
{
    // As per the Dialogs model example in the Bot Builder docs,
    //  we add a class to represent our conversation:
    [Serializable]
    public class VisionDialog : IDialog<object>
    {

        private const string IdentifyOption = "Identify a Part or Product";
        private const string KBOption = "Knowledge Base Search";
        private const string CaseOption = "Create a Case";

        public void StartingOptionsMessage(IDialogContext context)
        {
            // Present the options to the user, with a continuation back to the appropriate handler:
            PromptDialog.Choice(context, AfterChoiceAsync,
                new PromptOptions<string>("How can I help you?", null, null,
                new List<string>() { IdentifyOption, KBOption, CaseOption }, 3, null));
        }

        // Start the dialog:
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }

        // Handle an initial message from the user
        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> argument)
        {
            // Present the options to the user, with a continuation back to the appropriate handler:
            StartingOptionsMessage(context);
        }

        // handler for the initial user selection:
        public async Task AfterChoiceAsync(IDialogContext context, IAwaitable<string> argument)
        {
            var option = await argument;

            // Handle user selection as appropriate:
            if (option == IdentifyOption)
            {
                {
                    // Prompt user to upload an image:
                    await context.PostAsync("Please take or upload a photo of the item.");
                    context.Wait(AfterImageProvided);
                }
            }
            else if (option == KBOption)
            {
                // prompt the user to provide a search query, and specify the appropriate handler:
                PromptDialog.Text(context, AfterSearchTermProvidedAsync, "Please enter a keyword for searching..", null, 3);
            }

            else if (option == CaseOption)
            {
                // We will implement these capabilities in a future post
                // In the meantime, include a notification:
                await context.PostAsync("We will just need some basic information from you to create the case.");
                PromptDialog.Text(context, GetFullNameAsync, "Please enter your full name.", null, 3);
            }
            
            else
            {
                await context.PostAsync("This demo bot has not been configured for that yet.");
                StartingOptionsMessage(context);
            }

        }

        private async Task AfterImageProvided(IDialogContext context, IAwaitable<IMessageActivity> result)
        {

            // retrieve the Custom Vision credentials:
            string predictionKey = WebConfigurationManager.AppSettings["visionPredictionKey"];
            Guid projectGuid = new Guid(WebConfigurationManager.AppSettings["visionProjectGuid"]);

            //  Create the Hero Card varible to be used later to let the user know that no match can be found.
            var heroCard = new HeroCard

            {
                Title = "Not a Hot Dog",
                Subtitle = "- Jian Yang",
                Text = "HBO Series - Silicon Valley",
                Images = new List<CardImage> { new CardImage("http://www.8asians.com/wp-content/uploads/2017/05/Jian-Yang.jpg") },
                Buttons = new List<CardAction> { new CardAction() { Title = "Done", Type = ActionTypes.ImBack, Value = "Done" } }
            };

            try
            {

                // Retrieve user message, and ensure it has an attachment:
                var message = await result;
                if (message.Attachments != null && message.Attachments.Any())
                {
                    var attachment = message.Attachments.First();
                    using (HttpClient httpClient = new HttpClient())
                    {
                        // Skype & MS Teams attachment URLs are secured by a JwtToken, so we need to pass the token from our bot.
                        if ((message.ChannelId.Equals("skype", StringComparison.InvariantCultureIgnoreCase) ||
                            message.ChannelId.Equals("msteams", StringComparison.InvariantCultureIgnoreCase)))
                            //&& new Uri(attachment.ContentUrl).Host.EndsWith("skype.com"))
                        {
                            var token = await new MicrosoftAppCredentials().GetTokenAsync();
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        }

                        // retrieve our image, and create a byte array:
                        byte[] imageByteArray = await httpClient.GetByteArrayAsync(attachment.ContentUrl);

                        // Create our request client:
                        var client = new HttpClient();

                        // Add a request header with our Custom Vision Prediction Key:
                        client.DefaultRequestHeaders.Add("Prediction-Key", predictionKey);


                        // Construct Prediction URL, using Project GUID:
                        string url = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.0/Prediction/"
                            + projectGuid.ToString() + "/image?";

                        // Instantiate response:
                        HttpResponseMessage response;

                        using (var content = new ByteArrayContent(imageByteArray))
                        {
                            // Set content type, and retrieve response:
                            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            response = client.PostAsync(url, content).Result;

                            using (var stream = response.Content.ReadAsStreamAsync().Result)
                            {

                                // Serialize our response with data contract:
                                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(CustomVision.JSON.Response));
                                CustomVision.JSON.Response visionJsonResponse = ser.ReadObject(stream) as CustomVision.JSON.Response;

                                // Ensure we have some results from the Custom Vision Service:
                                if (visionJsonResponse != null && visionJsonResponse.Predictions != null && visionJsonResponse.Predictions.Length > 0)
                                {
                                    // Build a D365 Web API request to retrieve a matching product from the catalog:
                                    HttpResponseMessage productResponse = await Utilities.CRMWebAPIRequest("api/data/v8.2/products?" +
                                        "$select=name,productnumber,producturl,description,cfs_externalimageurl&" +
                                        "$top=1&$filter=name eq '" + visionJsonResponse.Predictions[0].Tag + "'",
                                        null, "retrieve");

                                    // Check to make sure our request was successful:
                                    if (productResponse.IsSuccessStatusCode)
                                    {

                                        // Read our response into a JSON Array:
                                        string myString = productResponse.Content.ReadAsStringAsync().Result;
                                        JObject productResults =
                                                JObject.Parse(productResponse.Content.ReadAsStringAsync().Result);
                                        JArray items = (JArray)productResults["value"];


                                        // Ensure we have some results:
                                        if (items.Count > 0)
                                        {
                                            if (visionJsonResponse.Predictions[0].Probability > .75)

                                            {

                                                // format probability for presentation:
                                                string formattedProbability = Math.Round((visionJsonResponse.Predictions[0].Probability * 100), 1).ToString() + "%";

                                                // prepare message back to user:    
                                                IMessageActivity msgProductReply = context.MakeMessage();
                                                msgProductReply.Type = ActivityTypes.Message;
                                                msgProductReply.TextFormat = TextFormatTypes.Markdown;

                                                // prepare rich card, showing product image, and button to see specs:
                                                if ((string)items[0]["producturl"] != null && (string)items[0]["cfs_externalimageurl"] != null)
                                                {

                                                    List<CardImage> cardImages = new List<CardImage>();
                                                    cardImages.Add(new CardImage(url: (string)items[0]["cfs_externalimageurl"]));
                                                    HeroCard actionCard = new HeroCard()
                                                    {

                                                        Title = visionJsonResponse.Predictions[0].Tag + " (" + (string)items[0]["productnumber"] + ")",
                                                        Subtitle = "Identified with probability of " + formattedProbability,
                                                        Text = (string)items[0]["description"],
                                                        Images = cardImages,
                                                        Buttons = {
                                                            new CardAction() { Title = "View Product Specs", Type = ActionTypes.OpenUrl, Value = (string)items[0]["producturl"] },
                                                            new CardAction() { Title = "Done", Type = ActionTypes.ImBack, Value = "Done" }
                                                        }
                                                    };

                                                    Attachment actionAttachment = actionCard.ToAttachment();
                                                    msgProductReply.Attachments.Add(actionAttachment);

                                                }

                                                // Post message::
                                                await context.PostAsync(msgProductReply);
                                                context.Wait(MessageReceivedAsync);
                                            }

                                            else
                                            
                                            {
                                                // prepare message back to user:    
                                                IMessageActivity msgVisionReply = context.MakeMessage();
                                                msgVisionReply.Type = ActivityTypes.Message;
                                                msgVisionReply.TextFormat = TextFormatTypes.Markdown;

                                                //  retrieve the Hero Card to let the user know that no match can be found.
                                                Attachment actionAttachment = heroCard.ToAttachment();
                                                msgVisionReply.Attachments.Add(actionAttachment);
                                                
                                                //    // Inform user and prompt them to try again:
                                                //    await context.PostAsync("I wasn't able to identify a product.");
                                                //    StartingOptionsMessage(context);
                                                //}
                                                await context.PostAsync(msgVisionReply);
                                                context.Wait(MessageReceivedAsync);
                                            }

                                     
                                        }
                                        else
                                        {
                                            // prepare message back to user:    
                                            IMessageActivity msgVisionReply = context.MakeMessage();
                                            msgVisionReply.Type = ActivityTypes.Message;
                                            msgVisionReply.TextFormat = TextFormatTypes.Markdown;

                                            //  retrieve the Hero Card to let the user know that no match can be found.
                                            Attachment actionAttachment = heroCard.ToAttachment();
                                            msgVisionReply.Attachments.Add(actionAttachment);

                                            //    // Inform user and prompt them to try again:
                                            //    await context.PostAsync("I wasn't able to identify a product.");
                                            //    StartingOptionsMessage(context);
                                            //}
                                            await context.PostAsync(msgVisionReply);
                                            context.Wait(MessageReceivedAsync);
                                        }
                                    }
                                }
                                else

                                {
                                    // prepare message back to user:    
                                    IMessageActivity msgVisionReply = context.MakeMessage();
                                    msgVisionReply.Type = ActivityTypes.Message;
                                    msgVisionReply.TextFormat = TextFormatTypes.Markdown;

                                    //  retrieve the Hero Card to let the user know that no match can be found.
                                    Attachment actionAttachment = heroCard.ToAttachment();
                                    msgVisionReply.Attachments.Add(actionAttachment);

                                    //    // Inform user and prompt them to try again:
                                    //    await context.PostAsync("I wasn't able to identify a product.");
                                    //    StartingOptionsMessage(context);
                                    //}
                                    await context.PostAsync(msgVisionReply);
                                    context.Wait(MessageReceivedAsync);
                                }
                            }
                        }
                    }

                }
                else
                {

                    // Prompt user to take a photo:
                    await context.PostAsync("Please take a photo of the item");
                    context.Wait(AfterImageProvided);
                }
            }
            catch (Exception e)
            {
                string reply = "Sorry, something went wrong.  Please try again. Details: " + e.ToString();
                await context.PostAsync(reply);
                context.Done<bool>(true);
            }

        }
        /// <summary>
        /// Search KB Article
        /// </summary>
        /// <param name="context">Dialog Context</param>
        /// <param name="argument">Arguments entered by user</param>
        /// <returns>State Object</returns>
        public async Task AfterSearchTermProvidedAsync(IDialogContext context, IAwaitable<string> argument)
    
        {
            // retrieve the query term provided by the user:
            string keyword = await argument;

            // retrieve the KB article based on key word
            HttpResponseMessage kbResponse = await Utilities.CRMWebAPIRequest("api/data/v8.2/knowledgearticles?" +
                                                "$select=title,articlepublicnumber,description,content" +
                                                "&$filter=contains(keywords,'" + keyword + "')",
                                                null, "retrieve");

            //if (kbResponse.IsSuccessStatusCode)
            //{
            //    // Read our response into a JSON Array:
            string myString = kbResponse.Content.ReadAsStringAsync().Result;
            JObject kbResults = JObject.Parse(myString);
            JArray items = (JArray)kbResults["value"];
            JObject item;
            string message = string.Empty;
            // Ensure we have some results:
            if (items.Count > 0)
            {
                IMessageActivity msgMarkdownReply = context.MakeMessage();
                await context.PostAsync("Found an Article that might help you..");

                for (int i = 0; i < items.Count; i++)
                {
                    item = (JObject)items[i];
                    var heroCard = new HeroCard
                    {
                        Title = (string)item["title"],
                        Subtitle = (string)item["articlepublicnumber"],
                        Text = (string)item["description"],
                        //Images = new List<CardImage> { new CardImage("https://sec.ch9.ms/ch9/7ff5/e07cfef0-aa3b-40bb-9baa-7c9ef8ff7ff5/buildreactionbotframework_960.jpg") },
                        Buttons = {
                                     new CardAction() { Title = "View Article", Type = ActionTypes.OpenUrl, Value = (string)WebConfigurationManager.AppSettings["portalkburl"] + (string)item["articlepublicnumber"] },
                                     new CardAction() { Title = "Done", Type = ActionTypes.ImBack, Value = "Done" }
                    }

                    };

                    Attachment actionAttachment = heroCard.ToAttachment();
                    msgMarkdownReply.Attachments.Add(actionAttachment);

                }
                //await context.PostAsync(message);
                await context.PostAsync(msgMarkdownReply);
                
            }
            else
            {
                await context.PostAsync("Sorry, there is no article available for '" + keyword + "'");
                
            }

        }
        /// <summary>
        /// Input Full name
        /// </summary>
        /// <param name="context">Dialog Context</param>
        /// <param name="argument">Arguments entered by user</param>
        /// <returns>State Object</returns>
         
        // Customer name and Description for Case
        private string _fullName = string.Empty;
        private string _issueDescription = string.Empty;
        public async Task GetFullNameAsync(IDialogContext context, IAwaitable<string> argument)
        {
            // retrieve the query term provided by the user:
            _fullName = await argument;

            // Get Description of the Issue
            PromptDialog.Text(context, GetIssueDescriptionAsync, "Please provide description of the issue.", null, 3);
        }
        /// <summary>
        /// Input Description
        /// </summary>
        /// <param name="context">Dialog Context</param>
        /// <param name="argument">Arguments entered by user</param>
        /// <returns>State Object</returns>
        public async Task GetIssueDescriptionAsync(IDialogContext context, IAwaitable<string> argument)
        {
            // retrieve the query term provided by the user:
            _issueDescription = await argument;
            await context.PostAsync("Please wait while we create a case for you.");
            await RaiseaTicketAsync(context);
        }

        /// <summary>
        /// Create Case
        /// </summary>
        /// <param name="context">Dialog Context</param>
        /// <returns>State Object</returns>
        public virtual async Task RaiseaTicketAsync(IDialogContext context)
        {
            // Create a Ticket with name and description
            string caseNumber =
                await
                    CRMCreateCaseWebAPIRequest(_fullName, _issueDescription);
            IMessageActivity msgCaseReply = context.MakeMessage();
            await context.PostAsync("A Case has been created, see below for details");
            
                var heroCard = new HeroCard
            {
                Title = "Case Number: " + caseNumber,
                //Subtitle = "Hello",
                Text = "Description: " + _issueDescription,
                Buttons = {
                                     new CardAction() { Title = "View Customer Portal", Type = ActionTypes.OpenUrl, Value = (string)WebConfigurationManager.AppSettings["portalcaselistingurl"] },
                                     new CardAction() { Title = "Done", Type = ActionTypes.ImBack, Value = "Done" }
                    }

            };
            Attachment actionAttachment = heroCard.ToAttachment();
            msgCaseReply.Attachments.Add(actionAttachment);

            await context.PostAsync(msgCaseReply);
        }


        /// <summary>
        /// Process user's choice
        /// </summary>
        /// <param name="context">Dialog Context</param>
        /// <param name="argument">Arguments entered by user</param>
        /// <returns>State Object</returns>
        public async Task ContinueChoiceAsync(IDialogContext context, IAwaitable<string> argument)
        {
            var option = await argument;
            if (option == "Yes")
            {
                // prompt the user to provide a search query, and specify the appropriate handler:
                context.Wait(MessageReceivedAsync);
            }
            else if (option == "No")
            {
                // Finish the Message
                await context.PostAsync("Thanks!!");
                context.Done("Done");
            }

        }

        /// <summary>
        /// Get response from CRM
        /// </summary>
        /// <param name="apiRequest">API request</param>
        /// <returns>response message</returns>
        private async Task<HttpResponseMessage> CRMWebAPIRequest(string apiRequest)
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
                apiResponse = await httpClient.GetAsync(apiRequest);
            }
            return apiResponse;
        }

        /// <summary>
        /// Create Case Request
        /// </summary>
        /// <param name="customerName">Customer Name</param>
        /// <param name="caseDescription">Issue description</param>
        /// <returns>Case Number</returns>
        private async Task<string> CRMCreateCaseWebAPIRequest(string customerName, string caseDescription)
        {

            AuthenticationContext authContext = new AuthenticationContext(WebConfigurationManager.AppSettings["adOath2AuthEndpoint"], false);
            UserCredential credentials = new UserCredential(WebConfigurationManager.AppSettings["dynamicsUsername"], WebConfigurationManager.AppSettings["dynamicsPassword"]);
            AuthenticationResult tokenResult = authContext.AcquireToken(WebConfigurationManager.AppSettings["dynamicsUri"], WebConfigurationManager.AppSettings["adClientId"], credentials);
            HttpResponseMessage createContactResponse;
            string caseNumber = String.Empty;

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

                JObject contact = new JObject();
                contact["lastname"] = customerName;
                Guid contactyId = Guid.Empty;
                Guid contactxId = Guid.Empty;
                Guid caseId = Guid.Empty;

                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(contact), System.Text.Encoding.UTF8, "application/json");

                // We will use a basic 'contains' filter on the contact name
                HttpResponseMessage contactResponse = await Utilities.CRMWebAPIRequest("api/data/v8.2/contacts?" +
                    "$select=fullname,contactid&" +
                    "$filter= contains(fullname, '" + customerName + "')", null, "retrieve");

                // Check to make sure our request was successful:

                    //    // Read our response into a JSON Array:
                string myJString = contactResponse.Content.ReadAsStringAsync().Result;
                JObject kbResults = JObject.Parse(myJString);
                JArray items = (JArray)kbResults["value"];
                JObject item;
                string message = string.Empty;

                //  Check for existing Contact
                if (items.Count > 0)

                //  existing Contact
                {
                    contactyId = (Guid)(items[0]["contactid"]);
                    JObject incident = new JObject();
                    incident["customerid_contact@odata.bind"] = "/contacts(" + contactyId + ")";
                    incident["title"] = "Case Created by BOT - " + DateTime.Now.ToShortDateString();
                    incident["description"] = caseDescription;
                    content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(incident), System.Text.Encoding.UTF8,
                        "application/json");
                    var request = new HttpRequestMessage(HttpMethod.Post, "/api/data/v8.2/incidents") { Content = content };
                    HttpResponseMessage createCaseResponse = await httpClient.SendAsync(request);
   
                    if (contactResponse.IsSuccessStatusCode)
                    {
                        caseId =
                            new Guid();
                    }

                    HttpResponseMessage caseResponse =
                        await CRMWebAPIRequest("/api/data/v8.2/incidents?$select=ticketnumber,&$orderby=createdon asc");
                    caseNumber = await GetValueFromResponse(caseResponse, "ticketnumber");

                }

                else

                //  New Contact
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "/api/data/v8.2/contacts") { Content = content };
                    createContactResponse = await httpClient.SendAsync(request);
                    if (createContactResponse.IsSuccessStatusCode)
                    {
                        contactxId =
                           new Guid(
                               (createContactResponse.Headers.GetValues("OData-EntityId")
                                   .FirstOrDefault()
                                   .Split('[', '(', ')', ']'))[1]);
                    }
                    if (contactxId != Guid.Empty)
                    {
                        JObject incident = new JObject();
                        incident["customerid_contact@odata.bind"] = "/contacts(" + contactxId + ")";
                        incident["title"] = "Case Created by BOT - " + DateTime.Now.ToShortDateString();
                        incident["description"] = caseDescription;
                        content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(incident), System.Text.Encoding.UTF8,
                          "application/json");
                        request = new HttpRequestMessage(HttpMethod.Post, "/api/data/v8.2/incidents") { Content = content };
                        HttpResponseMessage createCaseResponse = await httpClient.SendAsync(request);
                        if (createContactResponse.IsSuccessStatusCode)
                        {
                            caseId =
                               new Guid(
                                   (createContactResponse.Headers.GetValues("OData-EntityId")
                                       .FirstOrDefault()
                                       .Split('[', '(', ')', ']'))[1]);
                        }
                        HttpResponseMessage caseResponse =
                            await CRMWebAPIRequest("/api/data/v8.2/incidents?$select=ticketnumber,&$orderby=createdon asc");
                        caseNumber = await GetValueFromResponse(caseResponse, "ticketnumber");
                    }
                }
            }
            return caseNumber;
        }

        /// <summary>
        /// Get value from Repsonse
        /// </summary>
        /// <param name="responseMessage">Response Message</param>
        /// <param name="attributeName">Attribute name</param>
        /// <returns>Attribute Value</returns>
        private async Task<string> GetValueFromResponse(HttpResponseMessage responseMessage, string attributeName)
        {
            string attributeValue = string.Empty;
            if (responseMessage.IsSuccessStatusCode)
            {
                //// Read our response into a JSON Array:
                string responseString = responseMessage.Content.ReadAsStringAsync().Result;
                JObject Results = JObject.Parse(responseString);
                JArray items = (JArray)Results["value"];
                JObject item;

                // Ensure we have some results:
                if (items.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        item = (JObject)items[i];
                        attributeValue = (string)item[attributeName];
                    }
                }

            }
            return attributeValue;
        }

    }

}
