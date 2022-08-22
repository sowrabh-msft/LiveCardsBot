// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Connector.Authentication;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using AdaptiveCards.Templating;
using System.IO;
using AdaptiveCards;
using Microsoft.Bot.Schema.Teams;
using System.Linq;
using System.Net;
using Microsoft.Bot.Connector;

namespace Microsoft.BotBuilderSamples
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : TeamsActivityHandler where T : Dialog
    {
        protected readonly BotState ConversationState;
        protected readonly Dialog Dialog;
        protected readonly ILogger Logger;
        protected readonly BotState UserState;
        private readonly string _connectionName = "SnehBotTeamsAuthADv2";
        public DialogBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
        }

        private async Task<string> GetSignInLinkAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var userTokenClient = turnContext.TurnState.Get<UserTokenClient>();
            var resource = await userTokenClient.GetSignInResourceAsync(_connectionName, turnContext.Activity as Activity, null, cancellationToken).ConfigureAwait(false);
            return resource.SignInLink;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        } 

        Dictionary<string, string> cardIdToFile = new Dictionary<string, string>(){
            {"1", "ABSSORefresh.json"},
            {"2", "ABOAuthRefresh.json"},
            {"3", "ABSSOButton.json"},
            {"4", "ABOAuthButton.json"},
            {"5", "SSORefresh.json"},
            {"6", "OAuthRefresh.json"},
            {"7", "ABAB.json"},
            {"8", "ABSSORefresh412.json"},
            {"9", "SSORefresh412.json"},
            {"10", "SSORefreshWithoutSignIn.json" }
        };

        Dictionary<string, string> cardIdToCardName = new Dictionary<string, string>(){
            {"1", "Auth Block With SSO And Refresh"},
            {"2", "Auth Block With OAuth And Refresh"},
            {"3", "Auth Block With SSO And Button"},
            {"4", "Auth Block With OAuth And Button"},
            {"5", "Refresh With SSO"},
            {"6", "Refresh with OAuth"},
            {"7", "Authentication Block on Every Refresh"},
            {"8", "Test 412 with Auth block and Refresh"},
            {"9", "Test 412 with refresh"},
            { "10", "SSO Without SignIn"}
        };

        protected override async Task<MessagingExtensionResponse> OnTeamsAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedLinkQuery query, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            string cardId = query.Url.Split('/')[query.Url.Split('/').Length - 1];
            if (!cardIdToFile.ContainsKey(cardId)) {
                return new MessagingExtensionResponse();
            }
            string[] path = { ".", "Resources", cardIdToFile[cardId] };
            var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
            var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id).Content;
            var heroCard = new ThumbnailCard
            {
                Title = cardIdToCardName[cardId],
                Text = query.Url,
            };

            var attachments = new MessagingExtensionAttachment(AdaptiveCard.ContentType, null, initialAdaptiveCard, null, null, heroCard.ToAttachment());
            var result = new MessagingExtensionResult("list", "result", new[] { attachments });

            return new MessagingExtensionResponse(result);
        }

        protected override async Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            var cardId = query?.Parameters?[0]?.Value as string ?? string.Empty;
            if (!cardIdToFile.ContainsKey(cardId))
            {
                return new MessagingExtensionResponse();
            }

            string[] path = { ".", "Resources", cardIdToFile[cardId] };
            var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
            var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id).Content;
            var previewcard = new ThumbnailCard
            {
                Title = "Adaptive Card",
                Text = cardIdToCardName[cardId]
            };

            var attachment = new MessagingExtensionAttachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = initialAdaptiveCard,
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

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            if (turnContext.Activity.Text.Contains("hi"))
            {
                string[] path = { ".", "Resources", "options.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("ABSSORefresh"))
            {
                string[] path = { ".", "Resources", "ABSSORefresh.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("ABOAuthRefresh"))
            {
                string[] path = { ".", "Resources", "ABOAuthRefresh.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("ABSSOButton"))
            {
                string[] path = { ".", "Resources", "ABSSOButton.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("ABOAuthButton"))
            {
                string[] path = { ".", "Resources", "ABOAuthButton.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("SSORefresh"))
            {
                string[] path = { ".", "Resources", "SSORefresh.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("OAuthRefresh"))
            {
                string[] path = { ".", "Resources", "OAuthRefresh.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else if (turnContext.Activity.Text.Contains("ABAB"))
            {
                string[] path = { ".", "Resources", "ABAB.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, signInLink, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Please send 'hi' for options"), cancellationToken);
            }
        }
        
        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            if(turnContext.Activity.Name == "signin/verifyState")
            {
                Logger.LogInformation("Running dialog with signin/verifystate from an Invoke Activity.");

                // The OAuth Prompt needs to see the Invoke Activity in order to complete the login process.

                // Run the Dialog with the new Invoke Activity.
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
            }

            else if (turnContext.Activity.Name == "adaptiveCard/action")
            {

                if (turnContext.Activity.Value == null)
                    return null;


                JObject value = JsonConvert.DeserializeObject<JObject>(turnContext.Activity.Value.ToString());

                if (value["action"] == null)
                    return null;

                JObject actiondata = JsonConvert.DeserializeObject<JObject>(value["action"].ToString());

                if (actiondata["verb"] == null)
                    return null;

                string verb = actiondata["verb"].ToString();
                JObject authentication = null;

                if (value["authentication"] != null) {
                    authentication = JsonConvert.DeserializeObject<JObject>(value["authentication"].ToString());
                    string token = authentication["token"].ToString();
                    var userTokenClient = turnContext.TurnState.Get<UserTokenClient>();
                    var tokenResource = await userTokenClient.ExchangeTokenAsync(turnContext.Activity.From.Id, _connectionName, turnContext.Activity.ChannelId, new TokenExchangeRequest(null, token), cancellationToken).ConfigureAwait(false);
                    Console.WriteLine(tokenResource);
                    if ("TestPreConditionFailed".Equals(verb) || "TestPreConditionFailedWithoutSignIn".Equals(verb))
                    {
                        var loginReqResponse = JObject.FromObject(new
                        {
                            statusCode = 412,
                            type = "application/vnd.microsoft.error.preconditionFailed",
                            value = new {
                            code = "412",
                            message = "token expired"
                        }
                        });

                        return CreateInvokeResponse(loginReqResponse);
                    }
                }

                string state = null;
                if (value["state"] != null) {
                    state = value["state"].ToString();
                }

                // Loop sso and oauth for testing
                if ("loopOAuth".Equals(verb))
                {
                    return await initiateOAuthAsync(turnContext, cancellationToken);
                }
                else if ("loopSSO".Equals(verb))
                {
                    return await initiateSSOAsync(turnContext, cancellationToken);
                }

                // authToken and state are absent, handle verb
                if (authentication == null && state == null)
                {
                    switch (verb)
                    {
                        case "TestPreConditionFailed":
                            return await initiateSSOAsync(turnContext, cancellationToken);
                        case "TestPreConditionFailedWithoutSignIn":
                            return await initiateSSOWithoutSignAsync(turnContext, cancellationToken);
                        case "initiateSSO":
                            return await initiateSSOAsync(turnContext, cancellationToken);
                        case "initiateOAuth":
                            return await initiateOAuthAsync(turnContext, cancellationToken); //basicRefresh
                        case "basicRefresh":
                            return createAdaptiveCardInvokeResponseAsync(null, null, true);
                        case "abrefresh":
                            return createAdaptiveCardInvokeResponseAsync(authentication, state, false, "ABAB.json");
                    }
                }
                // authToken or state is present. Verify token/state in invoke payload and return AC response
                else
                {
                    switch (verb)
                    {
                        case "abrefresh":
                            return createAdaptiveCardInvokeResponseAsync(authentication, state, false, "ABAB.json");
                        default:
                            return createAdaptiveCardInvokeResponseAsync(authentication, state);
                    }
                    // verify token in invoke payload and return AC response
                    
                }
            }

            else if (turnContext.Activity.Name == "composeExtension/queryLink") 
                            return CreateInvokeResponse(await OnTeamsAppBasedLinkQueryAsync(turnContext, SafeCast<AppBasedLinkQuery>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

            else if (turnContext.Activity.Name == "composeExtension/query")
                return CreateInvokeResponse(await OnTeamsMessagingExtensionQueryAsync(turnContext, SafeCast<MessagingExtensionQuery>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));
            return null;
        }

        private static T SafeCast<T>(object value)
        {
            var obj = value as JObject;
            if (obj == null)
            {
                throw new InvokeResponseException(HttpStatusCode.BadRequest, $"expected type '{value.GetType().Name}'");
            }

            return obj.ToObject<T>();
        }

        private InvokeResponse createAdaptiveCardInvokeResponseAsync(JObject authentication, string state, bool isBasicRefresh = false, string fileName = "adaptiveCardResponseJson.json")
        {
            //verify token is present or not

            bool isTokenPresent = authentication != null ? true : false;
            bool isStatePresent = state != null && state != "" ? true : false;

            // TODO : Use token or state to perform operation on behalf of user

            string[] filepath = { ".", "Resources", fileName };

            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var authResultData = isTokenPresent ? "SSO success" : isStatePresent ? "OAuth success" : "SSO/OAuth failed";
            if (isBasicRefresh)
            {
                authResultData = "Refresh done";
            }
            var payloadData = new
            {
                authResult = authResultData
            };

            var cardJsonstring = template.Expand(payloadData);

            var adaptiveCardResponse =  new AdaptiveCardInvokeResponse()
            {
                StatusCode = 200,
                Type = AdaptiveCard.ContentType,
                Value = JsonConvert.DeserializeObject(cardJsonstring)
            };
            return CreateInvokeResponse(adaptiveCardResponse);
        }

        private InvokeResponse createMessageResponseAsync()
        {
            var messageResponse = JObject.FromObject(new
            {
                statusCode = 200,
                type = "application/vnd.microsoft.activity.message",
                value = "Message!"
            });

            return CreateInvokeResponse(messageResponse);
        }


        private async Task<InvokeResponse> initiateSSOAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            var oAuthCard = new OAuthCard
            {
                Text = "Signin Text",
                ConnectionName = "newConnection",
                TokenExchangeResource = new TokenExchangeResource
                {
                    Id = Guid.NewGuid().ToString()
                },
                Buttons = new List<CardAction>
                    {
                        new CardAction
                        {
                            Type = ActionTypes.Signin,
                            Value = signInLink,
                            Title = "Please sign in",
                        },
                    }
            };


            var loginReqResponse = JObject.FromObject(new
            {
                statusCode = 401,
                type = "application/vnd.microsoft.activity.loginRequest",
                value = oAuthCard
            });

            return CreateInvokeResponse(loginReqResponse);
        }

        private async Task<InvokeResponse> initiateSSOWithoutSignAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            var oAuthCard = new OAuthCard
            {
                Text = "Signin Text",
                ConnectionName = "newConnection",
                TokenExchangeResource = new TokenExchangeResource
                {
                    Id = Guid.NewGuid().ToString()
                },
                Buttons = new List<CardAction>
                    {
                        new CardAction
                        {
                            Type = ActionTypes.Signin,
                            Value = "",
                            Title = "Please sign in",
                        },
                    }
            };


            var loginReqResponse = JObject.FromObject(new
            {
                statusCode = 401,
                type = "application/vnd.microsoft.activity.loginRequest",
                value = oAuthCard
            });

            return CreateInvokeResponse(loginReqResponse);
        }

        private async Task<InvokeResponse> initiateOAuthAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            var signInLink = await GetSignInLinkAsync(turnContext, cancellationToken).ConfigureAwait(false);
            var oAuthCard = new OAuthCard
            {
                Text = "Signin Text",
                ConnectionName = "newConnection",
                Buttons = new List<CardAction>
                    {
                        new CardAction
                        {
                            Type = ActionTypes.Signin,
                            Value = signInLink,
                            Title = "Please sign in",
                        },
                    }
            };


            var loginReqResponse = JObject.FromObject(new
            {
                statusCode = 401,
                type = "application/vnd.microsoft.activity.loginRequest",
                value = oAuthCard
            });

            return CreateInvokeResponse(loginReqResponse);
        }

        private Attachment GetFirstOptionsAdaptiveCard(string[] filepath, string signInLink, string name = null, string userMRI = null)
        {
            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var payloadData = new
            {
                createdById = userMRI,
                createdBy = name
            };
            var cardJsonstring = template.Expand(payloadData);
            var card = JsonConvert.DeserializeObject<JObject>(cardJsonstring);
            if(card["authentication"] != null && card["authentication"]["buttons"] != null && card["authentication"]["buttons"][0] != null)
            {
                card["authentication"]["buttons"][0]["value"] = signInLink;
            }
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = card,
            };
            return adaptiveCardAttachment;
        }

    }
}
