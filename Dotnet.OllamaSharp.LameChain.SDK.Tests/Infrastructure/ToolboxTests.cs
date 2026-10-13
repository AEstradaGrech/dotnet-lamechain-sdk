using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Dotnet.OllamaSharp.LameChain.SDK.Extensions;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Interfaces;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.Tools;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // A second ToolService, used to check that the Toolbox aggregates tools across services.
    public class SecondaryTestTools : ToolsService<SecondaryTestTools>
    {
        [Description("Translates a text")]
        public string Translate(string text) => text;

        [Description("Static methods are not tools")]
        public static string StaticHelper() => "static";
    }

    public class ToolboxTests
    {
        [Fact]
        public void Ctor_ExposesOnlyDescribedPublicInstanceMethodsDeclaredInEachService()
        {
            var toolbox = new Toolbox([new ToolsServiceDescriptor(typeof(AgentTestTools)), new ToolsServiceDescriptor(typeof(SecondaryTestTools))]);

            toolbox.TryGetMethodInfo(nameof(AgentTestTools.LookupWeather), out var weather).Should().BeTrue();
            weather.Should().BeSameAs(typeof(AgentTestTools).GetMethod(nameof(AgentTestTools.LookupWeather)));
            toolbox.TryGetMethodInfo(nameof(AgentTestTools.SearchDocs), out _).Should().BeTrue();
            toolbox.TryGetMethodInfo(nameof(SecondaryTestTools.Translate), out _).Should().BeTrue();

            toolbox.TryGetMethodInfo(nameof(AgentTestTools.NotATool), out _).Should().BeFalse("it has no [Description]");
            toolbox.TryGetMethodInfo(nameof(SecondaryTestTools.StaticHelper), out _).Should().BeFalse("static methods are excluded");
            toolbox.TryGetMethodInfo(nameof(AgentTestTools.GetToolsCatalogue), out _).Should().BeFalse("inherited ToolsService members are excluded");
        }

        [Fact]
        public void TryGetMethodInfo_WithUnknownTool_ReturnsFalseAndNullMethodInfo()
        {
            var toolbox = new Toolbox([new ToolsServiceDescriptor(typeof(AgentTestTools))]);

            toolbox.TryGetMethodInfo("UnknownTool", out var methodInfo).Should().BeFalse();
            methodInfo.Should().BeNull();
        }

        [Fact]
        public void WithToolsFrom_RegistersOneToolboxSingletonCoveringEveryToolService()
        {
            var services = new ServiceCollection()
                .WithToolsFrom<AgentTestTools>()
                .WithToolsFrom<SecondaryTestTools>(ServiceLifetime.Singleton);

            services.Count(d => d.ServiceType == typeof(IToolbox)).Should().Be(1);
            services.Single(d => d.ServiceType == typeof(IToolbox)).Lifetime.Should().Be(ServiceLifetime.Singleton);

            using var provider = services.BuildServiceProvider();
            var toolbox = provider.GetRequiredService<IToolbox>();

            toolbox.Should().BeSameAs(provider.GetRequiredService<IToolbox>());
            toolbox.TryGetMethodInfo(nameof(AgentTestTools.LookupWeather), out _).Should().BeTrue();
            toolbox.TryGetMethodInfo(nameof(SecondaryTestTools.Translate), out _).Should().BeTrue();
        }

        [Theory]
        [InlineData(ServiceLifetime.Scoped)]
        [InlineData(ServiceLifetime.Transient)]
        [InlineData(ServiceLifetime.Singleton)]
        public void WithToolsFrom_RegistersToolServiceWithRequestedLifetime(ServiceLifetime lifetime)
        {
            var services = new ServiceCollection().WithToolsFrom<AgentTestTools>(lifetime);

            services.Single(d => d.ServiceType == typeof(IToolsService<AgentTestTools>)).Lifetime.Should().Be(lifetime);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IToolsService<AgentTestTools>>().Should().BeOfType<AgentTestTools>();
        }

        [Fact]
        public void GetToolsCatalogue_ListsOnlyDescribedMethods()
        {
            var catalogue = new AgentTestTools().GetToolsCatalogue().ToList();

            catalogue.Select(t => t.Name).Should().BeEquivalentTo(nameof(AgentTestTools.LookupWeather), nameof(AgentTestTools.SearchDocs));
            catalogue.Single(t => t.Name == nameof(AgentTestTools.LookupWeather)).Description.Should().Be("Returns the weather for a city");
        }
    }
}
