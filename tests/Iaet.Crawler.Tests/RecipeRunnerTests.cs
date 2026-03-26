using FluentAssertions;
using Iaet.Crawler;

namespace Iaet.Crawler.Tests;

public class RecipeRunnerTests
{
    [Fact]
    public void ValidateRecipe_MissingFile_ThrowsFileNotFound()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "does-not-exist.ts");

        var act = () => RecipeRunner.ValidateRecipe(nonExistentPath);

        act.Should().Throw<FileNotFoundException>()
           .WithMessage("*does-not-exist.ts*");
    }

    [Fact]
    public void ValidateRecipe_NonTsFile_ThrowsArgument()
    {
        // Create a real file but with wrong extension
        var jsFile = Path.ChangeExtension(Path.GetTempFileName(), ".js");
        File.WriteAllText(jsFile, "// js file");
        try
        {
            var act = () => RecipeRunner.ValidateRecipe(jsFile);
            act.Should().Throw<ArgumentException>()
               .WithMessage("*TypeScript*");
        }
        finally
        {
            File.Delete(jsFile);
        }
    }

    [Fact]
    public void ValidateRecipe_ValidTsFile_DoesNotThrow()
    {
        var tsFile = Path.ChangeExtension(Path.GetTempFileName(), ".ts");
        File.WriteAllText(tsFile, "// ts file");
        try
        {
            var act = () => RecipeRunner.ValidateRecipe(tsFile);
            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(tsFile);
        }
    }

    [Fact]
    public void BuildCommand_IncludesRecipePath()
    {
        var recipePath = "/some/path/my-recipe.ts";
        var (command, args) = RecipeRunner.BuildCommand(recipePath, 9222);

        command.Should().Be("npx");
        args.Should().Contain("tsx");
        args.Should().Contain(recipePath);
    }
}
