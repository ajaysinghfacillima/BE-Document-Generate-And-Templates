// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Reflection;
using AeonDocGen.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Tests.Contracts;

/// <summary>
/// API contract tests verifying exact HTTP method and route path coverage
/// for all 4 scoped endpoints.
/// </summary>
public class ApiContractTests
{
    // --- GET /api/v1/admin/templates ---

    [Fact]
    public void AdminTemplatesController_HasRouteAttribute_WithCorrectPath()
    {
        var routeAttr = typeof(AdminTemplatesController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/admin/templates", routeAttr!.Template);
    }

    [Fact]
    public void AdminTemplatesController_ListTemplates_IsHttpGet()
    {
        var method = typeof(AdminTemplatesController)
            .GetMethod("ListTemplates");
        Assert.NotNull(method);
        var httpGet = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGet);
    }

    [Fact]
    public void AdminTemplatesController_IsApiController()
    {
        var attr = typeof(AdminTemplatesController)
            .GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attr);
    }

    // --- POST /api/v1/admin/branding/assets ---

    [Fact]
    public void BrandingAssetsController_HasRouteAttribute_WithCorrectPath()
    {
        var routeAttr = typeof(BrandingAssetsController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/admin/branding/assets", routeAttr!.Template);
    }

    [Fact]
    public void BrandingAssetsController_UploadBrandingAssets_IsHttpPost()
    {
        var method = typeof(BrandingAssetsController)
            .GetMethod("UploadBrandingAssets");
        Assert.NotNull(method);
        var httpPost = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpPost);
    }

    [Fact]
    public void BrandingAssetsController_IsApiController()
    {
        var attr = typeof(BrandingAssetsController)
            .GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void BrandingAssetsController_ConsumesMultipartFormData()
    {
        var method = typeof(BrandingAssetsController).GetMethod("UploadBrandingAssets");
        Assert.NotNull(method);
        var consumesAttr = method!.GetCustomAttribute<ConsumesAttribute>();
        Assert.NotNull(consumesAttr);
        Assert.Contains("multipart/form-data", consumesAttr!.ContentTypes);
    }

    // --- POST /api/v1/projects/{projectId}/document-generations ---

    [Fact]
    public void DocumentsGenerationController_HasRouteAttribute_WithCorrectPath()
    {
        var routeAttr = typeof(DocumentsGenerationController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/projects/{projectId}/document-generations", routeAttr!.Template);
    }

    [Fact]
    public void DocumentsGenerationController_GenerateDocument_IsHttpPost()
    {
        var method = typeof(DocumentsGenerationController)
            .GetMethod("GenerateDocument");
        Assert.NotNull(method);
        var httpPosts = method!.GetCustomAttributes<HttpPostAttribute>().ToList();
        Assert.NotEmpty(httpPosts);
    }

    [Fact]
    public void DocumentsGenerationController_IsApiController()
    {
        var attr = typeof(DocumentsGenerationController)
            .GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void DocumentsGenerationController_ConsumesJson()
    {
        var method = typeof(DocumentsGenerationController).GetMethod("GenerateDocument");
        Assert.NotNull(method);
        var consumesAttr = method!.GetCustomAttribute<ConsumesAttribute>();
        Assert.NotNull(consumesAttr);
        Assert.Contains("application/json", consumesAttr!.ContentTypes);
    }

    // --- POST /api/v1/projects/{projectId}/documents/{documentId}/reviews ---

    [Fact]
    public void DocumentsReviewController_HasRouteAttribute_WithCorrectPath()
    {
        var routeAttr = typeof(DocumentsReviewController)
            .GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/projects/{projectId}/documents/{documentId}/reviews", routeAttr!.Template);
    }

    [Fact]
    public void DocumentsReviewController_ReviewDocument_IsHttpPost()
    {
        var method = typeof(DocumentsReviewController)
            .GetMethod("ReviewDocument");
        Assert.NotNull(method);
        var httpPosts = method!.GetCustomAttributes<HttpPostAttribute>().ToList();
        Assert.NotEmpty(httpPosts);
    }

    [Fact]
    public void DocumentsReviewController_IsApiController()
    {
        var attr = typeof(DocumentsReviewController)
            .GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void DocumentsReviewController_ConsumesJson()
    {
        var method = typeof(DocumentsReviewController).GetMethod("ReviewDocument");
        Assert.NotNull(method);
        var consumesAttr = method!.GetCustomAttribute<ConsumesAttribute>();
        Assert.NotNull(consumesAttr);
        Assert.Contains("application/json", consumesAttr!.ContentTypes);
    }

    [Fact]
    public void ScopedRoutes_DoNotUseVerbStyleSegments()
    {
        var generationRoute = typeof(DocumentsGenerationController).GetCustomAttribute<RouteAttribute>()!.Template!;
        var reviewRoute = typeof(DocumentsReviewController).GetCustomAttribute<RouteAttribute>()!.Template!;

        Assert.DoesNotContain("/generate", generationRoute, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/review/", reviewRoute, StringComparison.OrdinalIgnoreCase);
        Assert.False(reviewRoute.EndsWith("/review", StringComparison.OrdinalIgnoreCase));
    }

    // --- Route parameter validation ---

    [Fact]
    public void DocumentsGenerationController_HasProjectIdRouteParameter()
    {
        var method = typeof(DocumentsGenerationController).GetMethod("GenerateDocument");
        Assert.NotNull(method);
        var param = method!.GetParameters().FirstOrDefault(p => p.Name == "projectId");
        Assert.NotNull(param);
        var fromRoute = param!.GetCustomAttribute<FromRouteAttribute>();
        Assert.NotNull(fromRoute);
    }

    [Fact]
    public void DocumentsReviewController_HasProjectIdAndDocumentIdRouteParameters()
    {
        var method = typeof(DocumentsReviewController).GetMethod("ReviewDocument");
        Assert.NotNull(method);

        var projectIdParam = method!.GetParameters().FirstOrDefault(p => p.Name == "projectId");
        Assert.NotNull(projectIdParam);
        Assert.NotNull(projectIdParam!.GetCustomAttribute<FromRouteAttribute>());

        var documentIdParam = method.GetParameters().FirstOrDefault(p => p.Name == "documentId");
        Assert.NotNull(documentIdParam);
        Assert.NotNull(documentIdParam!.GetCustomAttribute<FromRouteAttribute>());
    }

    // --- All controllers produce JSON ---

    [Fact]
    public void AllEndpoints_ProduceApplicationJson()
    {
        var methods = new[]
        {
            typeof(AdminTemplatesController).GetMethod("ListTemplates"),
            typeof(BrandingAssetsController).GetMethod("UploadBrandingAssets"),
            typeof(DocumentsGenerationController).GetMethod("GenerateDocument"),
            typeof(DocumentsReviewController).GetMethod("ReviewDocument")
        };

        foreach (var method in methods)
        {
            Assert.NotNull(method);
            var produces = method!.GetCustomAttribute<ProducesAttribute>();
            Assert.NotNull(produces);
            Assert.Contains("application/json", produces!.ContentTypes);
        }
    }
}
