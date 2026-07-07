// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using AeonDocGen.Core.Validators;

namespace AeonDocGen.Tests.Validators;

public class HeaderValidatorTests
{
    private const string TraceId = "trace-test-001";

    // Authorization header tests
    [Fact]
    public void ValidateAuthorizationHeader_Null_ReturnsInvalid()
    {
        var (isValid, token, error) = HeaderValidator.ValidateAuthorizationHeader(null, TraceId);
        Assert.False(isValid);
        Assert.Null(token);
        Assert.NotNull(error);
        Assert.Equal("UNAUTHENTICATED", error!.Code);
    }

    [Fact]
    public void ValidateAuthorizationHeader_Empty_ReturnsInvalid()
    {
        var (isValid, _, error) = HeaderValidator.ValidateAuthorizationHeader("", TraceId);
        Assert.False(isValid);
        Assert.Equal("UNAUTHENTICATED", error!.Code);
    }

    [Fact]
    public void ValidateAuthorizationHeader_BasicScheme_ReturnsInvalid()
    {
        var (isValid, _, error) = HeaderValidator.ValidateAuthorizationHeader("Basic abc123", TraceId);
        Assert.False(isValid);
        Assert.Equal("UNAUTHENTICATED", error!.Code);
    }

    [Fact]
    public void ValidateAuthorizationHeader_BearerNoToken_ReturnsInvalid()
    {
        var (isValid, _, error) = HeaderValidator.ValidateAuthorizationHeader("Bearer ", TraceId);
        Assert.False(isValid);
        Assert.Equal("UNAUTHENTICATED", error!.Code);
    }

    [Fact]
    public void ValidateAuthorizationHeader_ValidBearer_ReturnsValid()
    {
        var (isValid, token, error) = HeaderValidator.ValidateAuthorizationHeader("Bearer my-jwt-token", TraceId);
        Assert.True(isValid);
        Assert.Equal("my-jwt-token", token);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAuthorizationHeader_BearerCaseInsensitive_ReturnsValid()
    {
        var (isValid, token, _) = HeaderValidator.ValidateAuthorizationHeader("bearer my-token", TraceId);
        Assert.True(isValid);
        Assert.Equal("my-token", token);
    }

    [Fact]
    public void ValidateAuthorizationHeader_PreservesTraceId()
    {
        var (_, _, error) = HeaderValidator.ValidateAuthorizationHeader(null, "custom-trace");
        Assert.Equal("custom-trace", error!.TraceId);
    }

    // X-Tenant-Id header tests
    [Fact]
    public void ValidateTenantIdHeader_Null_ReturnsInvalid()
    {
        var (isValid, _, error) = HeaderValidator.ValidateTenantIdHeader(null, TraceId);
        Assert.False(isValid);
        Assert.Equal("INVALID_REQUEST", error!.Code);
    }

    [Fact]
    public void ValidateTenantIdHeader_Empty_ReturnsInvalid()
    {
        var (isValid, _, error) = HeaderValidator.ValidateTenantIdHeader("", TraceId);
        Assert.False(isValid);
        Assert.Equal("INVALID_REQUEST", error!.Code);
    }

    [Fact]
    public void ValidateTenantIdHeader_OpaqueString_ReturnsValid()
    {
        var (isValid, tenantId, error) = HeaderValidator.ValidateTenantIdHeader("ten-001", TraceId);
        Assert.True(isValid);
        Assert.NotEqual(Guid.Empty, tenantId);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateTenantIdHeader_ValidGuid_ReturnsValid()
    {
        var guid = Guid.NewGuid();
        var (isValid, tenantId, error) = HeaderValidator.ValidateTenantIdHeader(guid.ToString(), TraceId);
        Assert.True(isValid);
        Assert.Equal(guid, tenantId);
        Assert.Null(error);
    }

    // Tenant isolation tests
    [Fact]
    public void ValidateTenantIsolation_SameTenants_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var error = HeaderValidator.ValidateTenantIsolation(tenantId, tenantId, TraceId);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateTenantIsolation_DifferentTenants_ReturnsForbidden()
    {
        var error = HeaderValidator.ValidateTenantIsolation(Guid.NewGuid(), Guid.NewGuid(), TraceId);
        Assert.NotNull(error);
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public void ValidateTenantIsolation_DifferentTenants_CustomCode()
    {
        var error = HeaderValidator.ValidateTenantIsolation(Guid.NewGuid(), Guid.NewGuid(), TraceId, "FORBIDDEN_DOCUMENT_REVIEW");
        Assert.Equal("FORBIDDEN_DOCUMENT_REVIEW", error!.Code);
    }

    // Role validation tests
    [Fact]
    public void ValidateRole_AdminInAllowedRoles_ReturnsNull()
    {
        var error = HeaderValidator.ValidateRole("Admin", new[] { "Admin" }, TraceId);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateRole_CaseInsensitive_ReturnsNull()
    {
        var error = HeaderValidator.ValidateRole("admin", new[] { "Admin" }, TraceId);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateRole_NotInAllowedRoles_ReturnsForbidden()
    {
        var error = HeaderValidator.ValidateRole("Consultant", new[] { "Admin" }, TraceId);
        Assert.NotNull(error);
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    [Fact]
    public void ValidateRole_MultipleAllowedRoles_Matches()
    {
        var allowed = new[] { "Sustainability Consultant", "Admin", "Owner", "PMC" };
        Assert.Null(HeaderValidator.ValidateRole("Admin", allowed, TraceId));
        Assert.Null(HeaderValidator.ValidateRole("Sustainability Consultant", allowed, TraceId));
        Assert.Null(HeaderValidator.ValidateRole("Owner", allowed, TraceId));
        Assert.Null(HeaderValidator.ValidateRole("PMC", allowed, TraceId));
    }

    [Fact]
    public void ValidateRole_ExternalAuditor_Rejected()
    {
        var allowed = new[] { "Sustainability Consultant", "Admin", "Owner", "PMC" };
        var error = HeaderValidator.ValidateRole("External Auditor", allowed, TraceId);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateRole_EmptyRole_ReturnsForbidden()
    {
        var error = HeaderValidator.ValidateRole("", new[] { "Admin" }, TraceId);
        Assert.NotNull(error);
    }

    // Idempotency-Key header tests
    [Fact]
    public void ValidateIdempotencyKeyHeader_Null_ReturnsInvalid()
    {
        var (isValid, error) = HeaderValidator.ValidateIdempotencyKeyHeader(null, TraceId);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateIdempotencyKeyHeader_Empty_ReturnsInvalid()
    {
        var (isValid, error) = HeaderValidator.ValidateIdempotencyKeyHeader("", TraceId);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateIdempotencyKeyHeader_Valid_ReturnsValid()
    {
        var (isValid, error) = HeaderValidator.ValidateIdempotencyKeyHeader("brand-upload-001", TraceId);
        Assert.True(isValid);
        Assert.Null(error);
    }

    // If-Match header tests
    [Fact]
    public void ValidateIfMatchHeader_Null_ReturnsInvalid()
    {
        var (isValid, error) = HeaderValidator.ValidateIfMatchHeader(null, TraceId);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateIfMatchHeader_Empty_ReturnsInvalid()
    {
        var (isValid, error) = HeaderValidator.ValidateIfMatchHeader("", TraceId);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateIfMatchHeader_Valid_ReturnsValid()
    {
        var (isValid, error) = HeaderValidator.ValidateIfMatchHeader("\"1-abc\"", TraceId);
        Assert.True(isValid);
        Assert.Null(error);
    }

    // Correlation Id resolution tests
    [Fact]
    public void ResolveCorrelationId_WhenHeaderPresent_ReturnsHeader()
    {
        var result = HeaderValidator.ResolveCorrelationId("corr-123", "fallback");
        Assert.Equal("corr-123", result);
    }

    [Fact]
    public void ResolveCorrelationId_WhenHeaderNull_ReturnsFallback()
    {
        var result = HeaderValidator.ResolveCorrelationId(null, "fallback-id");
        Assert.Equal("fallback-id", result);
    }

    [Fact]
    public void ResolveCorrelationId_WhenHeaderEmpty_ReturnsFallback()
    {
        var result = HeaderValidator.ResolveCorrelationId("", "fallback-id");
        Assert.Equal("fallback-id", result);
    }

    [Fact]
    public void ResolveCorrelationId_WhenHeaderWhitespace_ReturnsFallback()
    {
        var result = HeaderValidator.ResolveCorrelationId("   ", "fallback-id");
        Assert.Equal("fallback-id", result);
    }
}
