using System.ComponentModel.DataAnnotations;

namespace LLMService.Controllers.models
{    
    public record InputModel
    {
        [Required(ErrorMessage = "Prompt is required")]
        [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
        // [MaxLength(10000, ErrorMessage = "Prompt cannot exceed 10000 characters")]
        public string Prompt { get; init; } = string.Empty;

        [Required(ErrorMessage = "Provider is required")]
        public string Provider { get; init; } = string.Empty;

        [Required(ErrorMessage = "Model is required")]
        public string Model { get; init; } = string.Empty;
    }
}