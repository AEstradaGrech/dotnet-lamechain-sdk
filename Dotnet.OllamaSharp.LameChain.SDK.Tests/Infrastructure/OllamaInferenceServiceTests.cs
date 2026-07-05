using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Base;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Exceptions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Embedding;
using Dotnet.OllamaSharp.LameChain.SDK.Command.Core.Validators;
using Dotnet.OllamaSharp.LameChain.SDK.Commands.Request.Evaluators;
using System.Text.Json;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    public class OllamaInferenceServiceTests
    {
        private readonly Mock<IOllamaApiClient> _mockClient;
        private readonly Mock<IOptions<OllamaSettings>> _mockOptions;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly OllamaSettings _settings;
        private readonly OllamaInferenceService _service;

        public OllamaInferenceServiceTests()
        {
            _mockClient = new Mock<IOllamaApiClient>();
            _settings = new OllamaSettings { DefaultModel = "test-model" };
            _mockOptions = new Mock<IOptions<OllamaSettings>>();
            _mockOptions.Setup(o => o.Value).Returns(_settings);
            _mockConfig = new Mock<IConfiguration>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(_mockClient.Object);
            _service = new OllamaInferenceService(_mockClient.Object, _mockConfig.Object, _mockServiceProvider.Object, _mockOptions.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeService()
        {
            // Arrange
            var client = new Mock<IOllamaApiClient>();
            var options = new Mock<IOptions<OllamaSettings>>();
            var settings = new OllamaSettings();
            options.Setup(o => o.Value).Returns(settings);
            var config = new Mock<IConfiguration>();
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(sp => sp.GetService(typeof(IOllamaApiClient))).Returns(client.Object);

            // Act
            var service = new OllamaInferenceService(client.Object, config.Object, serviceProvider.Object, options.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region GeneratePrompt Tests

        [Fact]
        public async Task GeneratePrompt_WithValidRequest_ShouldReturnAggregatedMessage()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "Hello " },
                new GenerateResponseStream { Response = "World" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.GeneratePrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Hello World");
            result.Role.Should().Be(ChatRole.Assistant);
            request.Stream.Should().BeFalse();
        }

        [Fact]
        public async Task GeneratePrompt_WithEmptyResponses_ShouldReturnEmptyMessage()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>();

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.GeneratePrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be(string.Empty);
            result.Role.Should().Be(ChatRole.Assistant);
        }

        [Fact]
        public async Task GeneratePrompt_WithNullAndEmptyResponses_ShouldFilterThem()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream?>
            {
                null,
                new GenerateResponseStream { Response = "Valid" },
                new GenerateResponseStream { Response = "" },
                new GenerateResponseStream { Response = null },
                new GenerateResponseStream { Response = " " },
                new GenerateResponseStream { Response = "Response" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.GeneratePrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Valid Response");
            request.Stream.Should().BeFalse();
        }

        [Fact]
        public async Task GeneratePrompt_WithSingleResponse_ShouldReturnMessage()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "Single response" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.GeneratePrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Single response");
        }

        [Fact]
        public async Task GeneratePrompt_SetsStreamToFalse()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt", Stream = true };
            var responses = new List<GenerateResponseStream>();

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.GeneratePrompt(request, "ollama");

            // Assert
            request.Stream.Should().BeFalse();
        }

        #endregion

        #region ChatPrompt Tests

        [Fact]
        public async Task ChatPrompt_WithValidRequest_ShouldReturnAggregatedMessage()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "Hello " } },
                new ChatResponseStream { Message = new Message { Content = "World" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.ChatPrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Hello World");
            result.Role.Should().Be(ChatRole.Assistant);
        }

       

        [Fact]
        public async Task ChatPrompt_WithNullAndEmptyContent_ShouldFilterThem()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream?>
            {
                null,
                new ChatResponseStream { Message = new Message { Content = "Valid" } },
                new ChatResponseStream { Message = new Message { Content = "" } },
                new ChatResponseStream { Message = new Message { Content = "   " } },
                new ChatResponseStream { Message = new Message { Content = "Content" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.ChatPrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Valid   Content");
        }

        [Fact]
        public async Task ChatPrompt_WithSingleContent_ShouldReturnMessage()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "Single content" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.ChatPrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be("Single content");
        }

        [Fact]
        public async Task ChatPrompt_WithMultipleContentParts_ShouldConcatenateAll()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "Part1" } },
                new ChatResponseStream { Message = new Message { Content = "Part2" } },
                new ChatResponseStream { Message = new Message { Content = "Part3" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.ChatPrompt(request, "ollama");

            // Assert
            result.Content.Should().Be("Part1Part2Part3");
        }

        [Fact]
        public async Task ChatPrompt_WithEmptyResponses_ShouldReturnEmptyMessage()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>();

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.ChatPrompt(request, "ollama");

            // Assert
            result.Should().NotBeNull();
            result.Content.Should().Be(string.Empty);
            result.Role.Should().Be(ChatRole.Assistant);
        }

        #endregion

        #region GeneratePromptStream Tests

        [Fact]
        public async Task GeneratePromptStream_ShouldSetStreamTrue()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt", Stream = false };
            var responses = new List<GenerateResponseStream>();

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.GeneratePromptStream(request);

            // Assert
            request.Stream.Should().BeTrue();
        }

        [Fact]
        public async Task GeneratePromptStream_ShouldReturnClientAsyncEnumerable()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "Stream1" },
                new GenerateResponseStream { Response = "Stream2" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.GeneratePromptStream(request);
            var items = new List<GenerateResponseStream?>();
            await foreach (var item in result)
            {
                items.Add(item);
            }

            // Assert
            items.Should().HaveCount(2);
            items[0]?.Response.Should().Be("Stream1");
            items[1]?.Response.Should().Be("Stream2");
        }

        [Fact]
        public async Task GeneratePromptStream_WithEmptyResponse_ShouldReturnEmpty()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>();

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.GeneratePromptStream(request);
            var items = new List<GenerateResponseStream?>();
            await foreach (var item in result)
            {
                items.Add(item);
            }

            // Assert
            items.Should().BeEmpty();
        }

        #endregion

        #region ChatPromptStream Tests

        [Fact]
        public async Task ChatPromptStream_ShouldSetStreamTrue()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model", Stream = false };
            var responses = new List<ChatResponseStream>();

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.ChatPromptStream(request);

            // Assert
            request.Stream.Should().BeTrue();
        }

        [Fact]
        public async Task ChatPromptStream_ShouldReturnClientAsyncEnumerable()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "Stream1" } },
                new ChatResponseStream { Message = new Message { Content = "Stream2" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.ChatPromptStream(request);
            var items = new List<ChatResponseStream?>();
            await foreach (var item in result)
            {
                items.Add(item);
            }

            // Assert
            items.Should().HaveCount(2);
            items[0]?.Message.Content.Should().Be("Stream1");
            items[1]?.Message.Content.Should().Be("Stream2");
        }

        [Fact]
        public async Task ChatPromptStream_WithEmptyResponse_ShouldReturnEmpty()
        {
            // Arrange
            var request = new ChatRequest { Model = "test-model" };
            var responses = new List<ChatResponseStream>();

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = _service.ChatPromptStream(request);
            var items = new List<ChatResponseStream?>();
            await foreach (var item in result)
            {
                items.Add(item);
            }

            // Assert
            items.Should().BeEmpty();
        }

        #endregion

        #region GetEmbeddings Tests

        [Fact]
        public async Task GetEmbeddings_WithValidRequest_ShouldCallClientEmbedAsyncAndReturnResponse()
        {
            // Arrange
            var request = new EmbedRequest();
            var expectedResponse = new EmbedResponse();

            _mockClient
                .Setup(c => c.EmbedAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _service.GetEmbeddings(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(expectedResponse);
            _mockClient.Verify(c => c.EmbedAsync(request), Times.Once);
        }

        #endregion

        #region StructuredPrompt Tests

        [Fact]
        public async Task StructuredPrompt_WithValidPromptAndModel_ShouldReturnDeserializedObject()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "test-model";
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{\"name\": \"John\"}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama");

            // Assert
            result.Should().NotBeNull();
            _mockClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Once);
        }

        [Fact]
        public async Task StructuredPrompt_WithNullPromptAndSystemGuidance_ShouldThrowInvalidDataException()
        {
            // Arrange
            string? prompt = null;
            string? systemGuidance = null;

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                _service.StructuredPrompt<TestStructuredOutput>(prompt!, "model", "ollama", systemGuidance));
        }

        [Fact]
        public async Task StructuredPrompt_WithEmptyPromptAndSystemGuidance_ShouldThrowInvalidDataException()
        {
            // Arrange
            var prompt = "";
            var systemGuidance = "";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                _service.StructuredPrompt<TestStructuredOutput>(prompt, "model", "ollama", systemGuidance));
        }

        [Fact]
        public async Task StructuredPrompt_WithValidPromptButNullModel_ShouldUseDefaultModel()
        {
            // Arrange
            var prompt = "test prompt";
            string? model = null;
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.StructuredPrompt<TestStructuredOutput>(prompt, model!, "ollama");

            // Assert
            _mockClient.Verify(c => c.ChatAsync(It.Is<ChatRequest>(r => r.Model == _settings.DefaultModel)), Times.Once);
        }

        [Fact]
        public async Task StructuredPrompt_WithSystemGuidance_ShouldIncludeSystemMessage()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "test-model";
            var systemGuidance = "system guidance";
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama", systemGuidance);

            // Assert
            _mockClient.Verify(c => c.ChatAsync(It.Is<ChatRequest>(r =>
                r.Messages != null && r.Messages.Any(m => m.Role == ChatRole.System && m.Content != null && m.Content.Contains(systemGuidance)))), Times.Once);
        }

        [Fact]
        public async Task StructuredPrompt_WithMultipleResponses_ShouldConcatenateContent()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "test-model";
            var responses = new List<ChatResponseStream?>
            {
                new ChatResponseStream { Message = new Message { Content = "{" } },
                new ChatResponseStream { Message = new Message { Content = "\"name\": " } },
                new ChatResponseStream { Message = new Message { Content = "\"John\"}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task StructuredPrompt_WithNullAndEmptyResponses_ShouldSkipThem()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "test-model";
            var responses = new List<ChatResponseStream?>
            {
                null,
                new ChatResponseStream { Message = new Message { Content = "" } },
                new ChatResponseStream { Message = new Message { Content = "{" } },
                null,
                new ChatResponseStream { Message = new Message { Content = "}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task StructuredPrompt_ShouldSetStreamToFalse()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "test-model";
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama");

            // Assert
            _mockClient.Verify(c => c.ChatAsync(It.Is<ChatRequest>(r => r.Stream == false)), Times.Once);
        }

        [Fact]
        public async Task StructuredPrompt_WithCustomOptions_ShouldUseProvidedOptions()
        {
            // Arrange
            var prompt = "test prompt";
            var model = "ollama";
            var customOptions = new RequestOptions();
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.StructuredPrompt<TestStructuredOutput>(prompt, model, "ollama", null, customOptions);

            // Assert
            _mockClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Once);
        }

        #endregion

        #region CommandPrompt with GenerateRequest Tests

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithValidRequest_ShouldReturnDeserializedObject()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{\"name\": \"John\"}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(request);

            // Assert
            result.Should().NotBeNull();
            _mockClient.Verify(c => c.GenerateAsync(It.IsAny<GenerateRequest>()), Times.Once);
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithNullPrompt_ShouldThrowInvalidDataException()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = string.Empty };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                _service.CommandPrompt<TestStructuredOutput>(request));
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithoutValidations_ShouldNotCallValidator()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(request, new CommandPromptValidation<TestStructuredOutput> { Validations = 0, Validator = mockValidator.Object });

            // Assert
            mockValidator.Verify(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()), Times.Never);
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithValidationsAndValidator_ShouldCallValidator()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();
            var mockValidatorResponse = new TestStructuredOutput();
            mockValidator
                .Setup(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()))
                .ReturnsAsync(mockValidatorResponse);

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(request, new CommandPromptValidation<TestStructuredOutput> { Validations = 1, Validator = mockValidator.Object });

            // Assert
            result.Should().NotBeNull();
            mockValidator.Verify(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()), Times.Once);
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithStructuredOutputAndWithJsonInfo_ShouldAppendStructuredMessage()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(request, withJsonInfo: true);

            // Assert
            _mockClient.Verify(c => c.GenerateAsync(It.Is<GenerateRequest>(r =>
                r.System != null)), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithoutJsonInfo_ShouldNotModifySystem()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt", System = "original system" };
            var originalSystem = request.System;
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(request, withJsonInfo: false);

            // Assert
            request.System.Should().Be(originalSystem);
        }


        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithGeneralExceptionOnFirstIteration_ShouldRetry()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var callCount = 0;

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new PromptRetryException("error", callCount);
                    }
                    return GetAsyncEnumerable(new List<GenerateResponseStream>
                    {
                        new GenerateResponseStream { Response = "{}" }
                    });
                });

            // Act & Assert
            await Assert.ThrowsAsync<PromptRetryException>(() =>
                _service.CommandPrompt<TestStructuredOutput>(request, new CommandPromptValidation<TestStructuredOutput> { Validations = 1 }));
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithPromptRetryExceptionOnFirstIteration_ShouldRethrow()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(() =>
                {
                    throw new PromptRetryException("already retried", 2);
                });

            // Act & Assert
            await Assert.ThrowsAsync<PromptRetryException>(() =>
                _service.CommandPrompt<TestStructuredOutput>(request, new CommandPromptValidation<TestStructuredOutput> { Validations = 1 }));
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithMultipleResponses_ShouldConcatenate()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream?>
            {
                new GenerateResponseStream { Response = "{" },
                new GenerateResponseStream { Response = "\"name\": \"John\"" },
                new GenerateResponseStream { Response = "}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(request);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_WithNullAndEmptyResponses_ShouldSkip()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt" };
            var responses = new List<GenerateResponseStream?>
            {
                null,
                new GenerateResponseStream { Response = "" },
                new GenerateResponseStream { Response = "{" },
                null,
                new GenerateResponseStream { Response = "}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(request);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task CommandPrompt_WithGenerateRequest_ShouldSetStreamToFalse()
        {
            // Arrange
            var request = new GenerateRequest { Prompt = "test prompt", Stream = true };
            var responses = new List<GenerateResponseStream>
            {
                new GenerateResponseStream { Response = "{}" }
            };

            _mockClient
                .Setup(c => c.GenerateAsync(It.IsAny<GenerateRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(request);

            // Assert
            request.Stream.Should().BeFalse();
        }

        #endregion

        #region CommandPrompt with ChatRequest Tests

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithValidRequest_ShouldReturnDeserializedObject()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{\"name\": \"John\"}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(chatRequest);

            // Assert
            result.Should().NotBeNull();
            _mockClient.Verify(c => c.ChatAsync(It.IsAny<ChatRequest>()), Times.Once);
        }

       
        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithoutValidations_ShouldNotCallValidator()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 0, Validator = mockValidator.Object });

            // Assert
            mockValidator.Verify(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()), Times.Never);
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithValidationsAndValidator_ShouldCallValidator()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();
            var mockValidatorResponse = new TestStructuredOutput();
            mockValidator
                .Setup(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()))
                .ReturnsAsync(mockValidatorResponse);

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 1, Validator = mockValidator.Object });

            // Assert
            result.Should().NotBeNull();
            mockValidator.Verify(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()), Times.Once);
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithStructuredOutputAndWithJsonInfo_ShouldAppendToSystemMessage()
        {
            // Arrange
            var systemContent = "original system";
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = systemContent },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest, withJsonInfo: true);

            // Assert
            var systemMessage = chatRequest.Messages.First(m => m.Role == ChatRole.System);
            systemMessage.Content.Should().Contain(systemContent);
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithoutJsonInfo_ShouldNotModifySystemMessage()
        {
            // Arrange
            var systemContent = "original system";
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = systemContent },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest, withJsonInfo: false);

            // Assert
            var systemMessage = chatRequest.Messages.First(m => m.Role == ChatRole.System);
            systemMessage.Content.Should().Be(systemContent);
        }

       

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithGeneralExceptionOnFirstIteration_ShouldRetry()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var callCount = 0;

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new PromptRetryException("error", callCount);
                    }
                    return GetAsyncEnumerable(new List<ChatResponseStream>
                    {
                        new ChatResponseStream { Message = new Message { Content = "{}" } }
                    });
                });

            // Act & Assert
            await Assert.ThrowsAsync<PromptRetryException>(() =>
                _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 1 }));
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithPromptRetryExceptionOnFirstIteration_ShouldRethrow()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(() =>
                {
                    throw new PromptRetryException("already retried", 2);
                });

            // Act & Assert
            await Assert.ThrowsAsync<PromptRetryException>(() =>
                _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 1 }));
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithMultipleResponses_ShouldConcatenate()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream?>
            {
                new ChatResponseStream { Message = new Message { Content = "{" } },
                new ChatResponseStream { Message = new Message { Content = "\"name\": \"John\"" } },
                new ChatResponseStream { Message = new Message { Content = "}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(chatRequest);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_WithNullAndEmptyResponses_ShouldSkip()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream?>
            {
                null,
                new ChatResponseStream { Message = new Message { Content = "" } },
                new ChatResponseStream { Message = new Message { Content = "{" } },
                null,
                new ChatResponseStream { Message = new Message { Content = "}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerableNullable(responses));

            // Act
            var result = await _service.CommandPrompt<TestStructuredOutput>(chatRequest);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_ShouldSetStreamToFalse()
        {
            // Arrange
            var chatRequest = new ChatRequest
            {
                Stream = true,
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest);

            // Assert
            chatRequest.Stream.Should().BeFalse();
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_ShouldPassUserMessageToValidator()
        {
            // Arrange
            var userMessage = "test user prompt";
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = "system" },
                    new Message { Role = ChatRole.User, Content = userMessage }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();
            mockValidator
                .Setup(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()))
                .ReturnsAsync(new TestStructuredOutput());

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 1, Validator = mockValidator.Object });

            // Assert
            mockValidator.Verify(v => v.Prompt(It.Is<JsonRefineRequest<TestStructuredOutput>>(r =>
                r.ValidatedPrompt == userMessage)), Times.Once);
        }

        [Fact]
        public async Task CommandPrompt_WithChatRequest_ShouldPassSystemMessageToValidator()
        {
            // Arrange
            var systemMessage = "test system message";
            var chatRequest = new ChatRequest
            {
                Messages = new[]
                {
                    new Message { Role = ChatRole.System, Content = systemMessage },
                    new Message { Role = ChatRole.User, Content = "user prompt" }
                }
            };
            var responses = new List<ChatResponseStream>
            {
                new ChatResponseStream { Message = new Message { Content = "{}" } }
            };

            _mockClient
                .Setup(c => c.ChatAsync(It.IsAny<ChatRequest>()))
                .Returns(GetAsyncEnumerable(responses));

            var mockValidator = new Mock<JsonOutputRefinerCommand<TestStructuredOutput>>();
            mockValidator
                .Setup(v => v.Prompt(It.IsAny<JsonRefineRequest<TestStructuredOutput>>()))
                .ReturnsAsync(new TestStructuredOutput());

            // Act
            await _service.CommandPrompt<TestStructuredOutput>(chatRequest, new CommandPromptValidation<TestStructuredOutput> { Validations = 1, Validator = mockValidator.Object });

            // Assert
            mockValidator.Verify(v => v.Prompt(It.Is<JsonRefineRequest<TestStructuredOutput>>(r =>
                r.SystemMessage.Contains(systemMessage))), Times.Once);
        }

        #endregion

        #region Helper Methods

        private IAsyncEnumerable<T> GetAsyncEnumerable<T>(List<T> items)
        {
            return GetAsyncEnumerableIterator(items);
        }

        private async IAsyncEnumerable<T> GetAsyncEnumerableIterator<T>(List<T> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }

        private IAsyncEnumerable<T?> GetAsyncEnumerableNullable<T>(List<T?> items)
        {
            return GetAsyncEnumerableNullableIterator(items);
        }

        private async IAsyncEnumerable<T?> GetAsyncEnumerableNullableIterator<T>(List<T?> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }

        #endregion
    }
}

public class TestStructuredOutput : StructuredOutput
{
}
