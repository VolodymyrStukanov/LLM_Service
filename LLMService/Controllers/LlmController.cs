using LLMService.Controllers.models;
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
        private readonly string[] allowedModels;
        public LlmController(LlmService llmService, IConfiguration config)
        {
            this.llmService = llmService;
            this.allowedModels = config.GetSection("LlmServiceSettings").GetSection("AllowedModels").Get<string[]>()!;
        }
        
        [Route("SendMessage")]
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody]InputModel model)
        {
            try
            {
                if(Enum.GetNames<LlmProvider>().Contains(model.Provider) && allowedModels.Contains(model.Model))
                {
                    var provider = (LlmProvider)Enum.Parse(typeof(LlmProvider), model.Provider);
                    var res = await llmService.GetCompletionAsync(provider, model.Model, model.Prompt);
                    return Ok(res);
                    
                }
                else
                {
                    return Ok("Error.");
                }
                
            }
            catch(Exception ex)
            {
                return Ok("Error.");                
            }
        }
    }
}