namespace AeonDocGen.Tests.Contracts;

public class TemplatePaginationContractTests
{
    [Fact]
    public void TemplateRepository_UsesTenantScopedListQueryWithoutPagingWindow()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Repositories", "TemplateRepository.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("WHERE t.TenantId = @TenantId", code);
        Assert.DoesNotContain("p.RowNum > @Offset", code);
        Assert.DoesNotContain("p.RowNum <= (@Offset + @PageSize)", code);
    }

    [Fact]
    public void AdminTemplateResponse_DoesNotIncludePaginationMetadata()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Core", "DTOs", "AdminTemplateListResponseDto.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.DoesNotContain("AdminTemplatePaginationDto", code);
        Assert.DoesNotContain("TotalItems", code);
        Assert.DoesNotContain("TotalPages", code);
    }
}
