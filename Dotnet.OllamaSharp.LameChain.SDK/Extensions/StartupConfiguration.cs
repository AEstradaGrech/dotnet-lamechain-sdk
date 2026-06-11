using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using DotnetLlamaSharp.Services.Embeddings;
using DotnetLlamaSharp.Services.Prompting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using static OllamaSharp.OllamaApiClient;


namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions
{
    public static class StartupConfiguration
    {
        //Register Services etc
        public static IServiceCollection ConfigureOllamaSettings(this IServiceCollection services, IConfiguration appConfig)
            => services.Configure<OllamaSettings>(appConfig.GetSection(nameof(OllamaSettings)));
        
        public static IServiceCollection AddOllamaSharpApiClient(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch
            {
                ServiceLifetime.Scoped => services.AddScoped<IOllamaApiClient, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(config.OllamaUrl),
                        Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes) // Set your desired timeout here
                    };
                    return new OllamaApiClient(httpClient, config.DefaultModel);
                }),
                ServiceLifetime.Transient => services.AddTransient<IOllamaApiClient, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(config.OllamaUrl),
                        Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes)
                    };
                    return new OllamaApiClient(httpClient, config.DefaultModel);
                }),
                ServiceLifetime.Singleton => services.AddSingleton<IOllamaApiClient, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(config.OllamaUrl),
                        Timeout = TimeSpan.FromMinutes(config.TimeoutMinutes)
                    };
                    return new OllamaApiClient(httpClient, config.DefaultModel);
                }),
            };

        public static IServiceCollection AddOllamaEmbeddingsGenerator(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch
            {
                ServiceLifetime.Scoped => services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.EmbeddingModels.FirstOrDefault() ?? config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                }),
                ServiceLifetime.Transient => services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.EmbeddingModels.FirstOrDefault() ?? config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                }),
                ServiceLifetime.Singleton => services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OllamaApiClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.EmbeddingModels.FirstOrDefault() ?? config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                })
            };


        // IChatClient is for Microsoft.Extensions.AI, IOllamaApiClient is for OllamaSharp, you can register both if you want to use them side by side
        public static IServiceCollection AddOllamaIChatClient(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch
            {
                ServiceLifetime.Scoped => services.AddScoped<IChatClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                }),
                ServiceLifetime.Transient => services.AddTransient<IChatClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                }),
                ServiceLifetime.Singleton => services.AddSingleton<IChatClient>(sp =>
                {
                    var config = sp.GetRequiredService<IOptions<OllamaSettings>>().Value;
                    var settings = new Configuration
                    {
                        Uri = new Uri(config.OllamaUrl),
                        Model = config.DefaultModel
                    };
                    return new OllamaApiClient(settings);
                })
            };

        public static IServiceCollection AddLameChainServices(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime)
        {
            //Register Infra services
            //Register services
            switch (lifetime)
            {
                case (ServiceLifetime.Transient):
                    services.AddTransient<IPromptCommandsFactory, PromptCommandsFactory>();
                    services.AddTransient<IPromptCommandsService, PromptCommandsService>();
                    services.AddTransient<IOllamaInferenceService, OllamaInferenceService>();
                    services.AddTransient<IOllamaStreamService, OllamaStreamService>();
                    services.AddTransient<IEmbeddingsService, EmbeddingsService>();
                    break;

                case (ServiceLifetime.Scoped):
                default:
                    services.AddScoped<IPromptCommandsFactory, PromptCommandsFactory>();
                    services.AddScoped<IPromptCommandsService, PromptCommandsService>();
                    services.AddScoped<IOllamaInferenceService, OllamaInferenceService>();
                    services.AddScoped<IOllamaStreamService, OllamaStreamService>();
                    services.AddScoped<IEmbeddingsService, EmbeddingsService>();
                    break;

            }
            return services;
        }

        public static IServiceCollection ConfigureLameChain(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => services.ConfigureOllamaSettings(appConfig)
                       .AddOllamaSharpApiClient(appConfig, lifetime)
                       .AddOllamaEmbeddingsGenerator(appConfig, lifetime)
                       .AddLameChainServices(appConfig, lifetime);
        
    }
}
