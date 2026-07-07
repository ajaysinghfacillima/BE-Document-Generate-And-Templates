// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Core.Services;

/// <summary>
/// Service for branding asset upload, validation, idempotency, storage coordination,
/// transactional metadata persistence, and audit logging.
/// </summary>
public sealed partial class BrandingAssetService : IBrandingAssetService
{
    private static readonly HashSet<string> SupportedColorKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "primary", "secondary", "accent", "text", "background"
    };

    private readonly IBrandingAssetRepository _brandingAssetRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IBrandingStorageClient _fileStorageClient;
    private readonly IMalwareScannerClient _malwareScannerClient;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly BrandingUploadSettings _settings;
    private readonly ILogger<BrandingAssetService> _logger;

    public BrandingAssetService(
        IBrandingAssetRepository brandingAssetRepository,
        IAuditLogRepository auditLogRepository,
        IIdempotencyRepository idempotencyRepository,
        IBrandingStorageClient fileStorageClient,
        IMalwareScannerClient malwareScannerClient,
        IDbConnectionFactory connectionFactory,
        IOptions<BrandingUploadSettings> settings,
        ILogger<BrandingAssetService> logger)
    {
        _brandingAssetRepository = brandingAssetRepository;
        _auditLogRepository = auditLogRepository;
        _idempotencyRepository = idempotencyRepository;
        _fileStorageClient = fileStorageClient;
        _malwareScannerClient = malwareScannerClient;
        _connectionFactory = connectionFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task<BrandingAssetResponseDto> UploadBrandingAssetsAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        BrandingUploadInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Branding asset upload request received. TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, IdempotencyKey={IdempotencyKey}",
            tenantId, actorUserId, correlationId, idempotencyKey);

        var requestHash = ComputeRequestHash(input);

        // TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
        var existingIdempotency = await _idempotencyRepository.GetByKeyAsync(idempotencyKey, tenantId, cancellationToken);
        if (existingIdempotency != null)
        {
            if (existingIdempotency.RequestHash == requestHash)
            {
                _logger.LogInformation(
                    "Idempotent replay detected. IdempotencyKey={IdempotencyKey}, TenantId={TenantId}, CorrelationId={CorrelationId}",
                    idempotencyKey, tenantId, correlationId);
                return JsonSerializer.Deserialize<BrandingAssetResponseDto>(existingIdempotency.ResponseJson)!;
            }
            throw new InvalidOperationException("Idempotency-Key has been used with a different payload.");
        }

        ValidateAtLeastOneInput(input);
        ValidateLogoFile(input);
        ValidateColorsJson(input);
        ValidateFontsZip(input);

        _logger.LogInformation(
            "Branding asset validation passed. TenantId={TenantId}, CorrelationId={CorrelationId}, HasLogo={HasLogo}, HasColors={HasColors}, HasFonts={HasFonts}",
            tenantId, correlationId, input.LogoData != null, input.ColorsJson != null, input.FontsZipData != null);

        // TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
        await ScanUploadedFilesForMalware(input, cancellationToken);

        string? logoStorageUri = null;
        string? fontsStorageUri = null;
        var uploadedPaths = new List<string>();

        try
        {
            if (input.LogoData != null)
            {
                var logoPath = $"{tenantId}/branding/logo/{input.LogoFileName}";
                logoStorageUri = await _fileStorageClient.StoreFileAsync(logoPath, input.LogoData, cancellationToken);
                uploadedPaths.Add(logoStorageUri);
                _logger.LogInformation("Logo file stored. TenantId={TenantId}, StorageUri={StorageUri}, CorrelationId={CorrelationId}",
                    tenantId, logoStorageUri, correlationId);
            }

            if (input.FontsZipData != null)
            {
                var fontsPath = $"{tenantId}/branding/fonts/{input.FontsZipFileName}";
                fontsStorageUri = await _fileStorageClient.StoreFileAsync(fontsPath, input.FontsZipData, cancellationToken);
                uploadedPaths.Add(fontsStorageUri);
                _logger.LogInformation("Fonts archive stored. TenantId={TenantId}, StorageUri={StorageUri}, CorrelationId={CorrelationId}",
                    tenantId, fontsStorageUri, correlationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage write failed for branding assets. TenantId={TenantId}, CorrelationId={CorrelationId}", tenantId, correlationId);
            throw new InvalidOperationException("Failed to store branding asset files.", ex);
        }

        var existing = await _brandingAssetRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        BrandingAssetEntity entity;
        bool isCreate = existing == null;
        string? beforeJson = null;

        if (isCreate)
        {
            entity = new BrandingAssetEntity
            {
                BrandingAssetId = Guid.NewGuid(),
                TenantId = tenantId,
                Status = "updated",
                LogoStorageUri = logoStorageUri,
                FontsStorageUri = fontsStorageUri,
                ColorsJson = input.ColorsJson,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
                Etag = $"\"1-{Guid.NewGuid():N}\""
            };
        }
        else
        {
            beforeJson = JsonSerializer.Serialize(new
            {
                existing!.LogoStorageUri,
                existing.FontsStorageUri,
                existing.ColorsJson,
                existing.Status,
                existing.Version,
                existing.Etag
            });

            entity = existing;
            entity.LogoStorageUri = logoStorageUri ?? existing.LogoStorageUri;
            entity.FontsStorageUri = fontsStorageUri ?? existing.FontsStorageUri;
            entity.ColorsJson = input.ColorsJson ?? existing.ColorsJson;
            entity.Status = "updated";
            entity.UpdatedAt = now;
            entity.Version += 1;
            entity.Etag = $"\"{entity.Version}-{Guid.NewGuid():N}\"";
        }

        var afterJson = JsonSerializer.Serialize(new
        {
            entity.LogoStorageUri,
            entity.FontsStorageUri,
            entity.ColorsJson,
            entity.Status,
            entity.Version,
            entity.Etag
        });

        var response = BuildBrandingAssetResponse(entity);
        var responseJson = JsonSerializer.Serialize(response);

        // TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
            using var transaction = connection.BeginTransaction();

            try
            {
                if (isCreate)
                {
                    await _brandingAssetRepository.CreateAsync(entity, connection, transaction, cancellationToken);
                }
                else
                {
                    await _brandingAssetRepository.UpdateAsync(entity, connection, transaction, cancellationToken);
                }

                var auditLog = CreateAuditLogEntry(tenantId, actorUserId, correlationId, entity.BrandingAssetId,
                    "success", beforeJson, afterJson, now);
                await _auditLogRepository.InsertAuditLogAsync(auditLog, connection, transaction, cancellationToken);

                var idempotencyRecord = new IdempotencyRecordEntity
                {
                    IdempotencyKey = idempotencyKey,
                    TenantId = tenantId,
                    RequestHash = requestHash,
                    ResponseJson = responseJson,
                    StatusCode = 201,
                    CreatedAt = now
                };
                await _idempotencyRepository.InsertAsync(idempotencyRecord, connection, transaction, cancellationToken);

                await DbExecutionUtilities.CommitTransactionAsync(transaction, cancellationToken);
            }
            catch
            {
                await DbExecutionUtilities.RollbackTransactionAsync(transaction, cancellationToken);
                throw;
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex,
                "Database persistence failed after file upload. Marking orphaned files for cleanup. TenantId={TenantId}, CorrelationId={CorrelationId}",
                tenantId, correlationId);
            await QueueOrphanedFileCleanup(tenantId, correlationId, uploadedPaths, cancellationToken);
            throw new InvalidOperationException("Failed to persist branding asset metadata.", ex);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Branding asset upload completed. TenantId={TenantId}, ActorUserId={ActorUserId}, BrandingAssetId={BrandingAssetId}, Version={Version}, CorrelationId={CorrelationId}, LatencyMs={LatencyMs}",
            tenantId, actorUserId, entity.BrandingAssetId, entity.Version, correlationId, stopwatch.ElapsedMilliseconds);

        return response;
    }

    private void ValidateAtLeastOneInput(BrandingUploadInput input)
    {
        if (input.LogoData == null && string.IsNullOrWhiteSpace(input.ColorsJson) && input.FontsZipData == null)
        {
            throw new ArgumentException("At least one of logoFile, colorsJson, or fontsZip must be supplied.");
        }
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    private void ValidateLogoFile(BrandingUploadInput input)
    {
        if (input.LogoData == null) return;

        if (input.LogoData.Length == 0)
        {
            throw new ArgumentException("logoFile size must be greater than zero.");
        }

        if (input.LogoData.Length > _settings.MaxLogoSizeBytes)
        {
            throw new ArgumentException($"logoFile size exceeds the maximum allowed size of {_settings.MaxLogoSizeBytes} bytes.");
        }

        if (string.IsNullOrEmpty(input.LogoContentType) ||
            !_settings.AllowedLogoMimeTypes.Contains(input.LogoContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"logoFile must be an allowed image format: {string.Join(", ", _settings.AllowedLogoMimeTypes)}.");
        }
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    private void ValidateColorsJson(BrandingUploadInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ColorsJson)) return;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(input.ColorsJson);
        }
        catch (JsonException)
        {
            throw new ArgumentException("colorsJson must be valid JSON.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("colorsJson must be a JSON object.");
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!SupportedColorKeys.Contains(property.Name))
                {
                    throw new ArgumentException($"colorsJson contains unsupported key '{property.Name}'. Supported keys: {string.Join(", ", SupportedColorKeys)}.");
                }

                var value = property.Value.GetString();
                if (string.IsNullOrEmpty(value) || !CssHexColorRegex().IsMatch(value))
                {
                    throw new ArgumentException($"colorsJson key '{property.Name}' has an invalid CSS hex color value.");
                }
            }
        }
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    private void ValidateFontsZip(BrandingUploadInput input)
    {
        if (input.FontsZipData == null) return;

        if (input.FontsZipData.Length == 0)
        {
            throw new ArgumentException("fontsZip size must be greater than zero.");
        }

        if (input.FontsZipData.Length > _settings.MaxFontsZipSizeBytes)
        {
            throw new ArgumentException($"fontsZip size exceeds the maximum allowed size of {_settings.MaxFontsZipSizeBytes} bytes.");
        }

        try
        {
            using var stream = new MemoryStream(input.FontsZipData);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            if (archive.Entries.Count == 0)
            {
                throw new ArgumentException("fontsZip archive is empty.");
            }

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var extension = Path.GetExtension(entry.Name);
                if (!_settings.AllowedFontExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"fontsZip contains unsupported file type '{extension}'. Allowed extensions: {string.Join(", ", _settings.AllowedFontExtensions)}.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("fontsZip is not a valid ZIP archive.");
        }
    }

    // TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    private async Task ScanUploadedFilesForMalware(BrandingUploadInput input, CancellationToken cancellationToken)
    {
        if (!_settings.MalwareScanEnabled) return;

        if (input.LogoData != null)
        {
            var isSafe = await _malwareScannerClient.IsSafeAsync(input.LogoData, input.LogoFileName ?? "logo", cancellationToken);
            if (!isSafe)
            {
                throw new InvalidOperationException("Malware scan detected unsafe content in logoFile. Upload rejected.");
            }
        }

        if (input.FontsZipData != null)
        {
            var isSafe = await _malwareScannerClient.IsSafeAsync(input.FontsZipData, input.FontsZipFileName ?? "fonts.zip", cancellationToken);
            if (!isSafe)
            {
                throw new InvalidOperationException("Malware scan detected unsafe content in fontsZip. Upload rejected.");
            }
        }
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    private static BrandingAssetResponseDto BuildBrandingAssetResponse(BrandingAssetEntity entity)
    {
        return new BrandingAssetResponseDto
        {
            Id = entity.BrandingAssetId.ToString(),
            TenantId = entity.TenantId.ToString(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Version = entity.Version,
            Etag = entity.Etag,
            Status = entity.Status
        };
    }

    // TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    private AuditLogEntity CreateAuditLogEntry(
        Guid tenantId, Guid actorUserId, string correlationId,
        Guid brandingAssetId, string outcome, string? beforeJson, string afterJson, DateTime timestamp)
    {
        return new AuditLogEntity
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
            ActorUserId = actorUserId,
            ActorType = "user",
            Action = "branding.assets.upload",
            ResourceType = "BrandingAsset",
            ResourceId = brandingAssetId,
            ScopeType = "tenant",
            ScopeId = tenantId,
            Outcome = outcome,
            CorrelationId = correlationId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            ImmutableHash = ComputeImmutableHash(tenantId, actorUserId, "branding.assets.upload", correlationId, timestamp)
        };
    }

    private async Task QueueOrphanedFileCleanup(Guid tenantId, string correlationId, List<string> uploadedPaths, CancellationToken cancellationToken)
    {
        if (uploadedPaths.Count == 0) return;

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await DbExecutionUtilities.OpenConnectionAsync(connection, cancellationToken);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO JobQueue (JobQueueId, TenantId, JobType, Status, Priority, PayloadJson, AvailableAt, CreatedAt, UpdatedAt, AttemptCount, MaxAttempts, JobTimeoutSeconds)
                VALUES (@JobQueueId, @TenantId, @JobType, @Status, @Priority, @PayloadJson, @AvailableAt, @CreatedAt, @UpdatedAt, @AttemptCount, @MaxAttempts, @JobTimeoutSeconds)";
            var pJobQueueId = cmd.CreateParameter();
            pJobQueueId.ParameterName = "@JobQueueId";
            pJobQueueId.Value = Guid.NewGuid();
            cmd.Parameters.Add(pJobQueueId);
            var pTenant = cmd.CreateParameter();
            pTenant.ParameterName = "@TenantId";
            pTenant.Value = tenantId;
            cmd.Parameters.Add(pTenant);
            var pJobType = cmd.CreateParameter();
            pJobType.ParameterName = "@JobType";
            pJobType.Value = "branding.orphaned-file-cleanup";
            cmd.Parameters.Add(pJobType);
            var pStatus = cmd.CreateParameter();
            pStatus.ParameterName = "@Status";
            pStatus.Value = "queued";
            cmd.Parameters.Add(pStatus);
            var pPriority = cmd.CreateParameter();
            pPriority.ParameterName = "@Priority";
            pPriority.Value = 5;
            cmd.Parameters.Add(pPriority);
            var pPayload = cmd.CreateParameter();
            pPayload.ParameterName = "@PayloadJson";
            pPayload.Value = JsonSerializer.Serialize(new { tenantId, correlationId, orphanedPaths = uploadedPaths });
            cmd.Parameters.Add(pPayload);
            var pAvail = cmd.CreateParameter();
            pAvail.ParameterName = "@AvailableAt";
            pAvail.Value = DateTime.UtcNow;
            cmd.Parameters.Add(pAvail);
            var pCreated = cmd.CreateParameter();
            pCreated.ParameterName = "@CreatedAt";
            pCreated.Value = DateTime.UtcNow;
            cmd.Parameters.Add(pCreated);
            var pUpdated = cmd.CreateParameter();
            pUpdated.ParameterName = "@UpdatedAt";
            pUpdated.Value = DateTime.UtcNow;
            cmd.Parameters.Add(pUpdated);
            var pAttemptCount = cmd.CreateParameter();
            pAttemptCount.ParameterName = "@AttemptCount";
            pAttemptCount.Value = 0;
            cmd.Parameters.Add(pAttemptCount);
            var pMaxAttempts = cmd.CreateParameter();
            pMaxAttempts.ParameterName = "@MaxAttempts";
            pMaxAttempts.Value = 1;
            cmd.Parameters.Add(pMaxAttempts);
            var pTimeout = cmd.CreateParameter();
            pTimeout.ParameterName = "@JobTimeoutSeconds";
            pTimeout.Value = 60;
            cmd.Parameters.Add(pTimeout);
            if (cmd is System.Data.Common.DbCommand dbCommand)
            {
                await dbCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue orphaned file cleanup job. TenantId={TenantId}, Paths={Paths}, CorrelationId={CorrelationId}",
                tenantId, string.Join(", ", uploadedPaths), correlationId);
        }
    }

    private static string ComputeRequestHash(BrandingUploadInput input)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (input.LogoData != null) sha.AppendData(input.LogoData);
        sha.AppendData(Encoding.UTF8.GetBytes(input.ColorsJson ?? string.Empty));
        if (input.FontsZipData != null) sha.AppendData(input.FontsZipData);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }

    private static string ComputeImmutableHash(Guid tenantId, Guid actorUserId, string action, string correlationId, DateTime timestamp)
    {
        var payload = $"{tenantId}|{actorUserId}|{action}|{correlationId}|{timestamp:O}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hashBytes);
    }

    [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex CssHexColorRegex();
}
