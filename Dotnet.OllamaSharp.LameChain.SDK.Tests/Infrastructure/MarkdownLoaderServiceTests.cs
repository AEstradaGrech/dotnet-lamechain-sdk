using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // The skill loader reads <ResourcesPath>/markdowns/<fileName>/SKILL.md and splits it into pages:
    // [frontmatter heading (if any)] + [one page per H1/H2 section] + [the full file as last page].
    public class MarkdownLoaderServiceTests : IDisposable
    {
        private readonly AgentTestHost _host = new AgentTestHost()
            .WithDefaultSkills()
            .WithSkill("no-frontmatter", "# Plain Skill\n\nJust instructions.\n");

        public void Dispose() => _host.Dispose();

        private MarkdownLoaderService CreateLoader()
            => new(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentLoader:ResourcesPath"] = _host.ResourcesPath })
                .Build());

        [Fact]
        public void Ctor_WithoutResourcesPath_ThrowsArgumentNullException()
        {
            Action act = () => new MarkdownLoaderService(new ConfigurationBuilder().Build());

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task LoadDocument_WithFrontmatter_ReturnsHeadingSectionsAndFullFileAsPages()
        {
            var document = await CreateLoader().LoadDocument("skills\\senior-code-reviewer");

            document.Source.Should().Be("markdown");
            document.Pages.Should().HaveCount(4);
            document.TotalPages.Should().Be(4);
            document.Pages.Select(p => p.PageNumber).Should().Equal(1, 2, 3, 4);

            document.Pages[0].Text.Should().StartWith("name: senior-code-reviewer").And.Contain("type: behavioral").And.NotContain("---");
            document.Pages[1].Text.Should().StartWith("# Senior Code Reviewer").And.Contain(TestSkills.ReviewerBody);
            document.Pages[2].Text.Should().StartWith("## Tone").And.Contain("Direct and concise.");
            document.Pages[3].Text.Should().Be(TestSkills.SeniorCodeReviewer);
        }

        [Fact]
        public async Task LoadDocument_WithoutFrontmatter_StartsWithFirstSection()
        {
            var document = await CreateLoader().LoadDocument("skills\\no-frontmatter");

            document.Pages.Should().HaveCount(2);
            document.Pages[0].Text.Should().StartWith("# Plain Skill");
        }

        [Fact]
        public async Task LoadDocument_WithMissingSkill_ThrowsFileNotFoundException()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader().LoadDocument("skills\\missing-skill"));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        public async Task LoadPage_ReturnsRequestedPageDefaultingToFirst(int requested, int expectedPage)
        {
            var page = await CreateLoader().LoadPage("skills\\senior-code-reviewer", requested);

            page.PageNumber.Should().Be(expectedPage);
        }

        [Fact]
        public async Task LoadPage_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateLoader().LoadPage("skills\\senior-code-reviewer", 10));
        }

        [Fact]
        public async Task LoadPages_ReturnsBatchFromStartIndex()
        {
            var pages = await CreateLoader().LoadPages("skills\\senior-code-reviewer", startIndex: 2, batchSize: 2);

            pages.Select(p => p.PageNumber).Should().Equal(2, 3);
        }
    }
}
