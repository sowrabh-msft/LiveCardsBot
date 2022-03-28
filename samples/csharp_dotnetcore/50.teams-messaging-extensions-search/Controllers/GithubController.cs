using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace TeamsMessagingExtensionsSearch.Controllers
{
    public class GithubController : Controller
    {

        [HttpPost]
        [Route("api/gitHubEvent")]
        public async Task<IActionResult> PostEvents([FromBody] object data)
        {
            return Ok();
        }

    }
}
