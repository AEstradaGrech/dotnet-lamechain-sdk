

using OllamaSharp.Models.Chat;


namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request
{
    /// <summary>
    /// messages
    /// array
    /// Required
    /// A list of messages comprising the conversation so far.

    /// model
    /// string
    /// Required
    /// ID of the model to use.For details on which models are compatible with the Chat API, see available models
    /// 
    /// citation_options
    /// string or null
    /// Optional
    /// Defaults to enabled
    /// Allowed values: enabled, disabled
    /// Whether to enable citations in the response. When enabled, the model will include citations for information retrieved from provided documents or web searches.
    /// 
    /// compound_custom
    /// object or null
    /// Optional
    /// Custom configuration of models and tools for Compound.
    /// 

    /// disable_tool_validation
    /// boolean
    /// Optional
    /// Defaults to false
    /// If set to true, groq will return called tools without validating that the tool is present in request.tools.tool_choice= required / none will still be enforced, but the request cannot require a specific tool be used.
    /// 
    /// documents
    /// array or null
    /// Optional
    /// A list of documents to provide context for the conversation. Each document contains text that can be referenced by the model.
    /// 

    /// frequency_penalty
    /// number or null
    /// Optional
    /// Defaults to 0
    /// Range: -2 - 2
    /// This is not yet supported by any of our models. Number between -2.0 and 2.0. Positive values penalize new tokens based on their existing frequency in the text so far, decreasing the model's likelihood to repeat the same line verbatim.

    /// include_reasoning
    /// boolean or null
    /// Optional
    /// Whether to include reasoning in the response. If true, the response will include a reasoning field.If false, the model's reasoning will not be included in the response. This field is mutually exclusive with reasoning_format.

    /// max_completion_tokens
    /// integer or null
    /// Optional
    /// The maximum number of tokens that can be generated in the chat completion.The total length of input tokens and generated tokens is limited by the model's context length.

    /// n
    /// integer or null
    /// Optional
    /// Defaults to 1
    /// Range: 1 - 1
    /// How many chat completion choices to generate for each input message.Note that the current moment, only n = 1 is supported.Other values will result in a 400 response.
    /// 
    /// parallel_tool_calls
    /// boolean or null
    /// Optional
    /// Defaults to true
    /// Whether to enable parallel function calling during tool use.
    /// 
    /// presence_penalty
    /// number or null
    /// Optional
    /// Defaults to 0
    /// Range: -2 - 2
    /// This is not yet supported by any of our models. Number between -2.0 and 2.0. Positive values penalize new tokens based on whether they appear in the text so far, increasing the model's likelihood to talk about new topics.
    /// 
    /// reasoning_effort
    /// string or null
    /// Optional
    /// Allowed values: none, default, low, medium, high
    /// qwen3 models support the following values Set to 'none' to disable reasoning.Set to 'default' or null to let Qwen reason.
    /// 
    /// openai/gpt-oss-20b and openai/gpt-oss-120b support 'low', 'medium', or 'high'. 'medium' is the default value.
    /// 
    /// response_format
    /// object / object / object or null
    /// Optional
    /// An object specifying the format that the model must output.Setting to { "type": "json_schema", "json_schema": { ...} }
    ///     enables Structured Outputs which ensures the model will match your supplied JSON schema.json_schema response format is only available on supported models.Setting to { "type": "json_object" }
    ///     enables the older JSON mode, which ensures the message the model generates is valid JSON. Using json_schema is preferred for models that support it.
    /// 
    /// search_settings
    /// object or null
    /// Optional
    /// Settings for web search functionality when the model uses a web search tool.
    /// 

    /// seed
    /// integer or null
    /// Optional
    /// If specified, our system will make a best effort to sample deterministically, such that repeated requests with the same seed and parameters should return the same result.Determinism is not guaranteed, and you should refer to the system_fingerprint response parameter to monitor changes in the backend.
    /// 

    /// stop
    /// string / array or null
    /// Optional
    /// Up to 4 sequences where the API will stop generating further tokens.The returned text will not contain the stop sequence.
    /// 

    /// stream
    /// boolean or null
    /// Optional
    /// Defaults to false
    /// If set, partial message deltas will be sent.Tokens will be sent as data-only server-sent events as they become available, with the stream terminated by a data: [DONE] message.Example code.
    /// 

    /// temperature
    /// number or null
    /// Optional
    /// Defaults to 1
    /// Range: 0 - 2
    /// What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic.We generally recommend altering this or top_p but not both.
    /// 
    /// tool_choice
    /// string / object or null
    /// Optional
    /// Controls which (if any) tool is called by the model.none means the model will not call any tool and instead generates a message.auto means the model can pick between generating a message or calling one or more tools. required means the model must call one or more tools. Specifying a particular tool via { "type": "function", "function": { "name": "my_function"} }
    ///     forces the model to call that tool.
    /// 
    /// none is the default when no tools are present.auto is the default if tools are present.
    /// 
    /// tools
    /// array or null
    /// Optional
    /// A list of tools the model may call.Currently, only functions are supported as a tool.Use this to provide a list of functions the model may generate JSON inputs for. A max of 128 functions are supported.
    /// 

    /// top_p
    /// number or null
    /// Optional
    /// Defaults to 1
    /// Range: 0 - 1
    /// An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the tokens with top_p probability mass.So 0.1 means only the tokens comprising the top 10% probability mass are considered. We generally recommend altering this or temperature but not both.
    /// 
    /// user
    /// string or null
    /// Optional
    /// A unique identifier representing your end-user, which can help us monitor and detect abuse.
    /// </summary>
    public class GroqChatRequest
    {
        public GroqChatRequest()
        {
            // defaults per documented summaries
            CitationOptions = "disabled";
            DisableToolValidation = false;
            FrequencyPenalty = 0f;
            ParallelToolCalls = true;
            PresencePenalty = 0f;
            Temperature = 1f;
            TopP = 1f;
            Stream = false;
        }

        // Required
        public string? Model { get; set; }
        public List<Message>? Messages { get; set; }

        // RESPONSE JSON SCHEMA
        public object? ResponseFormat { get; set; }

        // Documented additional fields
        // Allowed values: "enabled", "disabled" (default "enabled")
        public string CitationOptions { get; set; }

        // Custom configuration object for Compound (optional)
        public object? CompoundCustom { get; set; }

        // Defaults to false
        public bool DisableToolValidation { get; set; }

        // A list of documents to provide context (optional)
        public List<object>? Documents { get; set; }

        // Defaults to 0. Range: -2 .. 2
        public float? FrequencyPenalty { get; set; }

        // Whether to include reasoning in the response (optional, mutually exclusive with reasoning_format)
        public bool? IncludeReasoning { get; set; }

        // The maximum number of completion tokens (optional)
        public int? MaxCompletionTokens { get; set; }

        // Defaults to true
        public bool ParallelToolCalls { get; set; }

        // Defaults to 0. Range: -2 .. 2
        public float? PresencePenalty { get; set; }

        // Allowed values: none, default, low, medium, high (optional)
        public string? ReasoningEffort { get; set; }

        // Settings for web search tools (optional)
        public object? SearchSettings { get; set; }

        // Optional seed for deterministic sampling
        public int? Seed { get; set; }

        // Up to 4 stop sequences (optional)
        public List<string>? Stop { get; set; }

        // Controls which (if any) tool is called. Can be string ("none","auto","required") or object specifying a tool.
        public object? ToolChoice { get; set; }

        // List of tools (functions) the model may call (optional)
        public List<object>? Tools { get; set; }

        // Sampling temperature. Defaults to 1. Range: 0 .. 2
        public float Temperature { get; set; }

        // Nucleus sampling. Defaults to 1. Range: 0 .. 1
        public float TopP { get; set; }

        // Optional end-user identifier
        public string? User { get; set; }

        // If true, stream partial deltas. Defaults to false
        public bool Stream { get; set; }
    }
}

