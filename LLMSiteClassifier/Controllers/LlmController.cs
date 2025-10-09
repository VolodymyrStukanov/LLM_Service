
using LLMSiteClassifier.Services.LLMService;
using LLMSiteClassifier.Services.LLMService.models;
using Microsoft.AspNetCore.Mvc;

namespace LLMSiteClassifier.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LlmController : ControllerBase
    {
        private readonly LlmService llmService;
        public LlmController(LlmService llmService)
        {
            this.llmService = llmService;
        }

        
        [Route("SendGemini")]
        [HttpPost]
        public async Task<IActionResult> SendGemini([FromBody]string prompt)
        {
            var res = await llmService.GetCompletionAsync(LlmProvider.Gemini, prompt);
            return Ok(res);
        }
    }
}