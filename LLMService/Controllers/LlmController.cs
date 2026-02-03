using LLMService.Services.LLMService;
using LLMService.Services.LLMService.models;
using Microsoft.AspNetCore.Mvc;

namespace LLMService.Controllers
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

        
        [Route("SendMessage")]
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody]string prompt)
        {
            var res = await llmService.GetCompletionAsync(LlmProvider.Gemini, prompt);
            return Ok(res);
        }
    }
}