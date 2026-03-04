# LLM Service

A robust, production-ready ASP.NET Core service for unified communication with multiple Large Language Model (LLM) providers including OpenAI, Claude, Gemini, Mistral, and Grok.

## Features

- 🔌 **Multi-Provider Support** - Communicate with 5 major LLM providers through a single unified API
- 🛡️ **Enterprise-Grade Error Handling** - Comprehensive error handling with detailed logging
- 🔄 **Automatic Retries** - Built-in retry logic with exponential backoff using Polly
- ⚡ **High Performance** - Thread-safe provider caching with concurrent request handling
- 📝 **Structured Logging** - Integrated Serilog for comprehensive logging and debugging
- ✅ **Input Validation** - ASP.NET Core model validation with clear error messages
- 🔧 **Configurable** - Easy configuration via appsettings.json
- 🎯 **Type-Safe** - Strongly-typed models and enum-based provider selection

## Supported LLM Providers

| Provider | Models Supported | Status |
|----------|-----------------|--------|
| OpenAI | GPT-4, GPT-4 Mini, GPT-4 Nano | ✅ Active |
| Claude (Anthropic) | Claude 3.5 Haiku | ✅ Active |
| Gemini (Google) | Gemini 2.5 Flash | ✅ Active |
| Mistral | Small, Medium, Large | ✅ Active |
| Grok (X.AI) | Grok 4 Fast | ✅ Active |

## Architecture

```
┌─────────────────────────────────────────┐
│         LlmController                   │
│  - Request validation                   │
│  - Error handling & HTTP responses      │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│         LlmService                      │
│  - Provider caching (thread-safe)       │
│  - Client lifecycle management          │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│    LlmHttpClientFactory                 │
│  - Creates provider-specific clients    │
│  - Dependency injection                 │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│      BaseLlmHttpClient                  │
│  - Common error handling                │
│  - JSON parsing with logging            │
│  - Template method pattern              │
└─────────────────┬───────────────────────┘
                  │
        ┌─────────┴─────────┬─────────┬─────────┐
        ▼                   ▼         ▼         ▼
  ClaudeClient      OpenAiClient  GeminiClient  etc.
```

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- API keys for the LLM providers you want to use

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/llm-service.git
cd llm-service
```

2. Configure your API keys in `appsettings.json`:
```json
{
  "LlmServiceSettings": {
    "AllowedModels": [
      "gpt-4.1",
      "claude-3-5-haiku-20241022",
      "gemini-2.5-flash:generateContent"
    ],
    "Providers": {
      "OpenAI": {
        "Headers": {
          "Authorization": "Bearer YOUR_OPENAI_API_KEY"
        },
        "BaseUrl": "https://api.openai.com/v1/chat/completions",
        "TimeoutSeconds": 120,
        "MaxRetries": 5
      }
    }
  }
}
```

3. Run the service:
```bash
dotnet run
```

The service will start on `https://localhost:5001` (or your configured port).

## API Documentation

### Send Message to LLM

**Endpoint:** `POST /api/Llm/SendMessage`

**Request Body:**
```json
{
  "prompt": "What is the capital of France?",
  "provider": "OpenAI",
  "model": "gpt-4.1"
}
```

**Success Response (200 OK):**
```json
"The capital of France is Paris."
```

**Error Responses:**

- `400 Bad Request` - Invalid input or unsupported provider/model
```json
{
  "errors": {
    "Provider": ["Invalid provider 'InvalidProvider'. Allowed: OpenAI, Grok, Claude, Mistral, Gemini"]
  }
}
```

- `503 Service Unavailable` - Provider API is down
```json
{
  "error": "Service temporarily unavailable. Please try again later.",
  "details": "Connection timeout"
}
```

- `504 Gateway Timeout` - Request took too long
```json
{
  "error": "Request timed out. The LLM provider took too long to respond."
}
```

### Get Service Info

**Endpoint:** `GET /api/Llm/Info`

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2024-02-09T10:30:00Z",
  "allowedProviders": ["OpenAI", "Grok", "Claude", "Mistral", "Gemini"],
  "allowedModels": ["gpt-4.1", "claude-3-5-haiku-20241022", ...]
}
```

## Configuration

### Provider Configuration

Each provider requires specific configuration in `appsettings.json`:

```json
{
  "LlmServiceSettings": {
    "AllowedModels": ["model1", "model2"],
    "Providers": {
      "ProviderName": {
        "BaseUrl": "https://api.provider.com/v1/endpoint",
        "Headers": {
          "Authorization": "Bearer YOUR_API_KEY",
          "Custom-Header": "value"
        },
        "TimeoutSeconds": 30,
        "MaxRetries": 3
      }
    }
  }
}
```

**Configuration Parameters:**

- `AllowedModels` - Whitelist of model names that can be used
- `BaseUrl` - API endpoint for the provider
- `Headers` - HTTP headers to send with each request (typically authentication)
- `TimeoutSeconds` - Request timeout (default: 30)
- `MaxRetries` - Number of retry attempts on transient failures (default: 3)

### Logging Configuration

The service uses Serilog for structured logging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

Logs are written to:
- `logs/all_logs.txt` - All logs (daily rolling)
- `logs/error_logs.txt` - Errors and fatal only (daily rolling)
- Console - Errors and fatal only

## Usage Examples

### C# Client Example

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };

var request = new
{
    prompt = "Explain quantum computing in simple terms",
    provider = "Claude",
    model = "claude-3-5-haiku-20241022"
};

var response = await client.PostAsJsonAsync("/api/Llm/SendMessage", request);
var result = await response.Content.ReadAsStringAsync();

Console.WriteLine(result);
```

### cURL Example

```bash
curl -X POST https://localhost:5001/api/Llm/SendMessage \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "What is machine learning?",
    "provider": "OpenAI",
    "model": "gpt-4.1"
  }'
```

### Python Example

```python
import requests

url = "https://localhost:5001/api/Llm/SendMessage"
payload = {
    "prompt": "Write a haiku about programming",
    "provider": "Gemini",
    "model": "gemini-2.5-flash:generateContent"
}

response = requests.post(url, json=payload)
print(response.text)
```

## Error Handling

The service implements comprehensive error handling at multiple levels:

### Input Validation
- Required fields validation
- Model whitelist checking
- Provider enum validation
- Automatic ModelState validation

### HTTP Client Level
- JSON parsing errors with full response logging
- HTTP status code validation
- Timeout handling
- Network error handling

### Service Level
- Provider caching errors
- Client creation failures

### Controller Level
- Returns appropriate HTTP status codes
- Provides detailed error messages
- Logs all errors with structured logging

### Example Error Logs

When JSON parsing fails:
```
[Error] Failed to parse Claude response. 
Status: 200, 
Raw JSON: {"result":{"type":"text","text":"Hello"},"usage":{"tokens":10}}
System.Text.Json.JsonException: The JSON property 'content' was not found.
```

## Resilience Patterns

### Retry with Exponential Backoff

Automatically retries failed requests using Polly:
- Retry on transient HTTP errors (5xx, 408)
- Exponential backoff: 2^n seconds (2s, 4s, 8s...)
- Configurable max retries per provider

### Timeout Handling

- Per-provider timeout configuration
- Separate timeout for entire request pipeline
- Graceful timeout error responses

### Circuit Breaker (Optional)

Can be enabled by adding to `ServiceCollectionExtensions.cs`:
```csharp
.AddTransientHttpErrorPolicy(policyBuilder =>
    policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)))
```

## Thread Safety

- **Provider Caching**: Uses `ConcurrentDictionary` with atomic `GetOrAdd`
- **HTTP Clients**: Managed by `IHttpClientFactory` (thread-safe by design)
- **No Manual Locking**: Avoids potential deadlocks and performance issues

## Performance Optimization

- ✅ HTTP client reuse via `IHttpClientFactory`
- ✅ Connection pooling
- ✅ Provider instance caching
- ✅ Async/await throughout
- ✅ Minimal allocations in hot paths

## Security Best Practices

### API Key Management

**❌ Never commit API keys to source control!**

Use one of these methods:

1. **Environment Variables**
```bash
export LlmServiceSettings__Providers__OpenAI__Headers__Authorization="Bearer sk-..."
```

2. **User Secrets (Development)**
```bash
dotnet user-secrets set "LlmServiceSettings:Providers:OpenAI:Headers:Authorization" "Bearer sk-..."
```

3. **Azure Key Vault (Production)**
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

4. **AWS Secrets Manager**

### Additional Security

- ✅ Input validation and sanitization
- ✅ Request size limits (via data annotations)
- ⚠️ Add authentication/authorization for production
- ⚠️ Implement rate limiting
- ⚠️ Use HTTPS in production

## Development

### Project Structure

```
LLMService/
├── Controllers/
│   ├── LlmController.cs          # API endpoints
│   └── models/
│       └── InputModel.cs         # Request model
├── Services/
│   └── LLMService/
│       ├── LlmService.cs         # Main service
│       ├── HttpClientFactory/
│       │   ├── LlmHttpClientFactory.cs
│       │   └── LlmHttpClientFactoryAbstract.cs
│       ├── LllHttpClients/
│       │   ├── Abstractions/
│       │   │   ├── BaseLlmHttpClient.cs
│       │   │   └── ILlmHttpClient.cs
│       │   ├── ClaudeHttpClient.cs
│       │   ├── OpenAiHttpClient.cs
│       │   ├── GeminiHttpClient.cs
│       │   ├── GrokHttpClient.cs
│       │   └── MistralHttpClient.cs
│       ├── Extensions/
│       │   ├── ServiceCollectionExtensions.cs
│       │   └── models/
│       │       ├── LlmClientSettings.cs
│       │       └── LlmServiceSettings.cs
│       └── models/
│           └── LlmProvider.cs
├── appsettings.json
└── Program.cs
```

### Adding a New Provider

1. Add the provider to the `LlmProvider` enum:
```csharp
public enum LlmProvider
{
    OpenAI,
    Claude,
    Gemini,
    Mistral,
    Grok,
    YourNewProvider  // Add here
}
```

2. Create a new HTTP client class:
```csharp
public class YourProviderHttpClient : BaseLlmHttpClient
{
    protected override async Task<HttpResponseMessage> SendRequestAsync(string prompt, string model)
    {
        // Implement provider-specific request format
    }

    protected override async Task<string> ParseResponseAsync(HttpResponseMessage response)
    {
        // Implement provider-specific response parsing
    }

    protected override string GetProviderName() => "YourProvider";
}
```

3. Register in `LlmHttpClientFactory.cs`:
```csharp
return provider switch
{
    // ... existing providers
    LlmProvider.YourNewProvider => new YourProviderHttpClient(
        httpClient, 
        _loggerFactory.CreateLogger<YourProviderHttpClient>()),
    // ...
};
```

4. Add configuration to `appsettings.json`:
```json
{
  "LlmServiceSettings": {
    "Providers": {
      "YourNewProvider": {
        "BaseUrl": "https://api.yourprovider.com/v1/chat",
        "Headers": {
          "Authorization": "Bearer YOUR_API_KEY"
        },
        "TimeoutSeconds": 30,
        "MaxRetries": 3
      }
    }
  }
}
```

### Running Tests

```bash
dotnet test
```

### Building for Production

```bash
dotnet publish -c Release -o ./publish
```

## Monitoring & Observability

### Recommended Monitoring

1. **Application Insights** (Azure)
2. **Prometheus + Grafana**
3. **ELK Stack** (Elasticsearch, Logstash, Kibana)
4. **Datadog**

### Key Metrics to Track

- Request rate per provider
- Response times (p50, p95, p99)
- Error rates by provider and error type
- Retry counts
- Cache hit rate
- API cost tracking

### Health Checks

The `/api/Llm/Info` endpoint can be used for health checks:
```bash
curl https://localhost:5001/api/Llm/Info
```

## Troubleshooting

### Common Issues

#### 1. "Provider configuration error"
**Cause:** Provider not configured in appsettings.json  
**Solution:** Add provider configuration with BaseUrl, Headers, TimeoutSeconds, and MaxRetries

#### 2. "Failed to parse {Provider} response"
**Cause:** API response format changed  
**Solution:** Check error logs for raw JSON, update `ParseResponseAsync` method

#### 3. "Service temporarily unavailable"
**Cause:** Provider API is down or network issues  
**Solution:** Check provider status page, verify network connectivity

#### 4. "Request timed out"
**Cause:** Request exceeds configured timeout  
**Solution:** Increase `TimeoutSeconds` in configuration or optimize prompt

### Debug Mode

Enable detailed logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "LLMService": "Trace"
    }
  }
}
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Code Standards

- Follow C# coding conventions
- Add XML documentation for public APIs
- Include unit tests for new features
- Update README for significant changes

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- Resilience via [Polly](https://github.com/App-vNext/Polly)
- Logging with [Serilog](https://serilog.net/)
- Swagger UI via [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)

## Support

For questions, issues, or feature requests, please open an issue on GitHub.

## Roadmap

- [ ] Add response streaming support
- [ ] Implement response caching
- [ ] Add message history/conversation context
- [ ] Support for function calling
- [ ] Add request batching
- [ ] Implement cost tracking per provider
- [ ] Add authentication middleware
- [ ] Create Docker container
- [ ] Add Kubernetes deployment configs

---

**Made with ❤️ for the LLM developer community**
