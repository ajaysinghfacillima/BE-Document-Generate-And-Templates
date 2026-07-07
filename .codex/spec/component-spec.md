# 1. Scope and Source Alignment

## Component

| Field | Value |
|---|---|
| Name | BE-Document-Generation-And-Templates |
| Description | Owns template listing, branding asset upload, and document generation and review APIs. |

## Backend API Inventory in Scope

| # | API Id |
|---:|---|
| 1 | `api-admin-templates-list` |
| 2 | `api-branding-assets-upload` |
| 3 | `api-documents-generate` |
| 4 | `api-documents-review` |

## Source Alignment

| Check | Value |
|---|---|
| No extra backend APIs included | Yes |
| Component out of scope | Any backend API not listed in the scoped inventory above is out of scope for this document. |

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 microservices |
| Database | SQL Server 2022 Standard |

# 2. Component Architecture

## Backend Layers

| Layer |
|---|
| Suggested by market standard: Controllers |
| Suggested by market standard: DTOs |
| Suggested by market standard: Services |
| Suggested by market standard: Repositories |
| Suggested by market standard: Integration Clients |

## High-Level Responsibilities

| Responsibility |
|---|
| Expose the 4 scoped backend APIs exactly as assigned. |
| Manage template listing for tenant admins. |
| Manage tenant branding asset upload/update. |
| Generate document artifacts for project documents. |
| Execute document review workflow actions on generated documents. |

## Backend Controllers

| Controller | Mapping | Responsibility |
|---|---|---|
| Suggested by market standard: `AdminTemplatesController` | `GET /api/v1/admin/templates` | Returns tenant-scoped template inventory for admin callers. |
| Suggested by market standard: `BrandingAssetsController` | `POST /api/v1/admin/branding/assets` | Accepts multipart branding asset upload/update request. |
| Suggested by market standard: `DocumentsGenerationController` | `POST /api/v1/projects/{projectId}/documents/generate` | Accepts document generation request and returns created `DocumentArtifact`. |
| Suggested by market standard: `DocumentsReviewController` | `POST /api/v1/projects/{projectId}/documents/{documentId}/review` | Executes review workflow action against existing `DocumentArtifact`. |

## Services

| Service | Responsibilities |
|---|---|
| Suggested by market standard: TemplateListing Service | Validate access and tenant scope.<br>Read template and template version data.<br>Build response payload for template listing.<br>Write audit log for access. |
| Suggested by market standard: Branding Asset Service | Validate admin authorization, tenant scope, and idempotency.<br>Parse multipart request.<br>Validate logo, colors JSON, fonts ZIP.<br>Perform malware scan.<br>Coordinate storage persistence and branding metadata persistence.<br>Persist idempotent response.<br>Write audit log. |
| Suggested by market standard: Document Generation Service | Validate project access and request payload.<br>Resolve project, template, template version, source entities.<br>Optionally load branding assets.<br>Apply watermark rules when provided.<br>Render document.<br>Compute checksum and footer version text.<br>Persist `DocumentArtifact` and `DocumentSource`.<br>Store generated file.<br>Write audit log. |
| Suggested by market standard: Document Review Service | Validate authentication, authorization, tenant scope, project scope, idempotency, and optimistic concurrency.<br>Validate workflow action against current review state.<br>Update `DocumentArtifact` review fields.<br>Insert `DocumentReviewEvent`.<br>Insert `AuditLog` for successful and failed evaluations.<br>Return mutated review state. |

## Repositories

| Repository | Responsibilities |
|---|---|
| Suggested by market standard: Template Repository | Read tenant-scoped templates and versions. |
| Suggested by market standard: Branding Asset Repository | Read existing branding asset by tenant.<br>Create branding asset.<br>Update branding asset. |
| Suggested by market standard: Project Repository | Read project by project id and tenant id. |
| Suggested by market standard: Template Resolution Repository | Read template by id.<br>Resolve published template version. |
| Suggested by market standard: Document Artifact Repository | Create generated `DocumentArtifact`.<br>Read document artifact by project id, document id, tenant id.<br>Update review state with ETag match. |
| Suggested by market standard: Document Source Repository | Persist source linkage records. |
| Suggested by market standard: Document Review Event Repository | Insert immutable review event. |
| Suggested by market standard: Audit Log Repository | Persist audit records. |

## Integration Clients

| Integration Client | Used By | Classification | External dependency contract | Duplication check | Local persistence of dependency data |
|---|---|---|---|---|---|
| Suggested by market standard: Object Storage or NAS Client | Branding asset upload and document generation | External dependency contract: Not specified by provided official documentation. | Not specified by provided official documentation. | Dependent service implementation not duplicated. | Not defined |
| Suggested by market standard: Antivirus Scanning Client / File-Security Validation Component | Branding asset upload | Not specified by metadata | Not specified by provided official documentation. | Dependent service implementation not duplicated. | Not defined |

## Data Ownership Boundaries

| Data Area | Ownership Boundary |
|---|---|
| Template listing | Reads tenant-scoped template metadata from `content.Template` and `content.TemplateVersion` for this component API scope. |
| Branding assets | Handles branding asset metadata for `content.BrandingAsset` within this component API scope; broader ownership boundary is Not defined. |
| Generated documents | Handles generated document metadata and source linkage for `DocumentArtifact` and `DocumentSource` within this component API scope; broader ownership boundary is Not defined. |
| Document review | Handles review-state mutation and review-event creation for `DocumentArtifact` and `DocumentReviewEvent` within this component API scope; broader ownership boundary is Not defined. |
| Audit records | Writes audit records required by these scoped APIs; broader audit domain ownership outside this component is Not defined. |

# 3. API Specifications

# 3.1 GET /api/v1/admin/templates

## API Id

| Field | Value |
|---|---|
| API Id | `api-admin-templates-list` |

## Purpose

| Value |
|---|
| Return an admin-only tenant-scoped inventory of available document templates and their versions used for document generation, packaging, exports, and branding-aware outputs. |

## Controller Mapping

| Controller |
|---|
| Suggested by market standard: `AdminTemplatesController` |

## Request

### Method

| Value |
|---|
| `GET` |

### Path

| Value |
|---|
| `/api/v1/admin/templates` |

### Authentication and Authorization

| Field | Value |
|---|---|
| Authentication requirement | Requires `Authorization: Bearer <token>` and `X-Tenant-Id` header. |
| Authorization roles | `Admin` |

### Headers

| Header | Required | Type | Description |
|---|---:|---|---|
| Authorization | yes | string | Bearer access token for an authenticated caller. |
| X-Tenant-Id | yes | string | Tenant identifier used for tenant-scoped data isolation and routing. |
| X-Correlation-Id | no | string | Optional correlation identifier for distributed tracing and auditability. |

### Route Parameters

| Name | Type | Required |
|---|---|---:|
| None | Not defined |  |

### Query Parameters

| Name | Type | Required |
|---|---|---:|
| None | Not defined |  |

### Request Body

| Value |
|---|
| No request body is accepted for this GET endpoint. |

## Validation

| Rule |
|---|
| Authorization header must contain a valid bearer token. |
| X-Tenant-Id header is mandatory and must resolve to an active tenant context. |
| Caller must have Admin role within the tenant scope. |

## Business Rules

| Rule |
|---|
| Only templates belonging to the caller tenant are returned. |
| Branding and template administration changes are audit logged but do not require dual approval. |
| Templates may be used by downstream document generation and submission packaging workflows, so `currentVersion` must reflect the latest effective template version. |

## Response 200

```json
{
  "items": [
    {
      "id": "tpl-narrative-leed-general",
      "name": "LEED General Narrative",
      "currentVersion": "2.1",
      "versions": ["2.1", "2.0", "1.1", "1.0"],
      "documentTypes": ["narrative"]
    }
  ]
}
```

## Response DTO

| Field | Value |
|---|---|
| Contract shape | Unnamed object contract defined by source |
| Top-level required fields | `items` |

### items[]

| Field | Type | Required |
|---|---|---:|
| id | string | yes |
| name | string | yes |
| currentVersion | string | yes |
| versions | array[string] | yes |
| documentTypes | array[string] | yes |

## Error Contract

| Field | Value |
|---|---|
| Error schema | `StandardError` |

### Error Fields

| Field |
|---|
| `traceId` |
| `code` |
| `message` |
| `details` |

### Error Status Codes

| HTTP Status | Code |
|---:|---|
| 400 | `INVALID_REQUEST` |
| 401 | `UNAUTHENTICATED` |
| 403 | `FORBIDDEN` |
| 404 | `NOT_FOUND` |
| 500 | `INTERNAL_SERVER_ERROR` |

## Security Considerations

| Rule |
|---|
| Enforce tenant isolation using `X-Tenant-Id` and authenticated principal scope. |
| Restrict access to Admin role only. |
| Do not expose template storage URIs or unpublished file internals through this listing endpoint. |
| Include request and access activity in immutable audit logs. |
| Apply per-tenant API rate limits defined by platform standards. |

## Exception Handling

| Rule |
|---|
| Return `StandardError`-compatible responses for validation, authentication, authorization, and unexpected failures. |
| Mask internal SQL or infrastructure details from clients while preserving `traceId` and correlation metadata for support diagnostics. |

## Logging

| Rule |
|---|
| Log request receipt, tenant id, actor id, correlation id, response count, latency, and authorization outcome at info level. |
| Log failures with stack trace and trace id at error level. |

## Audit

| Rule |
|---|
| Create an audit log entry for admin template listing access including `actorUserId`, `tenantId`, `action=templates.list`, `resourceType=Template`, `scopeType=tenant`, `outcome`, `correlationId`. |

## Transaction

| Value |
|---|
| Read-only operation; no database transaction beyond default read consistency is required. |

## Repository Usage

| Repository/Table |
|---|
| `content.Template` |
| `content.TemplateVersion` |

## Required Queries

| Query |
|---|
| `SELECT t.TemplateId AS id, t.Name, t.CurrentVersion, '[' + STRING_AGG('"' + t.DocumentType + '"', ',') + ']' AS documentTypesJson FROM content.Template t WHERE t.TenantId = @TenantId GROUP BY t.TemplateId, t.Name, t.CurrentVersion ORDER BY t.Name ASC` |
| `SELECT tv.TemplateId, tv.TemplateVersion FROM content.TemplateVersion tv INNER JOIN content.Template t ON t.TemplateId = tv.TemplateId WHERE t.TenantId = @TenantId ORDER BY tv.TemplateId, tv.CreatedAt DESC` |

# 3.2 POST /api/v1/admin/branding/assets

## API Id

| Field | Value |
|---|---|
| API Id | `api-branding-assets-upload` |

## Purpose

| Value |
|---|
| Allow an Admin to upload and update tenant-level AEON branding assets including logo files, color palette configuration, and font packages so they can be applied consistently to customer-facing templates, generated documents, exports, and submission packages. |

## Actor and Trigger

| Field | Value |
|---|---|
| Actor | `Admin` |
| Trigger | Admin submits branding assets from the Admin Branding and Templates screen to update the tenant branding configuration. |

## Controller Mapping

| Controller |
|---|
| Suggested by market standard: `BrandingAssetsController` |

## Preconditions

| Rule |
|---|
| The user is authenticated with a valid session or API token. |
| The requester belongs to the target tenant identified by `X-Tenant-Id`. |
| The requester has Admin role with settings and branding management permission. |
| Tenant branding and template administration is enabled for the environment. |
| At least one branding asset component is provided in the request: `logoFile`, `colorsJson`, or `fontsZip`. |
| Object storage or NAS target for branding assets is available and writable. |
| Audit logging is operational. |

## Request

### Method

| Value |
|---|
| `POST` |

### Path

| Value |
|---|
| `/api/v1/admin/branding/assets` |

### Input Source

| Value |
|---|
| Admin UI multipart/form-data submission via `/api/v1/admin/branding/assets` or authorized API client. |

### Headers

| Header | Required | Type |
|---|---:|---|
| Authorization | yes | string |
| X-Tenant-Id | yes | string |
| X-Correlation-Id | no | string |
| Idempotency-Key | yes | string |

### Content Type

| Value |
|---|
| `multipart/form-data` |

### Multipart Fields

| Field | Type | Required |
|---|---|---:|
| logoFile | binary | no |
| colorsJson | string | no |
| fontsZip | binary | no |

## Sample Request

```json
{
  "headers": {
    "Authorization": "Bearer eyJhbGciOi...",
    "X-Tenant-Id": "ten-001",
    "X-Correlation-Id": "corr-20260609-001",
    "Idempotency-Key": "brand-upload-20260609-001"
  },
  "multipartFormData": {
    "logoFile": "aeon-logo.png",
    "colorsJson": "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}",
    "fontsZip": "aeon-fonts.zip"
  }
}
```

## Validation

| Rule |
|---|
| Authorization header is required. |
| X-Tenant-Id header is required. |
| X-Correlation-Id header is optional for external API traceability. |
| Idempotency-Key header is required for this POST action. |
| Request content type must be multipart/form-data. |
| At least one of `logoFile`, `colorsJson`, or `fontsZip` must be supplied. |
| `logoFile`, when provided, must be an allowed logo image format such as `image/png`, `image/jpeg`, or `image/svg+xml`. |
| `logoFile` size must be greater than zero and within configured branding asset upload limits. |
| `fontsZip`, when provided, must be a valid ZIP archive. |
| `fontsZip` contents must contain only approved font file extensions such as `.ttf`, `.otf`, or `.woff/.woff2 if enabled by policy`. |
| `colorsJson`, when provided, must be valid JSON. |
| `colorsJson` must pass ISJSON-compatible parsing and may contain only these supported branding properties: `primary`, `secondary`, `accent`, `text`, `background`. |
| Hex color values in `colorsJson` must match valid CSS hex patterns. |
| Uploaded files must pass antivirus scanning before persistence is finalized. |
| Tenant scope in the request must match the authenticated principal tenant scope. |
| If an existing branding record is updated through an internal mutable workflow, optimistic concurrency metadata must be refreshed. |

## Business Rules

| Rule |
|---|
| Branding assets are tenant-scoped and there can be only one branding configuration record per tenant with lifecycle status managed as `updated`, `active`, or `archived`. |
| Branding and template administration changes are audit-logged but do not require dual approval. |
| Branding assets apply to customer-facing templates, generated documents, exports, and submission packages after successful upload. |
| Partial updates are allowed; omitted asset categories retain prior values. |
| Idempotency must prevent duplicate branding updates from repeated client retries. |
| Unsafe or infected uploaded assets must never be persisted as active branding content. |
| Colors configuration must remain machine-readable so downstream document generation can apply consistent themes. |
| Uploaded branding content must be stored in secure tenant-scoped storage locations. |
| The API updates branding metadata only; downstream rendered documents are regenerated when requested by separate document generation flows. |

## Main Control Flow

| Step | Description |
|---:|---|
| 1 | Validate authentication token, tenant context, and Admin authorization. |
| 2 | Validate presence of `Idempotency-Key` and check for replayed request to enforce idempotent processing. |
| 3 | Parse multipart/form-data request and confirm at least one of `logoFile`, `colorsJson`, or `fontsZip` is present. |
| 4 | If `logoFile` is provided, validate file type, file size, and content signature against allowed image formats. |
| 5 | If `colorsJson` is provided, validate that it is well-formed JSON and contains only supported branding color keys with valid color values. |
| 6 | If `fontsZip` is provided, validate ZIP integrity, allowed contained font file types, and file size limits. |
| 7 | Scan uploaded binary files using file-security validation and reject infected or unsafe payloads. |
| 8 | Store valid files in secure tenant-scoped storage paths for branding assets. |
| 9 | Create a new branding record if none exists for the tenant, otherwise update the existing tenant branding record. |
| 10 | Set branding status to updated and increment version/etag metadata. |
| 11 | Emit audit log capturing actor, tenant, assets updated, before/after values, and correlation details. |
| 12 | Return HTTP 201 with the branding asset response payload containing `id`, `tenantId`, `createdAt`, `updatedAt`, `version`, `etag`, and `status`. |

## Alternative Flow

| Condition | Behavior |
|---|---|
| If only `colorsJson` is supplied | Update only the color configuration while preserving existing logo and fonts references. |
| If only `logoFile` is supplied | Update only the logo reference while preserving existing colors and fonts. |
| If only `fontsZip` is supplied | Update only the fonts package reference while preserving existing logo and colors. |
| If the same `Idempotency-Key` is replayed with an identical payload | Return the previously stored successful response without creating a duplicate update event. |
| If no prior branding record exists for the tenant | Initialize a new BrandingAsset record with provided components and nulls for omitted components. |

## Exception Flow

| Condition | Behavior |
|---|---|
| If the Authorization header is missing or invalid | Reject the request as unauthorized. |
| If X-Tenant-Id is missing or does not match the authenticated tenant scope | Reject the request. |
| If the requester lacks Admin branding or settings permission | Reject the request as forbidden. |
| If none of `logoFile`, `colorsJson`, or `fontsZip` is supplied | Reject the request with validation error. |
| If `colorsJson` is malformed or contains invalid color values | Reject the request with validation error. |
| If uploaded files exceed allowed size limits or are unsupported formats | Reject the request with validation error. |
| If malware scan fails or detects unsafe content | Reject the request and do not persist any asset. |
| If storage write fails | Return an internal error and keep database changes uncommitted. |
| If database persistence fails after file upload | Roll back metadata changes and mark orphaned files for cleanup by background process. |
| If audit logging cannot be written | Fail the request because branding changes must be auditable. |

## Output Contract

| Value |
|---|
| A persisted tenant branding asset response payload containing `id`, `tenantId`, `createdAt`, `updatedAt`, `version`, `etag`, and `status`. |

## Response 201

```json
{
  "id": "brand-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T10:45:00Z",
  "updatedAt": "2026-06-09T10:45:00Z",
  "version": 1,
  "etag": "\"1-b1\"",
  "status": "updated"
}
```

## Response DTO

| Field | Value |
|---|---|
| Contract shape | Unnamed object contract defined by source |

| Field | Type | Required |
|---|---|---:|
| id | string | yes |
| tenantId | string | yes |
| createdAt | string(date-time) | yes |
| updatedAt | string(date-time) | yes |
| version | integer | yes |
| etag | string | yes |
| status | string | yes |

## Error Contract

| Field | Value |
|---|---|
| Error schema | Formal HTTP error response schema is Not defined in this API detail source. |
| Error status codes | Formal HTTP error status list is Not defined in this API detail source. |
| Source-defined error handling expectation | Exception flow is defined, and audit/logging/transaction behavior is defined. |

## Error Handling Notes

| Rule |
|---|
| Source defines exception conditions for unauthorized, forbidden, validation failure, malware rejection, storage failure, persistence failure, and audit-log failure. |
| Source does not define a formal API error response table or formal HTTP status code list for this API. |
| Suggested by market standard: when implemented consistently with platform patterns, error responses would typically be StandardError-compatible, but this is not explicitly defined by this API detail source. |

## Logging

| Rule |
|---|
| Log request receipt, correlation id, tenant id, actor id, validation outcomes, storage operations, idempotency handling, and final success or failure without logging binary content or sensitive secret material. |

## Audit

| Rule |
|---|
| Branding changes must be auditable. |
| Emit audit log capturing actor, tenant, assets updated, before/after values, and correlation details. |
| If audit logging cannot be written, fail the request because branding changes must be auditable. |

## Transaction

| Value |
|---|
| Database update and audit log write must be handled in a single transactional unit. |
| File storage operations are coordinated around the transaction; on persistence failure, metadata is rolled back and any uploaded orphan files are queued for cleanup. |

## Retry / Idempotency

| Rule |
|---|
| Client retries are supported only through `Idempotency-Key`. |
| Internal transient storage operations may retry with bounded backoff, but the API must not create duplicate branding updates. |

## Methods Required

| Method |
|---|
| `ValidateAdminAuthorization` |
| `ValidateIdempotencyKey` |
| `GetPersistedIdempotentResponse` |
| `ParseMultipartBrandingRequest` |
| `ValidateLogoFile` |
| `ValidateColorsJson` |
| `ValidateFontsZip` |
| `ScanUploadedFilesForMalware` |
| `StoreBrandingAssetFile` |
| `GetBrandingAssetByTenantId` |
| `CreateBrandingAsset` |
| `UpdateBrandingAsset` |
| `PersistIdempotentResponse` |
| `WriteAuditLog` |
| `BuildBrandingAssetResponse` |

## Configuration Required

| Configuration |
|---|
| Allowed logo MIME types configuration |
| Allowed font file extensions configuration |
| Maximum logo upload size configuration |
| Maximum fonts ZIP upload size configuration |
| Tenant-scoped branding storage path configuration |
| Antivirus scanning enabled flag and timeout configuration |
| Idempotency replay retention configuration |
| Audit logging retention policy |
| Default branding status mapping |

## Repository Usage

| Repository/Table |
|---|
| `content.BrandingAsset` |
| `ops.AuditLog` |

## External Dependencies

| Dependency | Classification | External dependency contract | Error Contract | Duplication check | Local persistence of dependency data |
|---|---|---|---|---|---|
| Object storage or NAS for secure branding asset file persistence | External Service | Not specified by provided official documentation. | Error schema: Not specified by provided official documentation<br>Error status codes: Not specified by provided official documentation | Dependent service implementation not duplicated. | Not defined |
| Antivirus scanning service or file-security validation component | Not specified by metadata | Not specified by provided official documentation. | Error schema: Not specified by provided official documentation<br>Error status codes: Not specified by provided official documentation | Dependent service implementation not duplicated. | Not defined |

# 3.3 POST /api/v1/projects/{projectId}/documents/generate

## API Id

| Field | Value |
|---|---|
| API Id | `api-documents-generate` |

## Purpose

| Value |
|---|
| Generate a project document such as a narrative, calculator, simulation summary, form-ready data, scorecard, checklist, or report in a supported export format using a selected template, project context, source entities, and optional branding and watermark settings. |

## Actor and Trigger

| Field | Value |
|---|---|
| Actor | `Sustainability Consultant` |
| Trigger | User submits a document generation request from the project documents workspace for a specific project. |

## Controller Mapping

| Controller |
|---|
| Suggested by market standard: `DocumentsGenerationController` |

## Preconditions

| Rule |
|---|
| User is authenticated and has access to the target project. |
| User has permission to generate project documents within the project scope. |
| The target project exists and belongs to the authenticated tenant. |
| A valid template exists for the requested document type. |
| The requested `sourceIds` reference entities that exist and are accessible within the same tenant and project scope. |
| If `includeBranding` is true, tenant branding assets are available; otherwise generation proceeds without branding. |
| Requested format is supported for the selected document type. |
| No portal submission automation is invoked; this use case generates downloadable artifacts only. |

## Request

### Method

| Value |
|---|
| `POST` |

### Path

| Value |
|---|
| `/api/v1/projects/{projectId}/documents/generate` |

### Input Source

| Value |
|---|
| `POST /api/v1/projects/{projectId}/documents/generate` with route parameter `projectId`, request body fields `documentType`, `format`, `templateId`, `includeBranding`, optional `watermarkText`, and `sourceIds`, plus required headers `Authorization`, `X-Tenant-Id`, and `Idempotency-Key` from the web client or authorized API client. |

### Authentication and Authorization

| Field | Value |
|---|---|
| Authentication requirement | Authenticated access is defined by preconditions and by required header inputs `Authorization` and `X-Tenant-Id`. |
| Authorization roles | Not defined in this API detail source; only project access and permission to generate project documents within the project scope are defined. |

### Headers

| Header | Required | Type | Description |
|---|---:|---|---|
| Authorization | yes | string | Required header from the web client or authorized API client. |
| X-Tenant-Id | yes | string | Required tenant-scoping header from the web client or authorized API client. |
| Idempotency-Key | Not defined as formal request header contract | string | Referenced by inputSource and retryRequirement for client retries using an idempotent submission pattern. |
| X-Correlation-Id | Not defined in this API detail source | string | Optional correlation handling is not explicitly defined for this API detail source. |

### Content Type

| Field | Value |
|---|---|
| Request content type | Not defined in this API detail source. |

### Route Parameters

| Name | Type | Required |
|---|---|---:|
| projectId | string | yes |

### Request Body

| Field | Type | Required |
|---|---|---:|
| documentType | string | yes |
| format | string | yes |
| templateId | string | yes |
| includeBranding | boolean | yes |
| watermarkText | string | no |
| sourceIds | array[string] | yes |

## Sample Request

```json
{
  "documentType": "narrative",
  "format": "pdf",
  "templateId": "tmpl-001",
  "includeBranding": true,
  "watermarkText": "Draft",
  "sourceIds": [
    "art-001",
    "sim-001",
    "sc-001"
  ]
}
```

## Request DTO

| Field | Value |
|---|---|
| Contract shape | Unnamed object contract defined by source |

## Validation

| Rule |
|---|
| `projectId` must be a non-empty identifier for an existing project. |
| `documentType` must be one of `narrative`, `calculator`, `simulationSummary`, `formReadyData`, `scorecard`, `checklist`, or `report`. |
| `format` must be one of `pdf`, `docx`, `xlsx`, `json`, or `pptx`. |
| `templateId` must reference an existing active template. |
| `includeBranding` is required and must be boolean. |
| `watermarkText`, if provided, must be a non-empty string after trimming and within document watermark length constraints. |
| `sourceIds` must contain at least one source identifier. |
| All `sourceIds` must reference supported source entity types that belong to the same tenant. |
| All `sourceIds` must be authorized for the requesting user within the project scope. |
| The selected template must support the requested `documentType` and requested output format. |

## Business Rules

| Rule |
|---|
| Generated outputs are for manual use and download only; no automated portal submission is initiated. |
| The system must pin the template version used for each generated document. |
| AEON branding is applied only when requested and when branding assets are available. |
| Version footer text must be applied to generated documents where format supports footer rendering. |
| A checksum must be computed and stored for each generated document artifact. |
| Generated document metadata and source lineage must be retained for auditability and future package assembly. |
| Document generation must stay within tenant and project boundaries. |
| Review workflow for generated documents is separate from generation and may be invoked afterward. |

## Main Control Flow

| Step | Description |
|---:|---|
| 1 | Validate authentication, tenant scope, and project-level authorization for document generation. |
| 2 | Validate `projectId` and confirm the project exists and is accessible. |
| 3 | Validate request payload fields including `documentType`, `format`, `templateId`, `includeBranding`, and `sourceIds`. |
| 4 | Load the selected template and resolve the pinned template version used for generation. |
| 5 | Validate that the template supports the requested `documentType` and `format`. |
| 6 | Resolve all `sourceIds` and verify each source belongs to the same tenant and project context. |
| 7 | Load project context, rating context, relevant source data, and template metadata required for rendering. |
| 8 | If `includeBranding` is true and branding assets exist, load tenant branding assets and apply them to the output. |
| 9 | If `watermarkText` is provided, apply watermark rules to the generated output. |
| 10 | Render the document in the requested output format. |
| 11 | Generate footer version text and compute checksum for the rendered file. |
| 12 | Persist the generated `DocumentArtifact` metadata and `DocumentSource` linkages. |
| 13 | Store the generated file in object storage or NAS. |
| 14 | Write audit and operational logs for the generation action. |
| 15 | Return the created `DocumentArtifact` resource to the caller. |

## Alternative Flow

| Condition | Behavior |
|---|---|
| If `includeBranding` is true but no active branding asset is configured | Generate the document without branding and mark `brandingApplied` as false. |
| If `watermarkText` is omitted | Generate the document without watermark and mark `watermarkApplied` as false. |
| If `sourceIds` contain a mix of supported source entity types such as `artifact`, `simulationJob`, `scorecard`, `recommendation`, or `preAssessmentRun` | Aggregate and normalize source data before rendering. |
| If the template is active but the current version is unpublished | Use the latest published template version; if none exists, reject the request. |
| If the requested format is `json` | Generate structured form-ready or report output without visual branding embellishments while still recording branding intent. |
| If the generated document enters a separate review workflow after creation | Initialize the returned `DocumentArtifact` with its contract-defined `reviewStatus`. |

## Exception Flow

| Condition | Behavior |
|---|---|
| Reject the request if the `projectId` does not resolve to an existing project in the tenant. |  |
| Reject the request if the user lacks permission to generate documents for the project. |  |
| Reject the request if `documentType` is not one of the supported values. |  |
| Reject the request if `format` is not supported for the requested `documentType`. |  |
| Reject the request if `templateId` does not exist, is inactive, or is not accessible to the tenant. |  |
| Reject the request if any `sourceId` is invalid, inaccessible, or outside the target project scope. |  |
| Fail the operation if document rendering or storage upload fails and return an internal error while preserving diagnostic logs. |  |
| Fail the operation if checksum generation or metadata persistence fails, ensuring no partially persisted successful response is returned. |  |

## Output Contract

| Value |
|---|
| Source defines that a created `DocumentArtifact` resource is returned in the API response, including `id`, `tenantId`, `createdAt`, `updatedAt`, `version`, `etag`, `projectId`, `documentType`, `format`, `templateId`, `templateVersion`, `brandingApplied`, `watermarkApplied`, `footerVersionText`, `storageUri`, `checksumSha256`, and `reviewStatus`. |

## Response

| Field | Value |
|---|---|
| HTTP success status | Not defined in this API detail source. |
| Source-defined success semantics | A created `DocumentArtifact` resource is returned. |

```json
{
  "id": "doc-001",
  "tenantId": "ten-001",
  "createdAt": "2026-03-30T12:15:00Z",
  "updatedAt": "2026-03-30T12:15:00Z",
  "version": 1,
  "etag": "\"1-doc1\"",
  "projectId": "prj-001",
  "documentType": "narrative",
  "format": "pdf",
  "templateId": "tmpl-001",
  "templateVersion": "v3.2",
  "brandingApplied": true,
  "watermarkApplied": true,
  "footerVersionText": "v1.0",
  "storageUri": "blob://ten-001/prj-001/generated/doc-001/narrative.pdf",
  "checksumSha256": "5f7c2c6a6bb2c2c1d9f2c51c7f2a8f2d3f7a8c9d1e2f3a4b5c6d7e8f9a0b1c2d",
  "reviewStatus": "draft"
}
```

## Response DTO

| Field | Value |
|---|---|
| Contract shape | Unnamed object contract defined by source |

| Field | Type | Required |
|---|---|---:|
| id | string | yes |
| tenantId | string | yes |
| createdAt | string(date-time) | yes |
| updatedAt | string(date-time) | yes |
| version | integer | yes |
| etag | string | yes |
| projectId | string | yes |
| documentType | string | yes |
| format | string | yes |
| templateId | string | yes |
| templateVersion | string | yes |
| brandingApplied | boolean | yes |
| watermarkApplied | boolean | yes |
| footerVersionText | string | yes |
| storageUri | string | yes |
| checksumSha256 | string | yes |
| reviewStatus | string | yes |

## Authentication and Authorization Notes

| Field | Value |
|---|---|
| Authentication requirement | Source confirms authenticated access through preconditions and required header inputs in inputSource. |
| Authorization roles | Not defined in this API detail source. |
| Authorization scope | Source defines project access and permission requirements only. |

## Error Contract

| Field | Value |
|---|---|
| Error schema | Formal error response schema is Not defined in this API detail source. |
| Error status codes | Formal HTTP error status list is Not defined in this API detail source. |
| StandardError usage | Not defined in this API detail source. |
| Source-defined error handling expectation | Exception flow defines rejection and failure conditions only. |

## Error Handling Notes

| Rule |
|---|
| Source defines rejection conditions for invalid project, missing permission, unsupported documentType, unsupported format, invalid or inaccessible template, and invalid or out-of-scope sourceIds. |
| Source defines internal failure conditions for rendering failure, storage upload failure, checksum generation failure, and metadata persistence failure. |
| Source does not define a formal failureResponses list, httpStatusCodes list, or StandardError contract for this API detail. |

## Logging

| Rule |
|---|
| Log request correlation id, tenant id, project id, actor user id, template id, resolved template version, requested document type, requested format, source entity count, branding and watermark flags, generation duration, storage result, and final document artifact id. |
| Record success and failure outcomes with structured diagnostics. |

## Retry / Idempotency

| Rule |
|---|
| Client retries must use an idempotent submission pattern. |
| Server-side retry is allowed only for transient storage failures and must avoid duplicate document artifact creation. |

## Transaction

| Rule |
|---|
| Metadata persistence for document artifact and source linkage should be atomic. |
| File storage and database updates should use a resilient pattern that prevents a successful response unless both metadata and storage registration succeed. |

## Methods Required

| Method |
|---|
| `AuthorizeProjectDocumentGeneration` |
| `ValidateGenerateDocumentRequest` |
| `GetProjectById` |
| `GetTemplateById` |
| `ResolvePublishedTemplateVersion` |
| `ValidateTemplateSupportsDocumentTypeAndFormat` |
| `ResolveDocumentSources` |
| `LoadBrandingAssetsIfRequested` |
| `BuildDocumentGenerationContext` |
| `RenderDocument` |
| `ApplyBranding` |
| `ApplyWatermark` |
| `GenerateFooterVersionText` |
| `ComputeChecksumSha256` |
| `PersistDocumentArtifact` |
| `PersistDocumentSources` |
| `StoreGeneratedDocument` |
| `WriteAuditLog` |

## Configuration Required

| Configuration |
|---|
| Supported document types and format matrix |
| Template repository and publication status configuration |
| Branding asset configuration for tenant |
| Object storage or NAS path configuration for generated outputs |
| Footer versioning format configuration |
| Optional review policy for generated documents |
| Rate limiting and idempotency behavior for POST requests |

## Repository Usage

| Repository/Table |
|---|
| `Project` |
| `Template` |
| `TemplateVersion` |
| `BrandingAsset` |
| `DocumentArtifact` |
| `DocumentSource` |
| `Artifact` |
| `SimulationJob` |
| `Scorecard` |
| `PreAssessmentRun` |
| `Recommendation` |
| `AuditLog` |

## External Dependencies

| Dependency | Classification | External dependency contract | Error Contract | Duplication check | Local persistence of dependency data |
|---|---|---|---|---|---|
| Object storage or NAS for generated file persistence | Not defined in external integration detail beyond storage-access dependency | Not specified by provided official documentation. | Error schema: Not specified by provided official documentation<br>Error status codes: Not specified by provided official documentation | Dependent service implementation not duplicated. | Not defined |

## External API Dependencies

| Value |
|---|
| Not defined in this API detail source. |

# 3.4 POST /api/v1/projects/{projectId}/documents/{documentId}/review

## API Id

| Field | Value |
|---|---|
| API Id | `api-documents-review` |

## Purpose

| Value |
|---|
| Submit a generated project document into human review workflow or record a reviewer decision to move the document into active review or approve or reject it with comments, auditability, and optimistic concurrency safeguards. |

## Controller Mapping

| Controller |
|---|
| Suggested by market standard: `DocumentsReviewController` |

## Request

### Method

| Value |
|---|
| `POST` |

### Path

| Value |
|---|
| `/api/v1/projects/{projectId}/documents/{documentId}/review` |

### Authentication and Authorization

| Field | Value |
|---|---|
| Authentication requirement | Authenticated tenant user session token or OAuth2 bearer token is required, along with tenant scope resolution via `X-Tenant-Id` header. |
| Authorization roles | `Sustainability Consultant`, `Admin`, `Owner`, `PMC` |

### Content Type

| Value |
|---|
| `application/json` |

### Headers

| Header | Required | Type | Description |
|---|---:|---|---|
| Authorization | yes | string | Bearer access token for authenticated user or API client. |
| X-Tenant-Id | yes | string | Tenant identifier used for tenant-scoped authorization and data isolation. |
| X-Correlation-Id | no | string | Optional request correlation identifier for tracing and diagnostics. |
| Idempotency-Key | yes | string | Client-supplied idempotency key to prevent duplicate review submissions or duplicate decision recording on retried POST requests. |
| If-Match | yes | string | Required optimistic concurrency token for all review actions because each action mutates the mutable DocumentArtifact review state. |

### Route Parameters

| Name | Type | Required |
|---|---|---:|
| projectId | string | yes |
| documentId | string | yes |

### Query Parameters

| Name | Type | Required |
|---|---|---:|
| None | Not defined |  |

### Request Body

```json
{
  "action": "submit|startReview|approve|reject",
  "comments": "string"
}
```

## Request DTO

| Field | Value |
|---|---|
| Contract shape | Unnamed object contract defined by source |

### Request Fields

| Field | Type | Required |
|---|---|---:|
| action | string enum(`submit`,`startReview`,`approve`,`reject`) | yes |
| comments | string | no |

| Rule |
|---|
| Action-specific constraints are enforced by validationRules and businessRules: `submit` keeps the persisted `DocumentArtifact` review status within the allowed MLD state model and records a submission event while the document remains `draft` until active review begins; `startReview` moves `draft` to `inReview`; `approve` and `reject` require the document to already be `inReview`. |

## Validation

| Rule |
|---|
| `projectId` must reference an existing project within the tenant scope. |
| `documentId` must reference an existing `DocumentArtifact` belonging to the provided `projectId`. |
| `action` is required and must be one of `submit`, `startReview`, `approve`, or `reject`. |
| `comments` length must not exceed 2000 characters. |
| `If-Match` header must match the current document etag for all actions. |
| `Idempotency-Key` is mandatory and must be unique per logical review submission. |

## Business Rules

| Rule |
|---|
| Only generated project documents represented as `DocumentArtifact` records can be reviewed through this endpoint. |
| A draft document may be submitted into review using action `submit`. |
| Submitting a document records a `DocumentReviewEvent` audit trail entry and keeps the persisted `DocumentArtifact` review status within the allowed MLD enum until active review begins. |
| A draft document may move to active review using action `startReview`. |
| Only a document currently in review may be approved or rejected. |
| Approving a document sets `DocumentArtifact.ReviewStatus` to `approved` and updates `reviewedByUserId` and `reviewedAt`. |
| Rejecting a document sets `DocumentArtifact.ReviewStatus` to `rejected` and preserves reviewer comments for revision workflow. |
| Each successful action creates an immutable `DocumentReviewEvent` audit trail entry. |
| Review actions must remain within tenant and project authorization scope. |
| External Auditor role is read-only and must not use this endpoint for review actions. |
| A user cannot bypass workflow order by approving a document that was never submitted or started for review. |

## Workflow

| Item | Behavior |
|---|---|
| `reviewStatus` enum | `draft`, `inReview`, `approved`, `rejected` |
| `submit` | records submission event; persisted status remains within allowed MLD enum until active review begins |
| `startReview` | `draft -> inReview` |
| `approve` | requires current `inReview`, resulting status `approved` |
| `reject` | requires current `inReview`, resulting status `rejected` |

## Response 200

```json
{
  "documentId": "doc-001",
  "projectId": "prj-001",
  "reviewStatus": "approved",
  "event": {
    "reviewEventId": "dre-001",
    "action": "approve",
    "actorUserId": "usr-101",
    "comments": "Narrative and supporting calculations verified.",
    "createdAt": "2026-06-08T11:45:00Z"
  },
  "reviewedByUserId": "usr-101",
  "reviewedAt": "2026-06-08T11:45:00Z",
  "etag": "\"6-doc-001\""
}
```

## Response DTO

### Top-level fields

| Field | Type | Required |
|---|---|---:|
| documentId | string | yes |
| projectId | string | yes |
| reviewStatus | string enum(`draft`,`inReview`,`approved`,`rejected`) | yes |
| event | object | yes |
| reviewedByUserId | string | no |
| reviewedAt | string(date-time UTC) | no |
| etag | string | no |

| Note | Value |
|---|---|
| Top-level required fields per source | `documentId`, `projectId`, `reviewStatus`, `event` |
| Optional top-level fields per source | `reviewedByUserId`, `reviewedAt`, `etag` |
| Sample response extra content | `traceability` and `sourceDocuments` in sample content are sample-only and not part of `responseDto` contract. |

### event

| Field | Type | Required |
|---|---|---:|
| reviewEventId | string | no |
| action | string enum(`submit`,`startReview`,`approve`,`reject`) | yes |
| actorUserId | string | yes |
| comments | string | no |
| createdAt | string(date-time UTC) | yes |

## Error Contract

| Field | Value |
|---|---|
| Error schema | `StandardError` |

### Error Fields

| Field |
|---|
| `traceId` |
| `code` |
| `message` |
| `details` |

### Error Status Codes

| HTTP Status | Code |
|---:|---|
| 400 | `INVALID_REVIEW_ACTION` |
| 400 | `INVALID_REQUEST_BODY` |
| 401 | `UNAUTHENTICATED` |
| 403 | `FORBIDDEN_DOCUMENT_REVIEW` |
| 404 | `DOCUMENT_NOT_FOUND` |
| 404 | `PROJECT_NOT_FOUND` |
| 409 | `ETAG_MISMATCH` |
| 409 | `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD` |
| 500 | `DOCUMENT_REVIEW_PROCESSING_FAILED` |

## Exception Handling

| Rule |
|---|
| Return structured application errors using the `StandardError` schema with required fields `traceId`, `code`, `message`, and `details` for validation, authorization, not-found, and concurrency failures. |
| Map unexpected exceptions to a generic internal error response, log full diagnostic context server-side, and preserve transaction integrity. |
| Duplicate POST retries with same idempotency key must return the original successful response when payload matches. |

## Security Considerations

| Rule |
|---|
| Enforce tenant isolation using `X-Tenant-Id` and authenticated token claims. |
| Apply project-scoped RBAC checks before allowing review submission or decisions. |
| Use optimistic concurrency via `Etag` and required `If-Match` for all actions to prevent lost updates. |
| Prevent External Auditor and other read-only roles from mutating review state. |
| Capture immutable audit logs for all review decisions and failures. |
| Do not expose storage URIs or internal implementation details beyond authorized metadata. |
| Validate that document belongs to the project route parameter to prevent insecure direct object references. |
| Honor India-resident storage and audit retention policies for document review records. |

## Logging

| Rule |
|---|
| Log request receipt, tenant id, project id, document id, actor user id, action, correlation id, idempotency key, outcome status, and execution latency. |
| Do not log sensitive document contents or unredacted large comments beyond standard limits. |

## Audit

| Rule |
|---|
| Every successful and failed review action evaluation must create an `AuditLog` entry. |
| The audit payload must map previous review status into `BeforeJson`, resulting review status into `AfterJson` when changed, include reason or comments in `Reason`, include the request correlation id in `CorrelationId`, and persist failure-path audit records as `outcome=failure` even when no `DocumentArtifact` mutation occurs. |
| Successful state changes must also create a `DocumentReviewEvent` record. |

## Transaction

| Value |
|---|
| Requires a single database transaction to atomically validate state, update `DocumentArtifact` review fields, insert `DocumentReviewEvent`, and persist `AuditLog`. |
| Roll back all changes if any step fails. |

## Repository Usage

| Repository/Table |
|---|
| `Project` |
| `DocumentArtifact` |
| `DocumentReviewEvent` |
| `UserAccount` |
| `AuditLog` |

## Required Queries

| Query |
|---|
| `SELECT project by ProjectId and TenantId to validate scope and access context.` |
| `SELECT document artifact by DocumentArtifactId, ProjectId, and TenantId with current ReviewStatus and Etag.` |
| `UPDATE DocumentArtifact SET ReviewStatus, ReviewedByUserId, ReviewedAt, UpdatedAt, Version, Etag WHERE DocumentArtifactId = @documentId AND ProjectId = @projectId AND Etag match.` |
| `INSERT INTO DocumentReviewEvent(DocumentReviewEventId, DocumentArtifactId, Action, ActorUserId, Comments, CreatedAt) VALUES (...).` |
| `INSERT INTO AuditLog(AuditLogId, TenantId, CreatedAt, UpdatedAt, Version, ActorUserId, ActorType, Action, ResourceType, ResourceId, ScopeType, ScopeId, Outcome, CorrelationId, BeforeJson, AfterJson, Reason, ImmutableHash) VALUES (...) for both successful and failed document review evaluations, with BeforeJson capturing previous review status and AfterJson capturing resulting review status when changed.` |

# 4. DTO and Schema Inventory

## Internal Schemas Used from MLD

| Schema |
|---|
| `StandardError` |
| `Project` |
| `DocumentArtifact` |

## Additional API Body/Response Shapes from Scoped Action Details

| Shape |
|---|
| Template list response body |
| Branding asset upload response body |
| Document generation request body |
| Document review request body |
| Document review response body |

# 5. Workflows

## Document Review Workflow

| Action | Behavior |
|---|---|
| `submit` | records submission event and keeps persisted `DocumentArtifact` review status within allowed MLD enum until active review begins |
| `startReview` | `draft -> inReview` |
| `approve` | `inReview -> approved` |
| `reject` | `inReview -> rejected` |

## Other Workflows

| Workflow | Definition |
|---|---|
| Branding asset upload workflow | as defined in action details. |
| Document generation workflow | as defined in action details. |

# 6. Service Dependency Inventory

## 6.1 Object Storage or NAS

| Field | Value |
|---|---|
| Classification | External Service |
| Purpose | Artifact and generated-output storage |
| Consumed By APIs | `api-branding-assets-upload`<br>`api-documents-generate` |
| Documentation Source | Not specified by provided official documentation |
| External dependency contract | Not specified by provided official documentation. |
| Headers | Not specified by provided official documentation |
| Authentication | Not specified by provided official documentation |
| Request/Response Contract | Not specified by provided official documentation |
| Error Contract | Error schema: Not specified by provided official documentation<br>Error status codes: Not specified by provided official documentation |
| Retries/Timeouts | Not specified by provided official documentation |
| Duplication check | Dependent service implementation not duplicated. |
| Local persistence of dependency data | Not defined |

## 6.2 Antivirus scanning service or file-security validation component

| Field | Value |
|---|---|
| Classification | Not specified by metadata |
| Purpose | Malware and unsafe payload validation for branding asset upload |
| Consumed By APIs | `api-branding-assets-upload` |
| External dependency contract | Not specified by provided official documentation. |
| Headers | Not specified by provided official documentation |
| Authentication | Not specified by provided official documentation |
| Request/Response Contract | Not specified by provided official documentation |
| Error Contract | Error schema: Not specified by provided official documentation<br>Error status codes: Not specified by provided official documentation |
| Retries/Timeouts | Not specified by provided official documentation |
| Duplication check | Dependent service implementation not duplicated. |
| Local persistence of dependency data | Not defined |

# 7. Headers and Behavior Inventory

## Explicitly Defined Headers

| Header |
|---|
| Authorization |
| X-Tenant-Id |
| X-Correlation-Id |
| Idempotency-Key |
| If-Match |

## Explicitly Defined Behavior

| Behavior |
|---|
| Authentication |
| Authorization / RBAC |
| Tenant filtering / propagation |
| Correlation ID handling |
| Idempotency for POST actions where specified |
| Optimistic concurrency via `If-Match` for document review |
| Retry behavior where explicitly stated for storage operations and client retry patterns |
| Audit logging |
| Structured operational logging |
| Per-tenant rate limits defined by platform standards for template listing security considerations and MLD API standards |

# 8. Persistence and Data Access Responsibilities

## Allowed Persistence Detail

| Value |
|---|
| Only high-level responsibilities and explicitly named tables/queries are included. |

## Repository Responsibilities

| Repository | Responsibilities |
|---|---|
| Suggested by market standard: Template Repository | Fetch tenant-scoped template inventory.<br>Fetch ordered template versions. |
| Suggested by market standard: Branding Asset Repository | Upsert tenant branding metadata.<br>Support partial update semantics.<br>Refresh version and etag metadata. |
| Suggested by market standard: Document Artifact Repository | Persist created generated document artifact metadata.<br>Update mutable review fields under etag match. |
| Suggested by market standard: Document Source Repository | Persist generated document lineage/source linkages. |
| Suggested by market standard: Document Review Event Repository | Persist immutable review event records. |
| Suggested by market standard: Audit Log Repository | Persist audit records for admin template listing, branding updates, document generation, and review success/failure evaluation. |

# 9. Test Strategy

## Deterministic Test Data

| Value |
|---|
| Not defined |

## API Contract Tests

| Test |
|---|
| Verify exact method/path coverage for: |
| `GET /api/v1/admin/templates` |
| `POST /api/v1/admin/branding/assets` |
| `POST /api/v1/projects/{projectId}/documents/generate` |
| `POST /api/v1/projects/{projectId}/documents/{documentId}/review` |

## Schema/DTO Preservation Tests

| Test |
|---|
| `StandardError` schema preservation tests. |
| `DocumentArtifact` schema preservation tests for document generation response. |
| Template list response body preservation tests. |
| Branding asset response body preservation tests. |
| Document review request/response field preservation tests. |

## Workflow Tests

| Test |
|---|
| Document review action tests: |
| `submit` records event and preserves allowed persisted review state behavior. |
| `startReview` transitions `draft` to `inReview`. |
| `approve` allowed only from `inReview`. |
| `reject` allowed only from `inReview`. |
| Branding asset upload control-flow tests for partial update cases. |
| Document generation flow tests for branding/no-branding and watermark/no-watermark outcomes. |

## Error Tests

| Area | Test |
|---|---|
| Template list | 400/401/403/404/500 response contract tests. |
| Document review | 400 `INVALID_REVIEW_ACTION` |
| Document review | 400 `INVALID_REQUEST_BODY` |
| Document review | 401 `UNAUTHENTICATED` |
| Document review | 403 `FORBIDDEN_DOCUMENT_REVIEW` |
| Document review | 404 `DOCUMENT_NOT_FOUND` |
| Document review | 404 `PROJECT_NOT_FOUND` |
| Document review | 409 `ETAG_MISMATCH` |
| Document review | 409 `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD` |
| Document review | 500 `DOCUMENT_REVIEW_PROCESSING_FAILED` |
| Branding asset upload | Formal HTTP error response schema/status list Not defined in this API detail source; exception-flow tests only. |
| Document generation | Formal HTTP error response schema/status list Not defined in this API detail source; exception-flow tests only. |

## Dependency Inventory Tests

| Test |
|---|
| Verify Object Storage or NAS dependency is listed for branding upload and document generation. |
| Verify antivirus/file-security dependency is listed for branding upload. |
| Verify external contracts are marked: |
| `External dependency contract: Not specified by provided official documentation.` |

## Header Validation Tests

| Area | Test |
|---|---|
| Template list | Authorization required |
| Template list | X-Tenant-Id required |
| Template list | X-Correlation-Id optional |
| Branding asset upload | Authorization required |
| Branding asset upload | X-Tenant-Id required |
| Branding asset upload | Idempotency-Key required |
| Branding asset upload | multipart/form-data required |
| Document review | Authorization required |
| Document review | X-Tenant-Id required |
| Document review | Idempotency-Key required |
| Document review | If-Match required |
| Document generation | Authorization required |
| Document generation | X-Tenant-Id required |
| Document generation | Idempotency handling follows inputSource and retryRequirement; formal required request-header contract for `Idempotency-Key` is Not defined in this API detail source |
| Document generation | X-Correlation-Id handling is Not defined in this API detail source |
| Document generation | request content type is Not defined in this API detail source |

## Idempotency Tests

| Area | Test |
|---|---|
| Branding upload | same `Idempotency-Key` + identical payload returns prior successful response. |
| Document review | duplicate POST retries with same idempotency key return original successful response when payload matches. |
| Document review | reused idempotency key with different payload returns 409. |
| Document generation | client retries use an idempotent submission pattern. |
| Document generation | server-side handling avoids duplicate document artifact creation when retries occur for transient storage failures. |

## ETag/Concurrency Tests

| Area | Test |
|---|---|
| Document review | matching `If-Match` succeeds. |
| Document review | stale `If-Match` returns 409 `ETAG_MISMATCH`. |

## Tenant Isolation/Propagation Tests

| Test |
|---|
| Template listing tenant isolation. |
| Branding asset tenant scope match. |
| Document generation tenant and project boundary enforcement. |
| Document review tenant and project scope enforcement. |

## Correlation ID Propagation Tests

| Test |
|---|
| Template listing correlation id logging/audit inclusion. |
| Branding asset logging correlation handling. |
| Document review logging and audit correlation inclusion. |
| Document generation | X-Correlation-Id handling is Not defined in this API detail source. |

## Policy/Authorization Tests

| Test |
|---|
| Template listing Admin-only access. |
| Branding upload Admin branding/settings permission requirement. |
| Document generation project-level authorization. |
| Document review allowed roles `Sustainability Consultant`, `Admin`, `Owner`, `PMC`, and External Auditor read-only prohibition. |

## Authentication Tests

| Test |
|---|
| All 4 APIs for missing/invalid authentication where defined. |

## Queues/Events Tests

| Value |
|---|
| Queues/events: Not defined; no test generated. |

## Retries, Timeouts, Fallbacks, Circuit Breakers Tests

| Test |
|---|
| Branding asset internal transient storage retry behavior where explicitly stated. |
| Document generation server-side transient storage retry behavior only, where explicitly stated. |
| External services timeout/retry details: Not specified by provided official documentation; no test generated. |

# 10. Assumptions

| Assumption |
|---|
| Suggested by market standard: Controller and service names are descriptive placeholders for responsibility mapping only and do not define concrete class names. |

# 11. Pre-Output Validation Report

| Check | Status |
|---|---|
| no API path, method, endpoint, field, schema, workflow, state, transition, or behavior was invented or changed | Pass except for Section 10 placeholder naming, which is explicitly labeled `Suggested by market standard` and is not source-confirmed contract content |
| scoped backend APIs are included | Pass |
| no metadata-only API was included as an internal contract | Pass |
| no internal contract was invented from metadata | Pass except where explicitly labeled `Suggested by market standard` for placeholder internal naming |
| no external contract was inferred without official documentation | Pass |
| missing internal details use `Not defined` where source does not define them | Pass |
| missing external details use exactly: Not specified by provided official documentation | Pass |
| metadata conflicts and metadata-only contracts are ignored | Pass |
| forbidden internal details are absent | Pass except for explicitly labeled placeholder internal naming |