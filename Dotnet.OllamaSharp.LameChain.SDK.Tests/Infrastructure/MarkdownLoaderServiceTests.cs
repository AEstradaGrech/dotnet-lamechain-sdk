using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // The generic markdown loader reads <ResourcesPath>/markdowns/<fileName>.md and splits it into pages:
    // [one page per H1/H2 section (H3+ stay inside their parent section)] + [the full file as last page].
    // Unlike the SkillsLoader it does not extract YAML frontmatter as a heading page.
    public class MarkdownLoaderServiceTests : IDisposable
    {
        private const string Guide = "# Guide\n\nIntro text.\n\n## Setup\n\nInstall it.\n\n### Details\n\nMore details.\n\n## Usage\n\nRun it.\n";
        private const string Steps = "## Steps\n\n1. First step\n2. Second step\n\n## Notes\n\n- alpha\n- beta\n";
        private const string PlainText = "Just a paragraph without headings.\n";

        private readonly AgentTestHost _host = new AgentTestHost()
            .WithDefaultSkills()
            .WithMarkdown("guide", Guide)
            .WithMarkdown("steps", Steps)
            .WithMarkdown("plain", PlainText)
            .WithMarkdown(Path.Combine("docs", "nested-guide"), Guide);

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
        public async Task LoadDocument_WithSections_ReturnsOnePagePerSectionAndFullFileAsLastPage()
        {
            var document = await CreateLoader().LoadDocument("guide");

            document.Source.Should().Be("markdown");
            document.Pages.Should().HaveCount(4);
            document.TotalPages.Should().Be(4);
            document.Pages.Select(p => p.PageNumber).Should().Equal(1, 2, 3, 4);

            document.Pages[0].Text.Should().StartWith("# Guide").And.Contain("Intro text.").And.NotContain("Install it.");
            document.Pages[1].Text.Should().StartWith("## Setup").And.Contain("Install it.");
            document.Pages[2].Text.Should().StartWith("## Usage").And.Contain("Run it.");
            document.Pages[3].Text.Should().Be(Guide);
        }

        [Fact]
        public async Task LoadDocument_WithH3Heading_KeepsSubsectionInsideParentSection()
        {
            var document = await CreateLoader().LoadDocument("guide");

            document.Pages[1].Text.Should().Contain("### Details").And.Contain("More details.");
            document.Pages.Should().NotContain(p => p.Text.StartsWith("### Details"));
        }

        [Fact]
        public async Task LoadDocument_WithLists_RendersOrderedItemsNumberedAndBulletItemsDashed()
        {
            var document = await CreateLoader().LoadDocument("steps");

            document.Pages[0].Text.Should().Contain("1: First step").And.Contain("2: Second step");
            document.Pages[1].Text.Should().Contain("- alpha").And.Contain("- beta");
        }

        [Fact]
        public async Task LoadDocument_WithoutHeadings_ReturnsFullFileAsSinglePage()
        {
            var document = await CreateLoader().LoadDocument("plain");

            document.Pages.Should().ContainSingle().Which.Text.Should().Be(PlainText);
        }

        [Fact]
        public async Task LoadDocument_WithSubfolderPath_LoadsNestedFile()
        {
            var document = await CreateLoader().LoadDocument(Path.Combine("docs", "nested-guide"));

            document.Pages.Last().Text.Should().Be(Guide);
        }

        [Fact]
        public async Task LoadDocument_WithMissingFile_ThrowsFileNotFoundException()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader().LoadDocument("missing"));
        }

        [Fact]
        public async Task LoadDocument_WithSkillFolderPath_ThrowsFileNotFoundException()
        {
            // Skills (<name>/SKILL.md) are resolved by the SkillsLoaderService only
            await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader().LoadDocument(Path.Combine("skills", "senior-code-reviewer")));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        public async Task LoadPage_ReturnsRequestedPageDefaultingToFirst(int requested, int expectedPage)
        {
            var page = await CreateLoader().LoadPage("guide", requested);

            page.PageNumber.Should().Be(expectedPage);
        }

        [Fact]
        public async Task LoadPage_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateLoader().LoadPage("guide", 10));
        }

        [Fact]
        public async Task LoadPages_ReturnsBatchFromStartIndex()
        {
            var pages = await CreateLoader().LoadPages("guide", startIndex: 2, batchSize: 2);

            pages.Select(p => p.PageNumber).Should().Equal(2, 3);
        }

        [Fact]
        public async Task LoadPages_StartIndexOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateLoader().LoadPages("guide", startIndex: 10, batchSize: 2));
        }
    }
}
