// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AeonDocGen.Core.Services;

/// <summary>
/// Service for admin template listing with audit logging and structured diagnostics.
/// </summary>
public sealed class AdminTemplateService : IAdminTemplateService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly ITemplateRepository _templateRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminTemplateService> _logger;

    public AdminTemplateService(
        ITemplateRepository templateRepository,
        IAuditLogRepository auditLogRepository,
        IMemoryCache memoryCache,
        ILogger<AdminTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _auditLogRepository = auditLogRepository;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task<AdminTemplateListResponseDto> ListTemplatesAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Template listing request received. TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}",
            tenantId, actorUserId, correlationId);

        var cacheKey = $"admin-templates:{tenantId}";
        if (_memoryCache.TryGetValue(cacheKey, out AdminTemplateListResponseDto? cached) && cached != null)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Template listing served from cache. TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, ResultCount={ResultCount}, LatencyMs={LatencyMs}",
                tenantId, actorUserId, correlationId, cached.Items.Count, stopwatch.ElapsedMilliseconds);
            await WriteAuditLogAsync(tenantId, actorUserId, correlationId, "success", cached.Items.Count, cancellationToken);
            return cached;
        }

        var templates = await _templateRepository.GetTemplatesByTenantIdAsync(tenantId, cancellationToken);
        var templateIds = templates.Select(t => t.TemplateId).Distinct().ToList();
        var templateVersions = await _templateRepository.GetTemplateVersionsByTemplateIdsAsync(tenantId, templateIds, cancellationToken);

        var versionsByTemplateId = templateVersions
            .GroupBy(v => v.TemplateId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(v => v.IsPublished)
                      .ThenByDescending(v => v.CreatedAt)
                      .Select(v => v.TemplateVersion)
                      .ToList());

        var templateGroups = templates
            .GroupBy(t => new { t.TemplateId, t.Name, t.CurrentVersion })
            .OrderBy(g => g.Key.Name)
            .Select(g =>
            {
                var documentTypes = g.Select(t => t.DocumentType).Distinct().OrderBy(d => d).ToList();
                var templateId = g.Key.TemplateId;

                versionsByTemplateId.TryGetValue(templateId, out var versions);
                versions ??= new List<string>();

                var currentVersion = g.Key.CurrentVersion;
                if (versions.Count > 0 && !string.IsNullOrWhiteSpace(versions[0]))
                {
                    currentVersion = versions[0];
                }

                return new AdminTemplateItemDto
                {
                    Id = templateId.ToString(),
                    Name = g.Key.Name,
                    CurrentVersion = currentVersion,
                    Versions = versions,
                    DocumentTypes = documentTypes
                };
            })
            .ToList();

        var response = new AdminTemplateListResponseDto
        {
            Items = templateGroups
        };
        _memoryCache.Set(cacheKey, response, CacheTtl);

        stopwatch.Stop();

        _logger.LogInformation(
            "Template listing completed. TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, ResultCount={ResultCount}, LatencyMs={LatencyMs}",
            tenantId, actorUserId, correlationId, response.Items.Count, stopwatch.ElapsedMilliseconds);

        // TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
        await WriteAuditLogAsync(tenantId, actorUserId, correlationId, "success", response.Items.Count, cancellationToken);

        return response;
    }

    private async Task WriteAuditLogAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        string outcome,
        int resultCount,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var afterJson = JsonSerializer.Serialize(new { resultCount });

        var auditLog = new AuditLogEntity
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            ActorUserId = actorUserId,
            ActorType = "user",
            Action = "templates.list",
            ResourceType = "Template",
            ResourceId = tenantId,
            ScopeType = "tenant",
            ScopeId = tenantId,
            Outcome = outcome,
            CorrelationId = correlationId,
            AfterJson = afterJson,
            ImmutableHash = ComputeImmutableHash(tenantId, actorUserId, "templates.list", correlationId, now)
        };

        await _auditLogRepository.InsertAuditLogAsync(auditLog, cancellationToken);
    }

    private static string ComputeImmutableHash(Guid tenantId, Guid actorUserId, string action, string correlationId, DateTime timestamp)
    {
        var payload = $"{tenantId}|{actorUserId}|{action}|{correlationId}|{timestamp:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }
}
