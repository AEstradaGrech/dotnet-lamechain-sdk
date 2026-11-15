using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Shared.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Clients;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Coding;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces.Service.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Scripting;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Utilities.Telemetry;
using Dotnet.OllamaSharp.LameChain.SDK.Interfaces.Command.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Services;
using Dotnet.OllamaSharp.LameChain.SDK.Services.Command;
using DotnetLlamaSharp.Domain.Services.Embeddings;
using DotnetLlamaSharp.Domain.Services.Inference;
using DotnetLlamaSharp.Infrastructure.Services.Inference;
using DotnetLlamaSharp.Services.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using System.Net.Http.Headers;
using System.Text;
using static OllamaSharp.OllamaApiClient;


namespace Dotnet.OllamaSharp.LameChain.SDK.Extensions
{
    public static class StartupConfiguration
    {
        //Register Services etc
        public static IServiceCollection ConfigureOllamaSettings(this IServiceCollection services, IConfiguration appConfig)
            => services.Configure<OllamaSettings>(appConfig.GetSection(nameof(OllamaSettings)));
        public static IServiceCollection ConfigureClaudeSettings(this IServiceCollection services, IConfiguration appConfig)
            => services.Configure<ClaudeSettings>(appConfig.GetSection(nameof(ClaudeSettings)));
        public static IServiceCollection ConfigureGroqSettings(this IServiceCollection services, IConfiguration appConfig)
            => services.Configure<GroqSettings>(appConfig.GetSection(nameof(GroqSettings)));

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

        public static IServiceCollection AddGroqApiClient(this IServiceCollection services, IConfiguration appConfig)
        {
            services.AddHttpClient<IGroqClient, GroqClient>(client =>
            {
                var cfg = appConfig.GetSection(nameof(GroqSettings)).Get<GroqSettings>();
                client.BaseAddress = new Uri(cfg.BaseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });

            return services;
        }
           
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

        public static IServiceCollection WithToolsFrom<TService>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped) where TService : ToolsService<TService>, new()
        {
            services.TryAddSingleton<IToolbox, Toolbox>();

            return lifetime switch
            { 
                ServiceLifetime.Scoped => services.AddScoped<IToolsService<TService>, TService>().AddSingleton(new ToolsServiceDescriptor(typeof(TService))),
                ServiceLifetime.Transient => services.AddTransient<IToolsService<TService>, TService>().AddSingleton(new ToolsServiceDescriptor(typeof(TService))),
                ServiceLifetime.Singleton => services.AddSingleton<IToolsService<TService>, TService>().AddSingleton(new ToolsServiceDescriptor(typeof(TService))),
                _ => services.AddScoped<IToolsService<TService>, TService>(),
            };
        }
        public static IServiceCollection ConfigureClaudeApiClient(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
           => lifetime switch {
               ServiceLifetime.Scoped => services.ConfigureClaudeSettings(appConfig).AddScoped<IClaudeClient, ClaudeClient>(),
               ServiceLifetime.Transient => services.ConfigureClaudeSettings(appConfig).AddTransient<IClaudeClient, ClaudeClient>(),
               ServiceLifetime.Singleton => services.ConfigureClaudeSettings(appConfig).AddSingleton<IClaudeClient, ClaudeClient>()
           };

        public static IServiceCollection AddLameChainServices(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime)
        {
            switch (lifetime)
            {
                case (ServiceLifetime.Transient):
                    services.AddTransient<IPromptCommandsFactory, PromptCommandsFactory>();
                    services.AddTransient<IPromptCommandsService, PromptCommandsService>();
                    services.AddTransient<ICommandSettingsService, CommandSettingsService>();
                    services.AddTransient<IOllamaInferenceService, OllamaInferenceService>();
                    services.AddTransient<IOllamaStreamService, OllamaStreamService>();
                    services.AddTransient<IAgentsFactory, AgentsFactory>();
                    services.AddTransient<IEmbeddingsService, EmbeddingsService>();
                    break;

                case (ServiceLifetime.Scoped):
                default:
                    services.AddScoped<IPromptCommandsFactory, PromptCommandsFactory>();
                    services.AddScoped<IPromptCommandsService, PromptCommandsService>();
                    services.AddScoped<ICommandSettingsService, CommandSettingsService>();
                    services.AddScoped<IOllamaInferenceService, OllamaInferenceService>();
                    services.AddScoped<IOllamaStreamService, OllamaStreamService>();
                    services.AddScoped<IAgentsFactory, AgentsFactory>();
                    services.AddScoped<IEmbeddingsService, EmbeddingsService>();
                    break;

            }
            return services;
        }

        public static IServiceCollection WithPythonExecutor(this IServiceCollection services, IConfiguration configuration, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var section = configuration.GetSection(nameof(PythonExecutionOptions));

            var options = section.Get<PythonExecutionOptions>()
                ?? throw new InvalidOperationException($"{nameof(WithPythonExecutor)} >> missing '{nameof(PythonExecutionOptions)}' configuration section");

            // Register the PythonExecutionOptions from configuration for DI (IOptions<PythonExecutionOptions>)
            services.Configure<PythonExecutionOptions>(section);

            // The execution mode is decided once, here: consumers only see IPythonExecutor
            if (options.UseUv)
            {
                // Installs the configured Python version, prunes the uv cache and warms up packages in background
                services.AddHostedService<PythonEnvService>();
                services.Add(new ServiceDescriptor(typeof(IPythonExecutor), typeof(PythonUvExecutor), lifetime));

                // Known malicious packages check (OSV.dev) before installing the dependencies
                if (options.CheckMaliciousPackages)
                {
                    services.AddHttpClient(OsvMaliciousPackageChecker.HttpClientName, client =>
                    {
                        client.BaseAddress = new Uri(options.OsvApiUrl.TrimEnd('/') + "/");
                        client.Timeout = TimeSpan.FromSeconds(20);
                    });

                    services.TryAddSingleton<IMaliciousPackageChecker, OsvMaliciousPackageChecker>();
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.PythonExe))
                    throw new InvalidOperationException($"{nameof(PythonExecutionOptions)} >> set '{nameof(PythonExecutionOptions.UseUv)}' to true or provide '{nameof(PythonExecutionOptions.PythonExe)}' (path to your local python executable)");

                services.Add(new ServiceDescriptor(typeof(IPythonExecutor), typeof(LocalPythonExecutor), lifetime));
            }

            // default tools for python coding are stored in LameTools
            return services.Any(s => s.ServiceType == typeof(IToolsService<LameTools>))  ? services : services.WithToolsFrom<LameTools>(lifetime);
        }

        /// <summary>
        /// Registers the IScriptExecutor (Bash or PowerShell) from the optional 'ScriptExecutionOptions' section.
        /// Without the section: PowerShell on Windows, Bash anywhere else, scripts in [temp]/lamechain-scripts
        /// 
        /// </summary>
        public static IServiceCollection WithScriptExecutor(this IServiceCollection services, IConfiguration configuration, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        {
            var section = configuration.GetSection(nameof(ScriptExecutionOptions));

            var options = section.Get<ScriptExecutionOptions>() ?? new ScriptExecutionOptions();

            // Register the ScriptExecutionOptions from configuration for DI (IOptions<ScriptExecutionOptions>)
            services.Configure<ScriptExecutionOptions>(section);

            var executorType = options.ResolveShell() == EScriptShell.PowerShell ? typeof(PowerShellScriptExecutor) : typeof(BashScriptExecutor);

            services.Add(new ServiceDescriptor(typeof(IScriptExecutor), executorType, lifetime));

            return services.Any(s => s.ServiceType == typeof(IToolsService<LameTools>)) ? services : services.WithToolsFrom<LameTools>(lifetime);
        }

        public static IServiceCollection AddLangfuseTracing(this IServiceCollection services, IConfiguration configuration)
        {
            var langfuseSettings = configuration.GetSection(nameof(LangfuseSettings)).Get<LangfuseSettings>();

            if (langfuseSettings == null) return services;

            services.AddOpenTelemetry().WithTracing(builder =>
            {
                builder.AddSource(langfuseSettings.SourceName)                       
                       .AddOtlpExporter(o => {
                           o.Endpoint = new Uri(langfuseSettings.BaseUrl + "/api/public/otel/v1/traces");
                           o.Protocol = OtlpExportProtocol.HttpProtobuf; 
                           o.Headers = $"Authorization=Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(langfuseSettings.PublicKey + ":" + langfuseSettings.PrivateKey))},x-langfuse-ingestion-version=4";
                       });
            });

            LameTelemetry.CreateSource(langfuseSettings.SourceName);  
            
            return services;
        }
        public static IServiceCollection AddPdfDocumentLoader(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch {
                ServiceLifetime.Scoped => services.AddScoped<IDocumentLoader<PdfLoaderService>, PdfLoaderService>(),
                ServiceLifetime.Transient => services.AddTransient<IDocumentLoader<PdfLoaderService>, PdfLoaderService>(),
                _ => services
            };
        public static IServiceCollection AddMarkdownDocumentLoader(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch {
                ServiceLifetime.Scoped => services.AddScoped<IDocumentLoader<MarkdownLoaderService>, MarkdownLoaderService>(),
                ServiceLifetime.Transient => services.AddTransient<IDocumentLoader<MarkdownLoaderService>, MarkdownLoaderService>(),
                _ => services
            };

        public static IServiceCollection AddSkillsLoader(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => lifetime switch
            {
                ServiceLifetime.Scoped => services.AddScoped<IDocumentLoader<SkillsLoaderService>, SkillsLoaderService>(),
                ServiceLifetime.Transient => services.AddTransient<IDocumentLoader<SkillsLoaderService>, SkillsLoaderService>(),
                _ => services
            };

        public static IServiceCollection ConfigureLameChain(this IServiceCollection services, IConfiguration appConfig, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            => services.ConfigureOllamaSettings(appConfig)
                       .AddOllamaSharpApiClient(appConfig, lifetime)
                       .AddOllamaEmbeddingsGenerator(appConfig, lifetime)
                       .AddLameChainServices(appConfig, lifetime);
        
    }
}
