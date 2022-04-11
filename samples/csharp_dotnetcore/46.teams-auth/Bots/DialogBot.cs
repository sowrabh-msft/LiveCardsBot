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

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text.Contains("ACv2"))
            {
                string[] path = { ".", "Resources", "initialCard.json" };
                var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
                var initialAdaptiveCard = GetFirstOptionsAdaptiveCard(path, turnContext.Activity.From.Name, member.Id);
                await turnContext.SendActivityAsync(MessageFactory.Attachment(initialAdaptiveCard), cancellationToken);
            }
            else
            {
                // Run the Dialog with the new message Activity.
                await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
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
                JObject authentication = JsonConvert.DeserializeObject<JObject>(value["authentication"].ToString());
                string state = value["state"].ToString();

                if (actiondata["verb"] == null)
                    return null;

                string verb = actiondata["verb"].ToString();

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
                        case "initiateSSO":
                            return await initiateSSOAsync(turnContext, cancellationToken);
                        case "initiateOAuth":
                            return await initiateOAuthAsync(turnContext, cancellationToken);
                        case "RefreshBasicCard":
                            // verify token in invoke payload and return AC response
                            return createAdaptiveCardInvokeResponseAsync(authentication, state);
                    }
                }
                // authToken or state is present. Verify token/state in invoke payload and return AC response
                else
                {
                    return createAdaptiveCardInvokeResponseAsync(authentication, state);
                }
            }
            return null;
        }

        private InvokeResponse createAdaptiveCardInvokeResponseAsync(JObject authentication, string state)
        {
            //verify token is present or not

            bool isTokenPresent = authentication != null ? true : false;
            bool isStatePresent = state != null && state != "" ? true : false;

            // TODO : Use token or state to perform operation on behalf of user

            string[] filepath = { ".", "Resources", "adaptiveCardResponseJson.json" };

            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var payloadData = new
            {
                authResult = isTokenPresent ? "SSO success" : isStatePresent ? "OAuth success" : "SSO/OAuth failed"
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
                            Type = ActionTypes.OpenUrl,
                            Value = signInLink,
                            Title = "Bot Service OAuth",
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
                            Type = ActionTypes.OpenUrl,
                            Value = signInLink,
                            Title = "Bot Service OAuth",
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

        private Attachment GetFirstOptionsAdaptiveCard(string[] filepath, string name = null, string userMRI = null)
        {
            var adaptiveCardJson = File.ReadAllText(Path.Combine(filepath));
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(adaptiveCardJson);
            var payloadData = new
            {
                createdById = userMRI,
                createdBy = name
            };
            var cardJsonstring = template.Expand(payloadData);
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(cardJsonstring),
            };
            return adaptiveCardAttachment;
        }

    }
}
