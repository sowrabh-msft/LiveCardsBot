﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;

namespace Microsoft.BotBuilderSamples
{
    public interface IBotServices
    {
        LuisRecognizer HomeAutomation { get; }
        LuisRecognizer Weather { get; }
        LuisRecognizer Dispatch { get; }
        QnAMaker SampleQnA { get; }
    }
}
