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
        private readonly LlmService _llmService;
        private readonly HashSet<string> _allowedModels;
        private readonly ILogger<LlmController> _logger;
        public LlmController(LlmService llmService, IConfiguration config, ILogger<LlmController> logger)
        {
            _llmService = llmService;            
            _logger = logger;
            var models = config.GetSection("LlmServiceSettings").GetSection("AllowedModels").Get<string[]>()!;
            _allowedModels = models != null 
                ? new HashSet<string>(models, StringComparer.OrdinalIgnoreCase) 
                : new HashSet<string>();
        }
        
        [Route("SendMessage")]
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody]InputModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model validation failed: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }
            
            if (!Enum.TryParse<LlmProvider>(model.Provider, true, out var provider))
            {
                _logger.LogWarning("Invalid provider requested: {Provider}", model.Provider);
                ModelState.AddModelError(nameof(model.Provider), 
                    $"Invalid provider '{model.Provider}'. Allowed: {string.Join(", ", Enum.GetNames<LlmProvider>())}");
                return BadRequest(ModelState);
            }
            
            if (!_allowedModels.Contains(model.Model))
            {
                _logger.LogWarning("Unauthorized model requested: {Model} for provider {Provider}", model.Model, model.Provider);
                ModelState.AddModelError(nameof(model.Model), "Model '{model.Model}' is not allowed");
                return BadRequest(ModelState);
            }
            
            try
            {
                _logger.LogInformation("Sending message to {Provider} with model {Model}", provider, model.Model);
                var res = await _llmService.Send(provider, model.Model, model.Prompt);                
                _logger.LogInformation("Successfully received response from {Provider}", provider);

                return Ok(res);                
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while communicating with LLM provider {Provider}", model.Provider);
                return StatusCode(503, new 
                { 
                    error = "Service temporarily unavailable. Please try again later.",
                    details = ex.Message 
                });
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout for provider {Provider}", model.Provider);
                return StatusCode(504, new 
                { 
                    error = "Request timed out. The LLM provider took too long to respond." 
                });
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Unsupported provider: {Provider}", model.Provider);
                return StatusCode(500, new 
                { 
                    error = "Provider configuration error",
                    details = ex.Message 
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation for provider {Provider}", model.Provider);
                return StatusCode(500, new 
                { 
                    error = "Failed to process LLM response",
                    details = ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request for provider {Provider}", 
                    model.Provider);
                return StatusCode(500, new 
                { 
                    error = "An unexpected error occurred while processing your request." 
                });
            }
        }

        [Route("Info")]
        [HttpGet]
        public IActionResult Info()
        {
            return Ok(new 
            { 
                status = "healthy",
                timestamp = DateTime.UtcNow,
                allowedProviders = Enum.GetNames<LlmProvider>(),
                allowedModels = _allowedModels.ToArray()
            });
        }
    }
}