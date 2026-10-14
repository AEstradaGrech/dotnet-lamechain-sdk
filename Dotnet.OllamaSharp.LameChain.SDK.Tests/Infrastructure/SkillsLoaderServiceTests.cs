using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Services.DocumentLoader;
using Dotnet.OllamaSharp.LameChain.SDK.Tests.Agents;

namespace Dotnet.OllamaSharp.LameChain.SDK.Tests.Infrastructure
{
    // The skills loader reads <SkillResourcesPath>/skills/<skillName>/SKILL.md and splits it into pages:
    // [frontmatter heading (if any)] + [one page per H1/H2 section] + [the full file as last page].
    public class SkillsLoaderServiceTests : IDisposable
    {
        private readonly AgentTestHost _host = new AgentTestHost()
            .WithDefaultSkills()
            .WithSkill("no-frontmatter", "# Plain Skill\n\nJust instructions.\n");

        public void Dispose() => _host.Dispose();

        private SkillsLoaderService CreateLoader()
            => new(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentLoader:SkillResourcesPath"] = _host.SkillResourcesPath })
                .Build());

        [Fact]
        public void Ctor_WithoutSkillResourcesPath_ThrowsArgumentNullException()
        {
            Action act = () => new SkillsLoaderService(new ConfigurationBuilder().Build());

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Ctor_WithGenericResourcesPathOnly_ThrowsArgumentNullException()
        {
            // The skills loader has its own setting: the generic markdown ResourcesPath must not be used as fallback
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentLoader:ResourcesPath"] = _host.ResourcesPath })
                .Build();

            Action act = () => new SkillsLoaderService(config);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task LoadDocument_WithFrontmatter_ReturnsHeadingSectionsAndFullFileAsPages()
        {
            var document = await CreateLoader().LoadDocument("senior-code-reviewer");

            document.Source.Should().Be("skill");
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
            var document = await CreateLoader().LoadDocument("no-frontmatter");

            document.Pages.Should().HaveCount(2);
            document.Pages[0].Text.Should().StartWith("# Plain Skill");
        }

        [Fact]
        public async Task LoadDocument_WithMissingSkill_ThrowsFileNotFoundException()
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader().LoadDocument("missing-skill"));
        }

        [Fact]
        public async Task LoadDocument_WithLegacySkillsPrefix_ThrowsFileNotFoundException()
        {
            // Before the SkillsLoader split, agents requested "skills\<name>" through the MarkdownLoader.
            // The skills folder is now part of the loader base path, so the prefix must not be passed anymore.
            await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader().LoadDocument(Path.Combine("skills", "senior-code-reviewer")));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        public async Task LoadPage_ReturnsRequestedPageDefaultingToFirst(int requested, int expectedPage)
        {
            var page = await CreateLoader().LoadPage("senior-code-reviewer", requested);

            page.PageNumber.Should().Be(expectedPage);
        }

        [Fact]
        public async Task LoadPage_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateLoader().LoadPage("senior-code-reviewer", 10));
        }

        [Fact]
        public async Task LoadPages_ReturnsBatchFromStartIndex()
        {
            var pages = await CreateLoader().LoadPages("senior-code-reviewer", startIndex: 2, batchSize: 2);

            pages.Select(p => p.PageNumber).Should().Equal(2, 3);
        }

        [Fact]
        public async Task LoadPages_StartIndexOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateLoader().LoadPages("senior-code-reviewer", startIndex: 10, batchSize: 2));
        }
    }
}
