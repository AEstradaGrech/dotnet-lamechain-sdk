using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Request;
using Microsoft.Extensions.AI;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using OllamaMessage = OllamaSharp.Models.Chat.Message;
using AIRole = Microsoft.Extensions.AI.ChatRole;
using OllamaRole = OllamaSharp.Models.Chat.ChatRole;
using System.Text.Json.Nodes;
using System.Text.Json;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Anthropic.Models.Messages;

namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions.Model
{
    public static class OllamaRequestExtensions
    {
        public static ChatOptions ToClaudeChatClientRequest(this ChatRequest ollamaRequest, IList<AITool>? tools, bool allowParallelToolCall = false, Effort reasoningEffort = Effort.Medium)
        {
            if (ollamaRequest.Messages.Count() == 0)
                throw new InvalidOperationException($"{nameof(ChatRequest)}.{nameof(ToClaudeChatClientRequest)} >> No messages present in the request");

            if (ollamaRequest.Options == null)
                ollamaRequest.Options = new RequestOptions();
            
            var chatRequest = new ChatOptions
            {
                ModelId = ollamaRequest.Model,
                ToolMode = tools != null ? new AutoChatToolMode() : null,
                MaxOutputTokens = ollamaRequest.Options.NumPredict,
                TopP = ollamaRequest.Model.Contains("opus") ? null : ollamaRequest.Options.TopP,
                TopK = ollamaRequest.Model.Contains("opus") ? null : ollamaRequest.Options.TopK,
                AllowMultipleToolCalls = allowParallelToolCall,
                FrequencyPenalty = ollamaRequest.Options.FrequencyPenalty,
                PresencePenalty = ollamaRequest.Options.PresencePenalty,
                StopSequences = ollamaRequest.Options.Stop,
                ResponseFormat = ollamaRequest.Format != null ? new ChatResponseFormatJson(JsonSerializer.SerializeToElement(ollamaRequest.Format)) : new ChatResponseFormatText(),
                
                Tools = tools
            };

            if(ollamaRequest.Think == true)
            {
                chatRequest.TopK = null;
                chatRequest.TopP = null;

                switch (reasoningEffort)
                {
                    case (Effort.Low):
                        chatRequest.MaxOutputTokens = ((int)EReasoningMinTokens.Low) + ollamaRequest.Options.NumPredict;
                        break;

                    case (Effort.Medium):
                        chatRequest.MaxOutputTokens = ((int)EReasoningMinTokens.Medium) + ollamaRequest.Options.NumPredict;
                        break;

                    case (Effort.High):
                    default:
                        chatRequest.MaxOutputTokens = ((int)EReasoningMinTokens.High) + ollamaRequest.Options.NumPredict;
                        break;
                }

                chatRequest.RawRepresentationFactory = _ => new MessageCreateParams
                {
                    Model = ollamaRequest.Model,                       
                    MaxTokens = (long)chatRequest.MaxOutputTokens,  
                    Messages = [],                                        
                    Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },
                    OutputConfig = new OutputConfig { Effort = reasoningEffort }
                };
            }

            return chatRequest;
        }

        public static GroqChatRequest AsGroqRequest(this ChatRequest req, string? reasoningEffort = null)
        {
            if (req.Messages.Count() == 0)
                throw new InvalidOperationException($"{nameof(ChatRequest)}.{nameof(AsGroqRequest)} >> No messages present in the request");

            if (req.Options == null)
                req.Options = new RequestOptions();

            var request = new GroqChatRequest
            {
                Messages = req.Messages.ToList(),
                Model = req.Model,
                Temperature = req.Options.Temperature ?? .7f,
                FrequencyPenalty = req.Options.FrequencyPenalty = 0,
                PresencePenalty = req.Options.PresencePenalty ?? .0f,
                MaxCompletionTokens = req.Options.NumPredict,
                TopP = req.Options.TopP ?? 1.0f,
                Stream = req.Stream,
                Stop = req.Options.Stop != null && req.Options.Stop.Length > 0 ? req.Options.Stop.ToList() : null,
                IncludeReasoning = req.Think == true ? true : null,
                ReasoningEffort = req.Think == true ? string.IsNullOrEmpty(reasoningEffort) ? "medium" : reasoningEffort : null,
                Tools = req.Tools != null && req.Tools.Count() > 0 ? req.Tools.ToList() : null,
                ToolChoice = req.Tools != null && req.Tools.Count() > 0 ? "auto" : "none",
                ParallelToolCalls = false,
                ResponseFormat = req.Format == null ? null :
                new {
                    type = "json_schema",
                    json_schema = new {
                        name = "response",
                        schema = req.Format,
                        strict = true
                    }
                }
            };

            return request;
        }

        public static ChatRequest GenerateRequestToChat(this GenerateRequest req)
        {
            if (string.IsNullOrEmpty(req.System) && string.IsNullOrEmpty(req.Prompt))
                throw new InvalidOperationException($"{nameof(GenerateRequest)}.{nameof(GenerateRequestToChat)} >> No system or user prompt found in request");

            ChatRequest chatRequest = new ChatRequest 
            { 
                Model = req.Model,
                Options = req.Options,
                KeepAlive = req.KeepAlive,
                Template = req.Template,
                Format = req.Format,
                Stream = req.Stream,
                Think = req.Think,
                Messages = []
            };

            var messages = new List<OllamaMessage>();

            if(!string.IsNullOrEmpty(req.System))
                messages.Add(new OllamaMessage(OllamaRole.System, req.System));

            if(!string.IsNullOrEmpty(req.Prompt))
                messages.Add(new OllamaMessage(OllamaRole.User, req.Prompt));

            chatRequest.Messages = messages;

            return chatRequest;
        }

        // ------------------------------------------------------------------
        // Microsoft.Extensions.AI  <->  OllamaSharp  message mapping
        // Used by ClaudeHandler to keep the conversation history in the Ollama
        // model while talking to Claude through IChatClient.
        // ------------------------------------------------------------------

        /// <summary>
        /// Maps a Microsoft.Extensions.AI <see cref="ChatMessage"/> (e.g. the assistant
        /// tool-call turn returned by IChatClient) to an OllamaSharp <see cref="OllamaMessage"/>.
        /// The tool-call id is preserved on <c>ToolCall.Id</c> so it can be paired again later.
        /// </summary>
        public static OllamaMessage ToOllamaMessage(this ChatMessage aiMessage)
        {
            var ollamaMessage = new OllamaMessage
            {
                Role = aiMessage.Role.ToOllamaRole(),
                Content = string.Concat(aiMessage.Contents.OfType<TextContent>().Select(t => t.Text))
            };

            // Reasoning/thinking is captured for display only. NOTE: the Ollama model has no slot
            // for Claude's thinking *signature* (TextReasoningContent.ProtectedData), so it is NOT
            // round-tripped back to Claude (an unsigned thinking block is rejected when thinking is on).
            var reasoning = aiMessage.Contents.OfType<TextReasoningContent>().FirstOrDefault();
            if (reasoning != null)
                ollamaMessage.Thinking = reasoning.Text;

            // Assistant tool-call blocks -> Ollama ToolCalls (CallId kept on ToolCall.Id)
            var functionCalls = aiMessage.Contents.OfType<FunctionCallContent>().ToList();
            if (functionCalls.Count > 0)
            {
                ollamaMessage.ToolCalls = functionCalls
                    .Select(fc => new OllamaMessage.ToolCall
                    {
                        Id = fc.CallId,
                        Function = new OllamaMessage.Function
                        {
                            Name = fc.Name,
                            Arguments = fc.Arguments
                        }
                    })
                    .ToList();
            }

            // Tool-result block -> serialized result in Content. FunctionResultContent carries the
            // CallId (not the tool name), so ToolName can't be recovered here — the caller that
            // builds the tool message (getToolResponseMessages) sets ToolName explicitly.
            var result = aiMessage.Contents.OfType<FunctionResultContent>().FirstOrDefault();
            if (result != null)
                ollamaMessage.Content = result.Result?.ToString() ?? string.Empty;

            return ollamaMessage;
        }

        /// <summary>
        /// Maps a single OllamaSharp <see cref="OllamaMessage"/> back to a
        /// Microsoft.Extensions.AI <see cref="ChatMessage"/>.
        /// For a *tool* message the CallId cannot be recovered from a lone message — use
        /// <see cref="ToChatMessages"/> to map a whole history so the result is paired with the
        /// preceding tool-call id (Claude matches tool_result to tool_use by id).
        /// </summary>
        public static ChatMessage ToChatMessage(this OllamaMessage ollamaMessage)
        {
            var contents = new List<AIContent>();

            // Tool-result turn. CallId is unknown at single-message scope; fall back to ToolName.
            if (ollamaMessage.Role == OllamaRole.Tool)
            {
                contents.Add(new FunctionResultContent(ollamaMessage.ToolName ?? string.Empty, ollamaMessage.Content));
                return new ChatMessage(AIRole.Tool, contents);
            }

            // Assistant tool-call turn -> FunctionCallContent (CallId restored from ToolCall.Id)
            if (ollamaMessage.ToolCalls != null && ollamaMessage.ToolCalls.Any())
            {
                foreach (var toolCall in ollamaMessage.ToolCalls)
                    contents.Add(new FunctionCallContent(toolCall.Id, toolCall.Function.Name, toolCall.Function.Arguments));
            }

            // restore thinking content
            if(!string.IsNullOrEmpty(ollamaMessage.Thinking))
                contents.Add(new TextReasoningContent(ollamaMessage.Thinking));
            
            // Plain text (skip thinking on the way out — see ToOllamaMessage note about signatures)
            if (!string.IsNullOrEmpty(ollamaMessage.Content))
                contents.Add(new TextContent(ollamaMessage.Content));

            return new ChatMessage(ollamaMessage.Role.ToAIRole(), contents);
        }

        /// <summary>
        /// Maps a full Ollama message history to Microsoft.Extensions.AI messages, threading each
        /// tool-call id onto the following tool-result so Claude can pair them. This is the method
        /// the handler should use to build the outgoing message list for IChatClient.
        /// </summary>
        public static IEnumerable<ChatMessage> ToChatMessages(this IEnumerable<OllamaMessage> messages)
        {
            string lastToolCallId = null;

            foreach (var message in messages)
            {
                if (message.Role == OllamaRole.Tool)
                {
                    // reuse the id emitted by the immediately-preceding assistant tool call
                    yield return new ChatMessage(
                        AIRole.Tool,
                        new List<AIContent> { new FunctionResultContent(lastToolCallId ?? message.ToolName ?? string.Empty, message.Content) });
                    continue;
                }

                var chatMessage = message.ToChatMessage();

                var call = chatMessage.Contents.OfType<FunctionCallContent>().FirstOrDefault();
                if (call != null)
                    lastToolCallId = call.CallId;

                yield return chatMessage;
            }
        }

        private static OllamaRole ToOllamaRole(this AIRole role)
            => role == AIRole.System ? OllamaRole.System
             : role == AIRole.Assistant ? OllamaRole.Assistant
             : role == AIRole.Tool ? OllamaRole.Tool
             : OllamaRole.User;

        private static AIRole ToAIRole(this OllamaRole? role)
            => role == OllamaRole.System ? AIRole.System
             : role == OllamaRole.Assistant ? AIRole.Assistant
             : role == OllamaRole.Tool ? AIRole.Tool
             : AIRole.User;
    }
}
