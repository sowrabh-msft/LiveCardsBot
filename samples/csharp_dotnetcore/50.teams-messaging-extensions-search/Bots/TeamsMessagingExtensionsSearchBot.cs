﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AdaptiveCards;
using AdaptiveCards.Templating;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples.Bots
{
     public class TeamsMessagingExtensionsSearchBot : TeamsActivityHandler
    {
        public readonly string _baseUrl;
        public static Dictionary<string, string> pRToFluid = new Dictionary<string, string>();
        public TeamsMessagingExtensionsSearchBot(IConfiguration configuration):base()
        {
            this._baseUrl = configuration["BaseUrl"];
        }

        protected override async Task<MessagingExtensionResponse> OnTeamsAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedLinkQuery query, CancellationToken cancellationToken)
        {
            var paths = new[] { ".", "Resources", "githubCard1.json" };
            string filepath = Path.Combine(paths);
            string pullRequestId = query.Url.Split('/')[query.Url.Split('/').Length - 1];
            object cardToSend = await GetGitHubPRCard(pullRequestId);
            var heroCard = new ThumbnailCard
            {
                Title = "Github",
                Text = query.Url,
            };

            var attachments = new MessagingExtensionAttachment(AdaptiveCard.ContentType, null, cardToSend, null, null, heroCard.ToAttachment());
            var result = new MessagingExtensionResult("list", "result", new[] { attachments });

            return new MessagingExtensionResponse(result);
        }

        protected override async Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            var text = query?.Parameters?[0]?.Value as string ?? string.Empty;

            switch (text)
            {
                case "adaptive card":
                    MessagingExtensionResponse response = await GetAdaptiveCard();
                    return response;

                case "connector card":
                    MessagingExtensionResponse connectorCard = GetConnectorCard();
                    return connectorCard;

                case "result grid":
                    MessagingExtensionResponse resultGrid = GetResultGrid();
                    return resultGrid;
            }

            var packages = await FindPackages(text);

            // We take every row of the results and wrap them in cards wrapped in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.
            var attachments = packages.Select(package =>
            {
                var previewCard = new ThumbnailCard { Title = package.Item1, Tap = new CardAction { Type = "invoke", Value = package } };
                if (!string.IsNullOrEmpty(package.Item5))
                {
                    previewCard.Images = new List<CardImage>() { new CardImage(package.Item5, "Icon") };
                }

                var attachment = new MessagingExtensionAttachment
                {
                    ContentType = HeroCard.ContentType,
                    Content = new HeroCard { Title = package.Item1 },
                    Preview = previewCard.ToAttachment()
                };

                return attachment;
            }).ToList();

            // The list of MessagingExtensionAttachments must we wrapped in a MessagingExtensionResult wrapped in a MessagingExtensionResponse.
            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = attachments
                }
            };
        }
        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JObject query, CancellationToken cancellationToken)
        {
            // The Preview card's Tap should have a Value property assigned, this will be returned to the bot in this event. 
            var (packageId, version, description, projectUrl, iconUrl) = query.ToObject<(string, string, string, string, string)>();

            // We take every row of the results and wrap them in cards wrapped in in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.

            var card = new ThumbnailCard
            {
                Title = $"{packageId}, {version}",
                Subtitle = description,
                Buttons = new List<CardAction>
                    {
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Nuget Package", Value = $"https://www.nuget.org/packages/{packageId}" },
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Project", Value = projectUrl },
                    },
            };

            if (!string.IsNullOrEmpty(iconUrl))
            {
                card.Images = new List<CardImage>() { new CardImage(iconUrl, "Icon") };
            }

            var attachment = new MessagingExtensionAttachment
            {
                ContentType = ThumbnailCard.ContentType,
                Content = card,
            };

            return Task.FromResult(new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            });
        }

        // Generate a set of substrings to illustrate the idea of a set of results coming back from a query. 
        private async Task<IEnumerable<(string, string, string, string, string)>> FindPackages(string text)
        {
            var obj = JObject.Parse(await (new HttpClient()).GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true"));
            return obj["data"].Select(item => (item["id"].ToString(), item["version"].ToString(), item["description"].ToString(), item["projectUrl"]?.ToString(), item["iconUrl"]?.ToString()));
        }

        public async Task<MessagingExtensionResponse> GetAdaptiveCard()
        {
            var paths = new[] { ".", "Resources", "RestaurantCard.json" };
            string filepath = Path.Combine(paths); 
            var previewcard = new ThumbnailCard
            {
                Title = "Adaptive Card",
                Text = "Please select to get Adaptive card"
            };
            var adaptiveList = await FetchAdaptive(filepath);

            var attachment = new MessagingExtensionAttachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveList.Content,
                Preview = previewcard.ToAttachment()
            };

            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            };
        }
        public MessagingExtensionResponse GetConnectorCard()
        {
            var path = new[] { ".", "Resources", "connectorCard.json" };
            var filepath = Path.Combine(path);   
            var previewcard = new ThumbnailCard
            {
                Title = "O365 Connector Card",
                Text = "Please select to get Connector card"
            };

            var connector = FetchConnector(filepath);
            var attachment = new MessagingExtensionAttachment
            {
                ContentType = O365ConnectorCard.ContentType,
                Content = connector.Content,
                Preview = previewcard.ToAttachment()
            };

            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            };
        }

        public  async Task<Attachment> FetchAdaptive(string filepath)
        {
            var adaptiveCardJson = File.ReadAllText(filepath);
            object adaptiveCard = JsonConvert.DeserializeObject(adaptiveCardJson);
            // string fluidContainerId = await CreateFluidContainer(adaptiveCard);
            //adaptiveCard.FallbackText = fluidContainerId;
            var adaptiveCardAttachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveCard
            };      
            return adaptiveCardAttachment;
        }

        public Attachment FetchConnector(string filepath)
        {
            var connectorCardJson = File.ReadAllText(filepath);
            var connectorCardAttachment = new MessagingExtensionAttachment
            {
                ContentType = O365ConnectorCard.ContentType,
                Content = JsonConvert.DeserializeObject(connectorCardJson),

            };
            return connectorCardAttachment;
        }

        public MessagingExtensionResponse GetResultGrid()
        {
            var imageFiles = Directory.EnumerateFiles("wwwroot", "*.*", SearchOption.AllDirectories)
            .Where(s => s.EndsWith(".jpg"));

            List<MessagingExtensionAttachment> attachments = new List<MessagingExtensionAttachment>();

            foreach (string img in imageFiles)
            {
                var image = img.Split("\\");                
                var thumbnailCard = new ThumbnailCard();
                thumbnailCard.Images = new List<CardImage>() { new CardImage(_baseUrl + "/" + image[1]) };
                var attachment = new MessagingExtensionAttachment
                {
                    ContentType = ThumbnailCard.ContentType,
                    Content = thumbnailCard,
                };
                attachments.Add(attachment);
            }
            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "grid",
                    Attachments = attachments
                }
            };
        }

        private async static Task<string> CreateFluidContainer(object adaptiveCard)
        {
            string timeStamp = System.DateTime.Now.ToString("yyyyMMddHHmmssffff");
            var payload = new
            {
                card = adaptiveCard,
                version = timeStamp
            };
            string requestAsString = JsonConvert.SerializeObject(payload);
            string requestUri = $"https://45be-2404-f801-8028-1-9143-ac5f-702d-5226.ngrok.io/createCard";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            request.Content = new StringContent(requestAsString, System.Text.Encoding.UTF8, "application/json");

            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.SendAsync(request);
            var payloadAsString = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ResourceResponse>(payloadAsString);
            return result.Id;

        }

        public async static void UpdateFluidContainer(JToken result)
        {
            string id = result["id"].ToString();
            //"890313670"
            string state = "CLOSED"; ;
            
            //"open"
            string title = result["title"].ToString();
            //"Pull request card changes for github app"
            string url = result["html_url"].ToString();
            //"https://github.com/sowrabh-msft/LiveCardsBot/pull/1"
            string creator = result["user"]["login"].ToString();
            //"baton17"
            string reviewer = "No reviewers";
            if (result["requested_reviewers"] != null && result["requested_reviewers"].HasValues)
            {
                reviewer = result["requested_reviewers"][0]["login"].ToString();
            }

            JObject templateJson = JObject.Parse(File.ReadAllText(@".\Resources\githubCard2.json"));

            AdaptiveCardTemplate template = new AdaptiveCardTemplate(templateJson);
            var myData = new
            {
                Id = id,
                State = state,
                Title = title,
                Url = url,
                Creator = creator,
                Reviewer = reviewer
            };

            string cardJson = template.Expand(myData); ;

            AdaptiveCard adaptiveCard = AdaptiveCard.FromJson(cardJson).Card;
            string fluidContainerId;
            pRToFluid.TryGetValue(id, out fluidContainerId);
            adaptiveCard.FallbackText = fluidContainerId;
            string timeStamp = System.DateTime.Now.ToString("yyyyMMddHHmmssffff"); ;
            var payload = new
            {
                container = fluidContainerId,
                cardData = new
                {
                    card = adaptiveCard,
                    version = timeStamp
                }
            };
            string requestAsString = JsonConvert.SerializeObject(payload);
            string requestUri = $"https://45be-2404-f801-8028-1-9143-ac5f-702d-5226.ngrok.io/updateCard";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = new StringContent(requestAsString, System.Text.Encoding.UTF8, "application/json");

            HttpClient httpClient = new HttpClient();
            await httpClient.SendAsync(request);
        }

        private async static Task<object> GetGitHubPRCard(string pId)
        {
            string requestUri = $"https://api.github.com/repos/sowrabh-msft/LiveCardsBot/pulls/{pId}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github.v3+json");
            request.Headers.TryAddWithoutValidation("User-Agent", "githubApp");
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = await httpClient.SendAsync(request);
            var payloadAsString = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(payloadAsString);
            
            string id = result["id"].ToString();
            //"890313670"
            string state = result["state"].ToString();
            string color = "warning";
            if (state == "open")
            {
                state = "OPEN";
                color = "warning";
            }

            if (state == "closed")
            {
                state = "CLOSED";
                color = "good";
            }
            //"open"
            string title = result["title"].ToString();
            //"Pull request card changes for github app"
            string url = result["html_url"].ToString();
            //"https://github.com/sowrabh-msft/LiveCardsBot/pull/1"
            string creator = result["user"]["login"].ToString();
            //"baton17"
            string reviewer = "No reviewers";
            if (result["requested_reviewers"] != null && result["requested_reviewers"].HasValues)
            {
                reviewer = result["requested_reviewers"][0]["login"].ToString();
            }
       

            JObject templateJson = JObject.Parse(File.ReadAllText(@".\Resources\githubCard1.json"));

            AdaptiveCardTemplate template = new AdaptiveCardTemplate(templateJson);
            var myData = new
            {
                Id = id,
                State = state,
                Title = title,
                Url = url,
                Creator = creator,
                Reviewer = reviewer,
                Color = color
            };

            string cardJson = template.Expand(myData);

            AdaptiveCard adaptiveCard = AdaptiveCard.FromJson(cardJson).Card;
            string fluidContainerId = await CreateFluidContainer(adaptiveCard);
            if (!pRToFluid.ContainsKey(id))
            {
                pRToFluid.Add(id, fluidContainerId);
            }
            adaptiveCard.FallbackText = fluidContainerId;
            return adaptiveCard;
        }
    }
}
