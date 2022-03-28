using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.BotBuilderSamples.Bots;

namespace TeamsMessagingExtensionsSearch.Controllers
{
    public class GithubController : Controller
    {

        [HttpPost]
        [Route("api/gitHubEvent")]
        public async Task<IActionResult> PostEvents([FromBody] object data)
        {
            JObject payload = JObject.FromObject(data);
            if (payload["action"] != null)
            {
                string action = payload["action"].ToString();
                if (action == "closed" && payload["pull_request"] != null)
                {
                    TeamsMessagingExtensionsSearchBot.UpdateFluidContainer(payload["pull_request"]);
                }
            }

            return Ok();
        }

    }
}
