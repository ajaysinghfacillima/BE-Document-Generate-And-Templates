// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Text;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Services;
using AeonDocGen.Api.Middleware;
using AeonDocGen.Api.Policies;
using AeonDocGen.Infrastructure.Clients;
using AeonDocGen.Infrastructure.Data;
using AeonDocGen.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AeonDocGen.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BrandingUploadSettings>(configuration.GetSection("BrandingUpload"));
        services.Configure<DocumentGenerationSettings>(configuration.GetSection("DocumentGeneration"));
        services.Configure<StorageSettings>(configuration.GetSection("Storage"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAdminTemplateService, AdminTemplateService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRequestAuthorizationService, RequestAuthorizationService>();

        services.AddScoped<IBrandingAssetRepository, BrandingAssetRepository>();
        services.AddScoped<IBrandingAssetService, BrandingAssetService>();
        services.AddSingleton<IBrandingStorageClient, BrandingStorageClient>();
        services.AddSingleton<IDocumentStorageClient, DocumentStorageClient>();
        services.AddSingleton<IIdempotencyRepository, IdempotencyRepository>();
        services.AddHttpClient<IMalwareScannerClient, MalwareScannerClient>();

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITemplateResolutionRepository, TemplateResolutionRepository>();
        services.AddScoped<ISourceEntityRepository, SourceEntityRepository>();
        services.AddScoped<IDocumentArtifactRepository, DocumentArtifactRepository>();
        services.AddScoped<IDocumentSourceRepository, DocumentSourceRepository>();
        services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();
        services.AddHostedService<DocumentRenderStoreWorker>();

        services.AddScoped<IDocumentReviewEventRepository, DocumentReviewEventRepository>();
        services.AddScoped<IDocumentReviewService, DocumentReviewService>();

        var brandingSettings = configuration.GetSection("BrandingUpload").Get<BrandingUploadSettings>() ?? new BrandingUploadSettings();
        if (brandingSettings.MalwareScanEnabled && string.IsNullOrWhiteSpace(brandingSettings.MalwareScanEndpoint))
        {
            throw new InvalidOperationException("BrandingUpload:MalwareScanEndpoint must be configured via environment/secret store when malware scanning is enabled.");
        }

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey) ||
            string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
            string.IsNullOrWhiteSpace(jwtSettings.Audience))
        {
            throw new InvalidOperationException("Jwt signing key, issuer, and audience must be configured via environment/secret store.");
        }

        var key = Encoding.UTF8.GetBytes(jwtSettings.SigningKey);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true
                };
            });

        services.AddAuthorization();

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy("ApiCors", policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddHealthChecks();
        services.AddResponseCompression();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
