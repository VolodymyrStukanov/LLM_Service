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
