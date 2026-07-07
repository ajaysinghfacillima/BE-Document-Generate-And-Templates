# Mid-Level Design

# Document Control

| Field | Value |
| --- | --- |
| Document Type | Mid-Level Design |
| Based On | Approved High-Level Design Version 1.0 dated 06-06-2026 |
| Scope | Implementation-ready elaboration of HLD requirements covered by this document |
| Intent Preservation | No HLD API, field, schema, deployment intent, or technology choice has been changed; only additive implementation detail is provided |
| Version | 1.0 |
| Status | Draft |
| Date | 09-06-2026 |

# Overview

This Mid-Level Design defines the implementation architecture for AEON’s AI-assisted Green Building Certification platform. It elaborates the HLD into:

| Area | Elaboration |
| --- | --- |
| 1 | component architecture and interactions |
| 2 | internal and external interfaces |
| 3 | data contracts and entity schemas covered in this document |
| 4 | representative and implementation-defining REST API contracts covered in this document |
| 5 | workflow state models |
| 6 | deployment architecture |
| 7 | operational, security, compliance, and scaling design |

The design preserves:

| Preserved Item | Value |
| --- | --- |
| Hosting pattern | microservices on Windows VMs |
| Frontend | React and TypeScript |
| Backend | .NET 10 backend |
| Database | SQL Server 2022 Standard |
| Search | self-hosted Elasticsearch |
| OCR | Tesseract OCR |
| AI Access | self-hosted India AI Gateway using OpenAI GPT 5.1 only |
| Queueing and Async | SQL-backed queues with Windows worker services |
| Packaging | manual portal packaging only at MVP |
| Portal automation | no RPA and no live portal integrations at MVP |
| Residency | India-resident data, logs, backups, and DR artifacts |

# Assumptions

| Assumption | Value |
| --- | --- |
| API root | All public REST APIs are rooted at `/api/v1`. |
| Time format | Public API wire format uses ISO 8601 UTC strings; UI presentation may still apply Indian date and number formats and configured locale-specific display rules. |
| Concurrency | All mutable public resources expose `etag` in response body and HTTP `ETag` header. |
| Common mutable fields | All tenant-scoped mutable entities include `id`, `tenantId`, `createdAt`, `updatedAt`, `version`, and `etag`. |
| Realtime | SignalR is the default real-time channel over WSS on port 443 with SSE fallback. |
| Queue implementation | SQL-backed queues are implemented in SQL Server queue tables with worker polling, leases, retries, and dead-letter handling. |
| Business day logic | Business day calculations use tenant-configured business calendar defaults, with project-level override where configured. |
| WhatsApp usage | WhatsApp is used only to send workflow and operational notifications, not authentication. |
| Revit handling | Revit files are accepted for reference only at MVP; extraction is from IFC and exported schedules. |
| Fire and smoke simulation | Fire and smoke simulation workflows remain out of scope at MVP even if Pyrosim connectivity exists. |
| Thermal comfort | Full native thermal comfort simulation remains out of scope; imported or derived outputs only. |
| Future integrations | Future portal APIs and future RPA capabilities remain disabled by feature flags and policy at MVP. |
| Deployment preference | Single-tenant SaaS is the default deployment preference; multi-tenant SaaS is additionally supported. |
| Out of scope at MVP | Automated portal submission, RPA for portal operations, live official portal API integrations, native mobile applications, full native lifecycle carbon automation, fire or smoke simulation workflows, full thermal comfort simulation beyond imported or derived outputs, WhatsApp-based authentication flows, legal hold as an end-user feature, external BI tooling, and SDKs remain out of scope. |

# Tech Stack

This MLD inherits the HLD technology stack without changes.

| Layer | Technology |
| --- | --- |
| Frontend | React + TypeScript |
| Backend | .NET 10 microservices |
| Database | SQL Server 2022 Standard |
| Search | Self-hosted Elasticsearch |
| OCR | Tesseract OCR |
| AI Access | Self-hosted India AI Gateway with OpenAI GPT 5.1 as the approved provider |
| Queueing and Async | SQL-backed queues + Windows worker services |
| Realtime | SignalR over WSS with SSE fallback |
| Secrets Protection | Windows DPAPI |
| Hosting | Windows VMs |
| Email Provider | SendGrid |
| WhatsApp Provider | Meta Cloud API |

# Scope Realization

The platform implements the following functional capability areas from the HLD:

| Capability Area | Included |
| --- | --- |
| project intake and project master data | yes |
| rating library and regional profiles | yes |
| AI-assisted pre-assessment | yes |
| credit interpretation and applicability | yes |
| artifact ingestion, OCR, BIM extraction, classification, repository search | yes |
| evidence workflows and reminders | yes |
| simulation orchestration and ingestion | yes |
| dynamic scorecards and what-if analysis | yes |
| recommendations and corrective actions | yes |
| narratives, calculators, form-ready outputs | yes |
| packaging for manual portal upload | yes |
| auditor Q and A | yes |
| standards and CIR Q and A | yes |
| notifications | yes |
| analytics and exports | yes |
| admin, governance, retention, branding | yes |
| REST APIs and webhooks | yes |
| SaaS and on-prem deployment modes | yes |

# Component Architecture

## Logical Components

| Component |
| --- |
| Web Client Application |
| API Gateway |
| SignalR Hub |
| Identity and RBAC Service |
| Project Service |
| Project Intake Service |
| Rating Library Service |
| Region Profile Service |
| Pre-assessment Service |
| Credit Interpretation Service |
| Evidence Workflow Service |
| Ingestion Service |
| OCR and Extraction Service |
| BIM Extraction Service |
| Classification Service |
| Search Index Service |
| Scorecard Service |
| Recommendation Service |
| Simulation Orchestrator Service |
| Narrative and Document Generation Service |
| Packaging Service |
| Auditor Q and A Service |
| Standards and CIR Q and A Service |
| Notification Service |
| Analytics Service |
| Admin and Governance Service |
| Audit and Explainability Service |
| Historical Data Ingestion Service |
| License Management Service |
| Background Worker Services |
| AI Gateway |
| SQL Server |
| Elasticsearch |
| Object Storage or NAS |
| Observability and Logging Stack |

## Component Responsibilities

### Web Client Application

| Responsibility |
| --- |
| renders role-based UI |
| supports project workflows, dashboards, notifications, explainability views |
| invokes APIs via API Gateway |
| subscribes to SignalR events |
| supports English India with SI and Imperial units at launch |
| applies client-side validation only as convenience; server is authoritative |

### API Gateway

| Responsibility |
| --- |
| single public ingress |
| validates tokens |
| applies rate limits per tenant |
| routes requests to services |
| handles request correlation ids |
| enforces IP allowlisting where configured |
| exposes webhook management endpoints |
| exposes SignalR negotiation endpoint |
| runs on IIS with YARP for routing and ingress |

### SignalR Hub

| Responsibility |
| --- |
| provides real-time event delivery over WSS on port 443 |
| supports SSE fallback through negotiation failure handling at client edge |
| emits artifact processing status updates |
| emits evidence reminder and escalation events |
| emits simulation status updates |
| emits packaging status updates |
| emits auditor query events |
| emits notification inbox updates |

### Identity and RBAC Service

| Responsibility |
| --- |
| local account authentication |
| password policy and optional MFA |
| user lifecycle |
| seeded role templates |
| custom role creation |
| scoped RBAC at tenant, org, portfolio, project, and credit levels |
| permission change audit |
| supports roles: Sustainability Consultant, Project Manager, Architect, MEP Consultant, Landscape Consultant, Construction Team, Procurement Team, Owner, PMC, External Auditor, Admin, Technical Admin |
| initializes new custom roles with all permissions false by default |
| Sustainability Consultant seeded template retains full access by default subject to RBAC |

### Project Service

| Responsibility |
| --- |
| project CRUD |
| organization and portfolio association |
| project workflow state |
| ownership transfer |
| cross-organization sharing records |
| milestones |
| budgets |
| KPI rollups |

### Project Intake Service

| Responsibility |
| --- |
| intake forms |
| mandatory and optional field validation |
| project bootstrap |
| import metadata |
| explicit intake model covering project, site, owner, stakeholder, timeline, budget, and import metadata fields |
| derived defaults from region and rating system |

### Rating Library Service

| Responsibility |
| --- |
| supported rating systems and versions |
| taxonomies, credits, prerequisites, dependencies |
| addenda tracking |
| draft/publish/retire lifecycle |
| 15 business day addenda SLA tracking |
| The SLA clock starts when an addenda document is received and verified by the platform administrator |
| The service must alert Admin and Technical Admin at 10 days remaining |
| Breach is escalated to the governance dashboard |
| admin-controlled updates only |

### Region Profile Service

| Responsibility |
| --- |
| region profiles |
| India climate zones and supported geographies |
| code references |
| standards references |
| EPW weather mapping |
| SI defaults and IST defaults |
| per-project parameter overlay |

### Pre-assessment Service

| Responsibility |
| --- |
| AI-assisted pre-assessment |
| feasibility analysis from project data and optional artifacts |
| credit-wise remarks |
| rationale generation |
| tables and graphs |
| stakeholder action items by Architect, Owner, Landscape, MEP, Construction, Procurement |
| confidence scoring |
| explainability persistence |
| When the AI Gateway is unavailable, the service degrades to rule-based scoring only and clearly marks outputs as non-AI-assisted pending retry |

### Credit Interpretation Service

| Responsibility |
| --- |
| typology/location/climate/version-based applicability |
| rules engine plus AI-assisted reasoning |
| rationale and source capture |
| versioned rule execution |
| regional preference overlay |

### Evidence Workflow Service

| Responsibility |
| --- |
| evidence requests |
| cadence scheduling |
| reminders and escalations |
| validation rules |
| ownership and statuses |
| monthly due-day defaults |
| weekly reminders if configured |
| business calendar aware SLA calculations |

### Ingestion Service

| Responsibility |
| --- |
| file upload |
| antivirus scan |
| content validation |
| duplicate detection by checksum and normalized metadata |
| file persistent storage using SaaS Object storage |
| metadata capture |
| handoff to OCR/classification/search |
| enforces default 100 MB maximum file size per artifact with admin-configurable overrides by artifact type |
| applies admin-configurable import templates by artifact type for metadata schema, mandatory fields, validation rules, naming rules, and default credit mappings |

### OCR and Extraction Service

| Responsibility |
| --- |
| OCR via Tesseract |
| table extraction |
| key-value extraction |
| reviewer workflow creation |
| provenance capture |
| curated benchmark tracking |

### BIM Extraction Service

| Responsibility |
| --- |
| IFC and exported schedule processing |
| spatial extraction |
| BIM normalization |
| RVT accepted as reference artifact only |

### Classification Service

| Responsibility |
| --- |
| rule-based and embedding-based classification |
| tenant-scoped embeddings only |
| no provider training |
| reviewer correction loop |
| confidence and rationale capture |

### Search Index Service

| Responsibility |
| --- |
| evidence indexing |
| search document synchronization |
| highlighting |
| aggregations |
| lifecycle management |

### Scorecard Service

| Responsibility |
| --- |
| dynamic score computation |
| prerequisites and dependency enforcement |
| manual override requests |
| dual approval handling |
| mixed-use apportionment |
| what-if scenario scoring |
| scoring history |
| anomaly and cross-credit consistency checks with corrective actions |

### Recommendation Service

| Responsibility |
| --- |
| compliance pathways |
| alternatives and tradeoffs |
| expected points impact |
| priority by impact/effort/feasibility |
| simulation or calculator backing where applicable |
| limited thermal comfort and carbon via imported or derived outputs |
| All quantitative design recommendations (energy, carbon, daylight, glare) must be backed by a simulation output or calculator result. Qualitative or governance recommendations (process steps, documentation practices) do not require simulation backing. This distinction is enforced by the Recommendation Service at generation time |

### Simulation Orchestrator Service

| Responsibility |
| --- |
| simulation request intake |
| license seat preflight |
| worker queueing |
| retry twice on failure |
| reproducibility seed logging |
| input fingerprinting |
| result ingestion within 5 minutes of completion |
| supports optional on-prem offline runners |

### Narrative and Document Generation Service

| Responsibility |
| --- |
| narratives |
| calculators |
| scorecards |
| feasibility reports |
| simulation summaries |
| checklists |
| form-ready outputs |
| export generation in PDF/DOCX/XLSX/JSON/PPTX |
| branding and footer stamping using AEON logo, colors, and fonts |

### Packaging Service

| Responsibility |
| --- |
| submission package assembly |
| naming convention enforcement |
| footer version stamping |
| checksum manifest generation |
| immutable package history |
| template version pins |
| submission dual approval enforcement with segregation of duties |
| package version sequence restricted to 1.0, 1.1, and 2.0 |
| manual download support only |

### Auditor Q and A Service

| Responsibility |
| --- |
| query threads |
| configurable workflow using default states QueryReceived, Assigned, DraftResponse, InternalReview, Approved, SentShared, AuditorFollowUp, Resolved, and Reopened |
| 2 business day first-response SLA baseline |
| canned responses |
| AI suggestions with citations |
| preserved context |
| mandatory evidence per claim before send |

### Standards and CIR Q and A Service

| Responsibility |
| --- |
| customer-uploaded licensed corpus |
| permitted public content |
| citation format enforcement |
| content version identifiers |
| license scope enforcement |
| expiry blocking |
| refresh cadence default monthly |
| access logging |

### Notification Service

| Responsibility |
| --- |
| in-app/email/WhatsApp notifications |
| notification send through email/WhatsApp |
| rule-driven dispatch |
| consent-aware WhatsApp sending |
| quiet hours from 21:00 to 08:00 IST |
| escalation after 2 hours unacknowledged |
| delivery tracking |
| consent capture with timestamped audit record |

### Analytics Service

| Responsibility |
| --- |
| project and portfolio dashboards |
| daily refresh at 01:00 IST |
| dashboard filter and grouping |
| KPI snapshots |
| exports |

### Admin and Governance Service

| Responsibility |
| --- |
| RBAC admin |
| workflow configuration |
| approvals |
| templates |
| branding |
| retention |
| notification rules |
| business calendars |
| policy rules |
| provider credentials |
| portal placeholders |
| per-portal configuration and enablement controls while defaulting to manual packaging and no RPA at MVP |
| risk register |
| audit export approvals with one business Admin approver and one Technical Admin approver before release |
| project transfer approvals |
| cross-organization sharing approvals driven by project sensitivity classification including dual approval for sensitive projects |

### Audit and Explainability Service

| Responsibility |
| --- |
| append-only audit logs |
| explainability records |
| AI prompt/response restricted logs |
| permission history |
| workflow history |
| package history |
| model registry lineage |

### Historical Data Ingestion Service

| Responsibility |
| --- |
| intake of historical customer-permitted datasets |
| redaction gating |
| written permission validation |
| approval workflows using an admin-configurable Data Owner approver role |
| tenant isolation |
| usage logging |
| embedding preparation |
| default opt-out unless explicit written permission exists |

### License Management Service

| Responsibility |
| --- |
| tool inventory |
| expiry alerts |
| seat alerts |
| invalid configuration alerts |
| seat assignment records |
| mode flags |
| checkout audit |

### Background Worker Services

| Responsibility |
| --- |
| executes long-running jobs from SQL queue |
| OCR |
| classification |
| simulation |
| packaging |
| exports |
| notifications |
| analytics refresh |
| SLA monitors |

### AI Gateway

| Responsibility |
| --- |
| acts as the internal platform gateway for all AI-assisted capabilities |
| centralizes AI request routing, policy enforcement, tenant isolation, rate limiting, prompt sanitization, and response validation |
| ensures all AI prompts and responses follow platform privacy, audit, explainability, and retention controls |
| applies PII minimization and masking before forwarding requests to external AI providers |
| invokes OpenAI GPT 5.1 as the approved third-party AI model provider |
| prevents direct service-to-provider calls from domain services |
| persists AI interaction logs, model identifiers, gateway policy version, evidence links, confidence scores, and explainability metadata |
| enforces no provider training and no provider retention policy through approved provider configuration |
| supports degradation behavior when the third-party AI provider is unavailable, allowing eligible services to fall back to rules-based or non-AI-assisted flows |

# Component Interactions

## Flow 1: Artifact Ingestion to Search

| Step | Description |
| --- | --- |
| 1 | User uploads artifact. |
| 2 | API Gateway routes to Ingestion Service. |
| 3 | Ingestion Service validates artifact type-specific import template rules and file size limits before storing content. |
| 4 | Ingestion Service stores artifact in storage and writes metadata to SQL. |
| 5 | Antivirus and content validation execute. |
| 6 | Duplicate detection executes. |
| 7 | Queue jobs are inserted for OCR, extraction, and classification. |
| 8 | OCR and Extraction Service extracts content. |
| 9 | Classification Service suggests relevant credits. |
| 10 | Search Index Service updates Elasticsearch. |
| 11 | Evidence Workflow Service links artifact to tasks if applicable. |
| 12 | SignalR notifies UI of processing updates. |

## Flow 2: Pre-assessment

| Step | Description |
| --- | --- |
| 1 | User triggers pre-assessment for a project. |
| 2 | Project master data and selected artifacts are assembled. |
| 3 | OCR/classification outputs are retrieved if needed. |
| 4 | Credit Interpretation Service evaluates likely applicability. |
| 5 | Recommendation Service generates alternatives and impacts. |
| 6 | AI-assisted requests from domain services are sent through the internal AI Gateway, which applies privacy, policy, masking, routing, and audit controls before invoking OpenAI GPT 5.1. |
| 7 | Pre-assessment Service composes score tables, graphs, rationales, and stakeholder action items. |
| 8 | Results are stored and can be exported. |
| 9 | Explainability and audit logs are recorded. |

## Flow 3: Evidence Workflow

| Step | Description |
| --- | --- |
| 1 | Evidence Workflow Service schedules tasks. |
| 2 | SQL scheduled jobs evaluate due dates and business calendars. |
| 3 | Notification Service sends reminders at 7, 3, and 1 days before due. |
| 4 | Overdue reminders send every 3 days. |
| 5 | Escalation goes to Project Manager at 3 days overdue. |
| 6 | Escalation goes to Owner at 10 days overdue. |
| 7 | State transitions are audited. |

## Flow 4: Simulation

| Step | Description |
| --- | --- |
| 1 | User submits simulation request. |
| 2 | Simulation Orchestrator validates tool, license seats, inputs, and region profile. |
| 3 | EPW weather file is selected. |
| 4 | Job is queued to worker. |
| 5 | Worker executes CLI/API or assisted-prep flow. |
| 6 | Output artifacts are uploaded. |
| 7 | Results are ingested and linked to project/credit. |
| 8 | Recommendation and scorecard recalculation can be triggered. |
| 9 | SignalR update is sent. |

## Flow 5: Submission Packaging

| Step | Description |
| --- | --- |
| 1 | User requests package creation. |
| 2 | Packaging Service validates selected artifacts and template pins. |
| 3 | Naming pattern `ProjectCode_CreditID_DocType_v1.0.ext`, `ProjectCode_CreditID_DocType_v1.1.ext`, or `ProjectCode_CreditID_DocType_v2.0.ext` is applied according to selected package version. |
| 4 | Version footer is stamped. |
| 5 | Checksum manifest is generated. |
| 6 | Package is stored as immutable history entry. |
| 7 | Package is stored using SaaS Object storage. |
| 8 | Dual approval workflow with segregation of duties is executed before approval status can become approved. |
| 9 | Final package is made available for manual download. |

## Flow 6: Auditor Q and A

| Step | Description |
| --- | --- |
| 1 | Query is created or received. |
| 2 | Evidence-linked claims are recorded. |
| 3 | AI suggestion can be requested. |
| 4 | Human drafts/edits response. |
| 5 | Internal review and approval occur. |
| 6 | Response is sent. |
| 7 | SLA and audit records are updated. |

# Interfaces

## Internal Service Interfaces

| Property | Value |
| --- | --- |
| Protocol | HTTPS REST over private network |

### Propagated Headers

| Header |
| --- |
| `X-Correlation-Id` |
| `X-Tenant-Id` |
| `X-User-Id` |

## External Interfaces

| Interface |
| --- |
| REST APIs via API Gateway |
| SignalR over WSS with SSE fallback |
| HMAC-signed webhooks |
| SendGrid HTTPS/SMTP |
| Meta Cloud API HTTPS REST |
| Weather source HTTPS download |
| OpenAI GPT 5.1 via AI Gateway |
| file exchange and API relay via Windows On-Prem Connector Service |

## External Interface Rules

| Rule | Value |
| --- | --- |
| security | explicit per-interface authentication, authorization, signing, allowlisting, and provider credential controls |
| idempotency | POST idempotency keys for public APIs and idempotent event delivery keys for webhooks |
| timeout | outbound external calls use explicit service-defined timeouts with retry/backoff and operational degradation behavior |
| retry | retries with backoff for supported outbound integrations |
| degradation | dependency-specific degradation preserves core transactional behavior where applicable |
| AI provider duplicate safety | no destructive duplicates |

## External Integration Matrix

| System | Integration Type | Protocol/Format | Direction | Frequency |
| --- | --- | --- | --- | --- |
| Certification Portals | Manual package export/upload support; future official API placeholder | User download/upload, file package | Outbound by user | On-demand |
| Autodesk ACC | Manual export/import | File download/upload | Inbound after user action | On-demand |
| Rhino / Grasshopper | Tool workflow support | File exchange / assisted orchestration | Bidirectional | On-demand |
| DesignBuilder | Simulation workflow support | File exchange / API/CLI where applicable | Bidirectional | On-demand |
| eQUEST | Simulation workflow support | File exchange / API/CLI where applicable | Bidirectional | On-demand |
| IESVE | Result ingestion and import/export | File exchange / supported interfaces | Inbound / Bidirectional | On-demand |
| OneClick LCA | Carbon and lifecycle result ingestion | File exchange / API where applicable | Inbound | On-demand |
| Revit | Import/export only; no headless automation | File upload/export | Bidirectional | On-demand |
| IoT Data Sources | Project data ingestion | Import/export or API-based ingestion | Inbound | Scheduled / On-demand |
| SendGrid | Notification delivery | SMTP or HTTPS API | Outbound | Event-driven |
| Meta Cloud API | WhatsApp notifications | HTTPS REST | Outbound | Event-driven |
| EPW / Weather Sources | Weather file retrieval | HTTPS download | Outbound | On-demand / cached |
| OpenAI GPT 5.1 | Third-party AI model provider invoked only through AI Gateway | HTTPS API | Outbound via AI Gateway | On-demand |
| External Webhook Subscribers | Event delivery | HTTPS callback with HMAC | Outbound | Event-driven |
| Windows On-Prem Connector Service | Secure file/API exchange for air-gapped environments; supports current and previous two major versions where applicable | Windows service, file exchange, API relay | Bidirectional | On-demand / scheduled |

# Data Architecture

## SQL Domains

| Domain |
| --- |
| identity and RBAC |
| projects, organizations, portfolios, stakeholders |
| project intake |
| rating systems and addenda |
| regional profiles |
| artifact metadata |
| OCR and extraction results |
| classification results |
| evidence tasks and transitions |
| scorecards and overrides |
| simulations and runs |
| packages and manifests |
| auditor threads |
| notifications and consent |
| audits and explainability |
| historical ingestion approvals |
| licenses |
| analytics warehouse aggregates |
| queue tables |

## Elasticsearch Indices

| Index |
| --- |
| `evidence-{tenantId}-v1` |
| `standards-{tenantId}-v1` |

## Storage Layout

| Path |
| --- |
| `/tenant/{tenantId}/project/{projectId}/artifacts/{artifactId}/` |
| `/tenant/{tenantId}/project/{projectId}/generated/{documentArtifactId}/` |
| `/tenant/{tenantId}/project/{projectId}/packages/{packageId}/` |
| `/tenant/{tenantId}/audit-exports/{requestId}/` |
| `/tenant/{tenantId}/consents/{consentId}/` |

## Domain Retention Coverage

| Data Domain | Retention |
| --- | --- |
| users, sessions, roles, permissions | per tenant policy; session and identity audit retained per policy |
| projects, organizations, portfolios, stakeholders | project continuity retained per tenant policy |
| project intake records | retained with project history |
| rating systems, credit definitions, addenda | retained per versioning policy |
| region profiles, climate maps, weather references | retained as configuration/reference data |
| artifacts and generated files | raw and derived artifacts default 36 months, then archive/purge per tenant policy |
| artifact metadata, hashes, upload events | default 36 months aligned to artifact policy |
| OCR outputs and extraction results | aligned to artifact/project retention |
| searchable content and metadata | lifecycle-managed per tenant/search policy |
| classification results and corrections | aligned to project retention |
| evidence items, tasks, state transitions | retained with project history |
| scorecards, overrides, scoring history | retained with project history |
| simulation jobs, runs, output metadata | retained with project history/policy |
| packages, checksums, package manifests | immutable once finalized; retained per policy |
| auditor threads and responses | retained with project history |
| notifications and delivery state | retained per policy |
| audit records and explainability metadata | not deletable through normal UI; retained per policy |
| historical ingestion requests and redaction reviews | per policy and permitted use controls |
| tool licenses and seat records | retained per administrative policy |
| analytics warehouse aggregates and KPI snapshots | retained per analytics policy |
| logs, metrics, traces | 12 months hot plus 7 years archive |
| AI prompt-response audit logs | 180 days default, configurable by tenant policy |

## Data Privacy Classifications

| Data Class | Classification |
| --- | --- |
| user identity data | PII |
| project reference data | sensitive business data |
| evidence documents | sensitive business data / possible PII |
| prompt-response audit records | restricted sensitive data |
| licensed standards corpus | restricted licensed content |
| analytics aggregates | usually non-PII but tenant-sensitive |

# Data Contracts

## Standard Error

```json
{ "traceId": "string", "code": "string", "message": "string", "details": {} }
```

## Schema: StandardError

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| traceId | string | yes | Distributed trace identifier |
| code | string | yes | Stable application error code |
| message | string | yes | Human-readable error message |
| details | object | yes | Structured error details |

## Schema: User

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | User identifier |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| email | string | yes | Login email |
| displayName | string | yes | User full name |
| status | string | yes | active/disabled/invited/locked |
| passwordPolicyVersion | string | yes | Applied password policy version |
| mfaEnabled | boolean | yes | MFA enabled indicator |
| lastLoginAt | string(date-time) | no | Last successful login |
| locale | string | yes | Locale code |
| unitSystem | string | yes | SI or Imperial |
| timezone | string | yes | IANA timezone |
| roles | array[string] | yes | Assigned role ids |
| scopes | array[object] | yes | Scope assignments |

## Schema: RoleTemplate

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Role template identifier |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| name | string | yes | Role name |
| systemRole | boolean | yes | True for seeded roles |
| permissions | array[string] | yes | Granted permissions |
| defaultPermissionState | string | yes | falseByDefault or predefinedTemplate |
| description | string | yes | Role description |

## Schema: Project

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Project identifier |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectCode | string | yes | Unique project code |
| name | string | yes | Project name |
| portfolioId | string | no | Portfolio identifier |
| ratingSystemCode | string | yes | Rating system code |
| ratingVersion | string | yes | Rating version |
| regionCode | string | yes | Geography code |
| climateZoneCode | string | no | Climate zone code |
| regionalProfileId | string | yes | Applied region profile id |
| timezone | string | yes | Project timezone |
| unitSystem | string | yes | SI or Imperial |
| status | string | yes | ProjectIntake/PreAssessment/EvidenceCollection/SimulationOrchestration/DocumentationNarrativesCalculators/PackagingForSubmission/ManualPortalUpload/AuditorQA/Resubmission/CertifiedClosed |
| ownerUserId | string | yes | Project owner user id |
| targetCertificationLevel | string | no | Target level |
| description | string | no | Description |
| startDate | string(date) | no | Start date |
| endDate | string(date) | no | End date |

## Schema: ProjectIntakeRecord

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Intake record id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectName | string | yes | Project name entered at intake |
| projectCode | string | yes | Unique intake project code |
| ratingSystemCode | string | yes | Selected rating system |
| ratingVersion | string | yes | Selected rating version |
| siteName | string | no | Site name |
| siteAddress | string | no | Site address |
| regionCode | string | yes | Geography code |
| climateZoneCode | string | no | Climate zone code |
| ownerOrganizationName | string | yes | Owner organization |
| stakeholderAssignments | array[object] | yes | Stakeholder role assignments |
| plannedStartDate | string(date) | no | Planned start date |
| plannedEndDate | string(date) | no | Planned end date |
| baselineBudgetAmount | number | no | Baseline budget |
| budgetCurrencyCode | string | no | Budget currency |
| importSourceType | string | no | Manual or import source |
| importSourceSystem | string | no | Source system name |
| importReference | string | no | Source reference |
| defaultedFields | array[string] | yes | Fields defaulted by system |
| derivedFields | array[string] | yes | Fields derived by system |

## Schema: RegionalProfile

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Regional profile id |
| tenantId | string | yes | Tenant or shared owner id |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| status | string | yes | draft/published/retired |
| code | string | yes | Profile code |
| regionCode | string | yes | Region code |
| climateZoneCode | string | no | Climate zone code |
| weatherFileCode | string | yes | Weather file reference |
| weatherFileUri | string | yes | Weather file URI |
| standardsReferences | array[string] | yes | Standards references |
| codeReferences | array[string] | yes | Code references |
| timezone | string | yes | Default timezone |
| unitSystem | string | yes | Default unit system |
| parameters | object | yes | Regional parameter set |

## Schema: RatingLibrary

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Rating library id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| ratingSystemCode | string | yes | Rating system code |
| ratingVariant | string | yes | Rating variant |
| versionLabel | string | yes | Version label |
| status | string | yes | draft/published/retired |
| effectiveDate | string(date) | yes | Effective date |
| authorUserId | string | yes | Authoring user |
| notes | string | no | Notes |
| priorVersionId | string | no | Previous version id |
| addenda | array[object] | yes | Addenda records |
| taxonomy | object | yes | Credit taxonomy |
| changeSummary | string | yes | Change summary |

## Schema: Artifact

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Artifact identifier |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | no | Linked credit id |
| fileName | string | yes | Original file name |
| mediaType | string | yes | MIME type |
| sizeBytes | integer | yes | Size in bytes |
| storageUri | string | yes | Storage path |
| checksumSha256 | string | yes | SHA256 checksum |
| uploadStatus | string | yes | uploaded/scanning/quarantined/validated/rejected/processed |
| antivirusStatus | string | yes | pending/clean/infected/error |
| contentValidationStatus | string | yes | pending/valid/invalid |
| searchableTextStatus | string | yes | pending/ready/failed |
| extractionStatus | string | yes | pending/inReview/approved/rejected/notApplicable |
| classificationStatus | string | yes | pending/suggested/approved/corrected/rejected |
| sourceType | string | yes | upload/import/simulation/generated/email |
| sourceSystem | string | no | Source system classification |
| sourceReference | string | no | Source reference |
| systemOfRecord | boolean | yes | Indicates AEON authoritative record |
| tags | array[string] | yes | Tags |
| metadata | object | yes | Searchable metadata |

## Schema: ExtractionResult

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Extraction result id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| artifactId | string | yes | Artifact id |
| extractionType | string | yes | ocr/table/keyValue/bimSpatial |
| confidence | number | yes | Overall confidence |
| fields | array[object] | yes | Extracted fields |
| rawText | string | no | OCR raw text |
| reviewerStatus | string | yes | pending/approved/rejected/corrected |
| reviewedByUserId | string | no | Reviewer user id |
| reviewedAt | string(date-time) | no | Review time |
| provenance | object | yes | Engine/source provenance |

## Schema: ClassificationResult

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Classification result id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| artifactId | string | yes | Artifact id |
| projectId | string | yes | Project id |
| suggestedCreditIds | array[string] | yes | Suggested credit ids |
| finalCreditIds | array[string] | yes | Approved credit ids |
| method | string | yes | rules/embeddings/hybrid |
| precisionEstimate | number | no | Precision estimate |
| recallEstimate | number | no | Recall estimate |
| reviewerStatus | string | yes | pending/approved/corrected/rejected |
| rationale | string | yes | Classification rationale |
| noTrainingUsed | boolean | yes | Confirms no training use |

## Schema: EvidenceTask

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Evidence task id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | no | Credit id |
| title | string | yes | Task title |
| description | string | no | Description |
| ownerUserId | string | yes | Owner user id |
| dueDate | string(date-time) | yes | Due date time |
| cadence | string | yes | monthly/weekly/adHoc |
| reminderRule | string | yes | Reminder expression |
| escalationRule | string | yes | Escalation expression |
| validationRules | array[string] | yes | Validation rules |
| status | string | yes | NotRequested/Requested/InProgress/Submitted/UnderReview/Approved/RevisionRequired/Resubmitted/Rejected/Overdue/WaivedNotApplicable/LockedForSubmission |
| slaStatus | string | yes | onTrack/atRisk/breached |
| lastReminderAt | string(date-time) | no | Last reminder time |
| lastEscalationAt | string(date-time) | no | Last escalation time |
| dueDayOfMonth | integer | no | Monthly due day default 5 |

## Schema: Scorecard

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Scorecard id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| totalPossiblePoints | number | yes | Total possible points |
| projectedPoints | number | yes | Projected points |
| achievedPoints | number | yes | Achieved points |
| prerequisiteStatus | string | yes | pass/fail/warning |
| dependencyStatus | string | yes | valid/blocked/warning |
| credits | array[object] | yes | Credit score entries |
| lastRecalculatedAt | string(date-time) | yes | Last calculation time |
| scenarioId | string | no | What-if scenario id |

## Schema: ScoreOverride

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Override id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | yes | Credit id |
| requestedByUserId | string | yes | Requestor |
| reasonCode | string | yes | Controlled reason code |
| justification | string | yes | Justification text |
| attachmentArtifactIds | array[string] | yes | Supporting artifacts |
| requestedValue | object | yes | Requested override value |
| approvalStatus | string | yes | pending/approved/rejected |
| approvalCount | integer | yes | Approval count |
| requiredApprovalCount | integer | yes | Required approvals |
| segregationOfDutiesPassed | boolean | yes | SoD result |

## Schema: InterpretationResult

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Interpretation id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | yes | Credit id |
| ratingSystemCode | string | yes | Rating system code |
| ratingVersion | string | yes | Rating version |
| locationCode | string | yes | Location code |
| ruleVersionId | string | yes | Applied rule version |
| preferenceVersionId | string | no | Applied preference version |
| addendumIds | array[string] | yes | Applied addenda ids |
| applicability | string | yes | applicable/conditionallyApplicable/notApplicable |
| rationale | string | yes | Interpretation rationale |
| sources | array[object] | yes | Citations |
| confidence | number | yes | Confidence |
| modelIdentifier | string | yes | Model identifier |

## Schema: SimulationJob

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Simulation job id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| tool | string | yes | Simulation tool |
| simulationType | string | yes | daylight/glare/energy/other |
| regionalProfileId | string | yes | Regional profile id |
| weatherFileCode | string | yes | Weather file code |
| inputArtifactIds | array[string] | yes | Input artifacts |
| assistedPrepUsed | boolean | yes | Assisted prep used |
| status | string | yes | queued/preparing/running/retrying/completed/failed/cancelled/deadLettered |
| queueEnteredAt | string(date-time) | yes | Queue entry time |
| startedAt | string(date-time) | no | Start time |
| completedAt | string(date-time) | no | Completion time |
| runtimeBudgetSeconds | integer | yes | Runtime budget |
| retryCount | integer | yes | Retry count |
| maxRetries | integer | yes | Max retries |
| workerId | string | no | Worker id |
| reproducibilitySeed | string | yes | Deterministic seed |
| inputFingerprint | string | yes | Input fingerprint |
| outputArtifactIds | array[string] | yes | Output artifacts |

## Schema: DocumentArtifact

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Document artifact id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| documentType | string | yes | Output type |
| format | string | yes | pdf/docx/xlsx/json/pptx |
| templateId | string | yes | Template id |
| templateVersion | string | yes | Template version |
| brandingApplied | boolean | yes | Branding applied flag |
| watermarkApplied | boolean | yes | Watermark applied flag |
| footerVersionText | string | yes | Footer version |
| storageUri | string | yes | Storage URI |
| checksumSha256 | string | yes | SHA256 checksum |
| reviewStatus | string | yes | draft/inReview/approved/rejected |
| reviewedByUserId | string | no | Reviewer |
| reviewedAt | string(date-time) | no | Review time |

## Schema: SubmissionPackage

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Package id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| packageVersion | string | yes | Supported values: 1.0/1.1/2.0 |
| namingConvention | string | yes | Naming pattern |
| artifactIds | array[string] | yes | Included artifacts |
| documentArtifactIds | array[string] | yes | Included documents |
| checksumManifest | array[object] | yes | Checksum entries |
| immutableHistorySequence | integer | yes | Immutable sequence |
| templatePins | array[object] | yes | Template pins |
| approvalStatus | string | yes | pending/approved/rejected |
| requiredApprovalCount | integer | yes | Required approvals |
| approvalCount | integer | yes | Completed approvals |
| approverIds | array[string] | yes | Approver ids |
| segregationOfDutiesPassed | boolean | yes | SoD result |
| approvalAuditTrail | array[object] | yes | Approval events |

## Schema: AuditorQuery

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Query id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | yes | Credit id |
| subject | string | yes | Subject |
| state | string | yes | QueryReceived/Assigned/DraftResponse/InternalReview/Approved/SentShared/AuditorFollowUp/Resolved/Reopened |
| firstResponseDueAt | string(date-time) | yes | SLA deadline |
| firstResponseSentAt | string(date-time) | no | First response sent time |
| evidenceLinks | array[string] | yes | Thread-level evidence links |
| cannedResponseId | string | no | Canned response id |
| threadContext | array[object] | yes | Conversation context |
| slaStatus | string | yes | onTrack/atRisk/breached |
| claims | array[object] | yes | Claim records with evidence links |

## Schema: Notification

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Notification id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| channel | string | yes | inApp/email/whatsapp |
| recipientUserId | string | no | User id |
| recipientAddress | string | yes | Delivery address |
| templateId | string | yes | Template id |
| ruleId | string | no | Triggering rule id |
| status | string | yes | queued/suppressed/sent/delivered/failed |
| consentVerified | boolean | yes | Consent verified |
| externalMessageId | string | no | Provider message id |
| deliveredAt | string(date-time) | no | Delivered time |
| acknowledgedAt | string(date-time) | no | User acknowledgement time |
| failureReason | string | no | Failure reason |
| suppressedReason | string | no | quietHours/consentMissing/userPreference/other |

## Schema: WhatsAppConsent

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Consent id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Creation time |
| updatedAt | string(date-time) | yes | Update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| subjectType | string | yes | user/contact |
| subjectId | string | yes | Subject id |
| phoneNumber | string | yes | E.164 phone number |
| consentStatus | string | yes | granted/withdrawn |
| captureMethod | string | yes | inApp/emailLink/userInitiatedMessage |
| evidenceUri | string | yes | Consent evidence URI |
| templateScope | array[string] | yes | Approved template scope |
| unsubscribedAt | string(date-time) | no | Unsubscribe time |
| auditRecordedAt | string(date-time) | yes | Timestamped audit record time |

## Schema: StandardCorpusItem

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Corpus item id |
| tenantId | string | yes | Tenant or shared owner id |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| title | string | yes | Title |
| contentVersionIdentifier | string | yes | Content version id |
| sourceType | string | yes | licensed/permittedPublic |
| licenseScope | string | yes | Permitted scope |
| licenseStartDate | string(date) | yes | License start |
| licenseEndDate | string(date) | no | License end |
| citationStyle | string | yes | Citation style |
| refreshCadenceDays | integer | yes | Refresh cadence |

## Schema: AIInteractionLog

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | AI interaction id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Same as createdAt for immutable record |
| version | integer | yes | Schema version |
| actorUserId | string | no | Initiating user id |
| useCase | string | yes | AI use case |
| promptRedacted | string | yes | Redacted prompt |
| responseRedacted | string | yes | Redacted response |
| rationale | string | no | Explainability rationale |
| evidenceLinks | array[string] | yes | Evidence links |
| confidence | number | no | Confidence |
| modelIdentifier | string | yes | Model identifier |
| modelVersion | string | yes | Model version |
| gatewayPolicyVersion | string | yes | Gateway policy version |
| retentionDays | integer | yes | Retention period |
| immutableHash | string | yes | Tamper-evident hash |
| datasetReferences | array[string] | yes | Referenced datasets |
| trainingPermissionRecordId | string | no | Explicit written permission reference |
| providerTrainingAllowed | boolean | yes | Always false |
| crossBorderProcessingAllowed | boolean | yes | Indicates whether cross-border processing may occur for LLM API calls with PII minimization as allowed by FRD |

## Schema: RiskRegisterItem

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Risk id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | no | Linked project id |
| creditId | string | no | Linked credit id |
| title | string | yes | Risk title |
| category | string | yes | Risk category |
| severity | string | yes | low/medium/high/critical |
| likelihood | string | yes | rare/unlikely/possible/likely/almostCertain |
| ownerUserId | string | yes | Owner |
| mitigationPlan | string | yes | Mitigation |
| status | string | yes | open/monitoring/mitigated/closed |
| source | string | yes | manual/automatedMonitor |
| lastReviewedAt | string(date-time) | no | Last review time |

## Schema: Milestone

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Milestone id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| name | string | yes | Milestone name |
| plannedDate | string(date) | yes | Planned date |
| actualDate | string(date) | no | Actual date |
| status | string | yes | notStarted/inProgress/completed/delayed |
| stageGate | string | no | Stage gate |
| ownerUserId | string | yes | Owner |

## Schema: Budget

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Budget id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| baselineAmount | number | yes | Baseline amount |
| currentAmount | number | yes | Current amount |
| varianceAmount | number | yes | Variance amount |
| variancePercent | number | yes | Variance percent |
| alertThresholdPercent | number | yes | Threshold percent |
| currencyCode | string | yes | Currency code |
| acknowledgedByUserId | string | no | Acknowledged by |
| acknowledgedAt | string(date-time) | no | Acknowledgement time |

## Schema: KPIRecord

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | KPI record id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record time |
| updatedAt | string(date-time) | yes | Same as createdAt |
| version | integer | yes | Schema version |
| metricCode | string | yes | KPI code |
| scopeType | string | yes | tenant/portfolio/project |
| scopeId | string | yes | Scope id |
| baselineValue | number | yes | Baseline value |
| currentValue | number | yes | Current value |
| trendDirection | string | yes | up/down/flat |
| periodStart | string(date) | yes | Start date |
| periodEnd | string(date) | yes | End date |

## Schema: WebhookSubscription

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Subscription id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| targetUrl | string | yes | Webhook target URL |
| secretRef | string | yes | Secret reference |
| eventTypes | array[string] | yes | Event types |
| active | boolean | yes | Active flag |
| ipAllowlistEnabled | boolean | yes | IP restriction flag |
| retryPolicy | object | yes | Retry policy |
| secretRotationEnabled | boolean | yes | Rotating secret support enabled |

## Schema: RetentionPolicy

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Policy id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| policyName | string | yes | Policy name |
| entityType | string | yes | Entity type |
| retentionDays | integer | yes | Retention days |
| softDeleteRestoreWindowDays | integer | no | Default 30 calendar day restore window, tenant-configurable subject to policy |
| status | string | yes | enabled/disabled |
| notes | string | no | Notes |

## Schema: TenantExportRequest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Export request id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| requestedByUserId | string | yes | Requestor user id |
| status | string | yes | requested/preparing/ready/expired/failed |
| deliveryMethod | string | yes | download/sftp |
| deliveryUri | string | no | Delivery URI |
| expiresAt | string(date-time) | no | Expiry time |

## Schema: SecureDeletionRequest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Secure deletion request id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| requestedByUserId | string | yes | Requestor |
| scopeType | string | yes | project/portfolio/tenant/object |
| scopeId | string | yes | Scope id |
| status | string | yes | pending/approved/rejected/inProgress/completed/failed |
| justification | string | yes | Justification |
| approvalCount | integer | yes | Approval count |
| requiredApprovalCount | integer | yes | Required approvals |
| irreversible | boolean | yes | Irreversibility flag |

## Schema: TrainingPermissionRecord

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Permission record id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| subjectType | string | yes | tenant/project/dataset |
| subjectId | string | yes | Subject id |
| permissionStatus | string | yes | granted/withdrawn/expired |
| writtenApprovalUri | string | yes | Written approval evidence URI |
| approvedByUserId | string | yes | Approver |
| effectiveFrom | string(date-time) | yes | Effective from |
| effectiveTo | string(date-time) | no | Effective to |
| notes | string | no | Notes |

## Schema: AuditExportRequest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Audit export request id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| requestedByUserId | string | yes | Initiating Admin or Technical Admin |
| status | string | yes | requested/pendingApproval/approved/released/failed |
| approvalCount | integer | yes | Completed approvals |
| requiredBusinessAdminApprovals | integer | yes | Required business Admin approvals |
| requiredTechnicalAdminApprovals | integer | yes | Required Technical Admin approvals |
| requesterCannotApprove | boolean | yes | Separation of requester and approver enforced |
| deliveryUri | string | no | Delivery location |
| releasedAt | string(date-time) | no | Release time |

## Schema: BusinessCalendar

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Business calendar id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| name | string | yes | Calendar name |
| scopeType | string | yes | tenant/project |
| scopeId | string | no | Project id when scopeType is project |
| timezone | string | yes | Calendar timezone |
| workingDays | array[string] | yes | Working days |
| holidayDates | array[string] | yes | Holiday dates |
| active | boolean | yes | Active flag |

## Schema: NotificationRule

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Notification rule id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| eventCode | string | yes | Triggering event code |
| channels | array[string] | yes | inApp/email/whatsapp |
| enabled | boolean | yes | Rule enabled flag |
| escalationAfterMinutes | integer | no | Unacknowledged escalation threshold |
| quietHoursStartLocal | string | no | Default 21:00 IST |
| quietHoursEndLocal | string | no | Default 08:00 IST |
| quietHoursTimezone | string | no | Default Asia/Kolkata |

## Schema: NotificationSettings

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Notification settings id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| userId | string | yes | User id |
| emailEnabled | boolean | yes | Email enabled |
| inAppEnabled | boolean | yes | In-app enabled |
| whatsappEnabled | boolean | yes | WhatsApp enabled subject to consent |

## Schema: AccessInvite

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Invite id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Shared project id |
| targetOrganizationId | string | yes | Invited organization id |
| defaultAccessLevel | string | yes | readOnly |
| status | string | yes | pending/accepted/rejected/expired |
| expiresAt | string(date-time) | yes | Invite expiry |

## Schema: AccessGrant

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Access grant id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| inviteId | string | yes | Source invite id |
| projectId | string | yes | Shared project id |
| organizationId | string | yes | Granted organization id |
| accessLevel | string | yes | readOnly/elevated |
| elevatedByUserId | string | no | Elevation approver |
| active | boolean | yes | Grant active flag |
| sensitivityApprovalRequirement | string | yes | single/dual |

## Schema: ProjectTransferRequest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Transfer request id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| sourceOrganizationId | string | yes | Source organization |
| targetOrganizationId | string | yes | Target organization |
| approvalStatus | string | yes | pending/approved/rejected |
| requiredApprovalCount | integer | yes | Dual-admin approval count requirement |
| approvalCount | integer | yes | Completed approval count |
| approverIds | array[string] | yes | Recorded approver ids |
| sourceHandoverAccessExpiresAt | string(date-time) | no | Automatic expiry unless extended |
| handoverAccessExtensionApproved | boolean | yes | Indicates whether handover access expiry was extended |
| segregationOfDutiesPassed | boolean | yes | Dual-admin control result |

## Schema: ModelRegistry

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Model registry id |
| tenantId | string | yes | Tenant or shared owner id |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| modelIdentifier | string | yes | Model identifier |
| modelVersion | string | yes | Model version |
| referencedDatasets | array[string] | yes | Referenced datasets |
| status | string | yes | active/retired |
| notes | string | no | Notes |

## Schema: PolicyRule

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Policy rule id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| scopeType | string | yes | project/portfolio |
| scopeId | string | yes | Scope id |
| sensitivityClass | string | no | Sensitivity classification |
| approvalRequirement | string | yes | single/dual |
| enabled | boolean | yes | Rule enabled |

## Schema: AnomalyCheckResult

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Anomaly result id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| projectId | string | yes | Project id |
| creditId | string | no | Related credit id |
| ruleCode | string | yes | Anomaly rule code |
| status | string | yes | open/resolved/waived |
| severity | string | yes | low/medium/high |
| measuredValue | string | no | Measured value |
| expectedValue | string | no | Expected value |
| rationale | string | yes | Explainable rule output |

## Schema: CorrectiveAction

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Corrective action id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| anomalyCheckResultId | string | yes | Linked anomaly result |
| actionText | string | yes | Proposed corrective action |
| ownerUserId | string | no | Assigned owner |
| dueAt | string(date-time) | no | Due date |
| status | string | yes | open/inProgress/completed/rejected |

## Schema: ImportTemplate

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Import template id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| artifactType | string | yes | Artifact type classification |
| metadataSchema | object | yes | Metadata schema definition |
| mandatoryFields | array[string] | yes | Required metadata fields |
| validationRules | array[string] | yes | Validation rules |
| namingRule | string | no | Naming rule |
| defaultCreditMappings | array[object] | yes | Default credit mappings |
| maxFileSizeMb | integer | yes | Default 100 unless overridden by artifact type |
| active | boolean | yes | Active flag |

## Schema: PortalConfiguration

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Portal configuration id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| portalCode | string | yes | Portal identifier |
| enabled | boolean | yes | Enablement control |
| mode | string | yes | manualPlaceholder |
| packagingTemplateSetId | string | no | Mapped packaging template set |
| notes | string | no | Configuration notes |

## Schema: LicenseSeatAssignment

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Seat assignment id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| toolCode | string | yes | Licensed tool code |
| seatIdentifier | string | yes | Seat identifier |
| assignedToUserId | string | no | Assigned user |
| modeFlag | string | yes | named/concurrent/offlineRunner |
| checkoutStatus | string | yes | available/checkedOut/reserved/invalid |
| checkedOutAt | string(date-time) | no | Checkout time |
| returnedAt | string(date-time) | no | Return time |

## Schema: RestoreRequest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| id | string | yes | Restore request id |
| tenantId | string | yes | Tenant identifier |
| createdAt | string(date-time) | yes | Record creation time |
| updatedAt | string(date-time) | yes | Record update time |
| version | integer | yes | Row version |
| etag | string | yes | Concurrency token |
| entityType | string | yes | Entity type to restore |
| entityId | string | yes | Soft-deleted entity id |
| requestedByUserId | string | yes | Requestor |
| status | string | yes | requested/approved/rejected/restored/failed |
| restoreWindowDaysApplied | integer | yes | Applied restore window, default 30 calendar days subject to tenant policy |
| restoredAt | string(date-time) | no | Restore time |

# API Standards

| Standard | Value |
| --- | --- |
| Base path | `/api/v1` |
| Authentication UI | session token |
| Authentication API clients | OAuth2 client credentials |
| Required header 1 | `Authorization: Bearer <token>` |
| Required header 2 | `X-Tenant-Id: <tenant-id>` |
| Optional header | `X-Correlation-Id: <guid>` |
| Create/trigger header | `Idempotency-Key: <string>` |
| Mutable update header | `If-Match: <etag>` |
| Per-tenant rate limit sustained | 50 requests/sec |
| Per-tenant rate limit burst | 200 |
| Standard error | `{ "traceId": "string", "code": "string", "message": "string", "details": {} }` |

# Complete API Contracts

## POST /api/v1/artifacts

Description: Upload a new artifact.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Content-Type | multipart/form-data |

### Body

| Field | Type | Required |
| --- | --- | --- |
| projectId | string | required |
| creditId | string | optional |
| sourceType | string | required |
| sourceSystem | string | optional |
| sourceReference | string | optional |
| importTemplateId | string | optional |
| tags | string | optional comma-separated |
| file | binary | required |

### Response 201

```json
{
  "id": "art-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T10:00:00Z",
  "updatedAt": "2026-06-09T10:00:00Z",
  "version": 1,
  "etag": "\"1-art-001\"",
  "projectId": "prj-001",
  "creditId": null,
  "fileName": "facade-study.pdf",
  "mediaType": "application/pdf",
  "sizeBytes": 4194304,
  "storageUri": "/tenant/ten-001/project/prj-001/artifacts/art-001/facade-study.pdf",
  "checksumSha256": "a1b2c3",
  "uploadStatus": "scanning",
  "antivirusStatus": "pending",
  "contentValidationStatus": "pending",
  "searchableTextStatus": "pending",
  "extractionStatus": "pending",
  "classificationStatus": "pending",
  "sourceType": "upload",
  "sourceSystem": "cadUpload",
  "sourceReference": "local-user-upload",
  "systemOfRecord": true,
  "tags": ["facade","daylight"],
  "metadata": {}
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid multipart request, file exceeds default 100 MB limit or active artifact-type override, or import template validation failed |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project not found |
| 500 | internal server error |

## GET /api/v1/artifacts/{id}/status

Description: Get artifact processing status.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | id string required |

### Response 200

```json
{
  "id": "art-001",
  "status": "processed",
  "uploadStatus": "processed",
  "antivirusStatus": "clean",
  "contentValidationStatus": "valid",
  "searchableTextStatus": "ready",
  "extractionStatus": "approved",
  "classificationStatus": "approved"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | artifact not found |
| 500 | internal server error |

## GET /api/v1/projects/{id}/scorecard

Description: Get current project scorecard.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | id string required |

### Response 200

```json
{
  "id": "sc-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T10:10:00Z",
  "updatedAt": "2026-06-09T10:15:00Z",
  "version": 3,
  "etag": "\"3-sc-001\"",
  "projectId": "prj-001",
  "totalPossiblePoints": 110,
  "projectedPoints": 67,
  "achievedPoints": 12,
  "prerequisiteStatus": "pass",
  "dependencyStatus": "valid",
  "credits": [],
  "lastRecalculatedAt": "2026-06-09T10:15:00Z",
  "scenarioId": null
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project or scorecard not found |
| 500 | internal server error |

## POST /api/v1/webhooks/subscriptions

Description: Create a webhook subscription.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| targetUrl | string | required |
| secretRef | string | required |
| eventTypes | array[string] | required |
| active | boolean | required |
| ipAllowlistEnabled | boolean | required |
| retryPolicy | object | required |
| secretRotationEnabled | boolean | required |

### Response 201

```json
{
  "id": "wh-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T10:20:00Z",
  "updatedAt": "2026-06-09T10:20:00Z",
  "version": 1,
  "etag": "\"1-wh-001\"",
  "targetUrl": "https://example.com/webhooks/aeon",
  "secretRef": "dpapi://wh-001",
  "eventTypes": ["artifact.added","simulation.completed"],
  "active": true,
  "ipAllowlistEnabled": false,
  "retryPolicy": { "maxAttempts": 5 },
  "secretRotationEnabled": true
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | tenant not found |
| 500 | internal server error |

## GET /api/v1/submission-packages/{id}/status

Description: Get submission package status.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | id string required |

### Response 200

```json
{
  "id": "pkg-001",
  "projectId": "prj-001",
  "approvalStatus": "approved",
  "packageVersion": "1.0",
  "immutableHistorySequence": 4
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | package not found |
| 500 | internal server error |

## GET /api/v1/auditor-queries/{id}/status

Description: Get auditor query workflow status.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | id string required |

### Response 200

```json
{
  "id": "aq-001",
  "projectId": "prj-001",
  "state": "InternalReview",
  "slaStatus": "onTrack",
  "firstResponseDueAt": "2026-06-11T18:30:00Z"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | auditor query not found |
| 500 | internal server error |

## POST /api/v1/admin/provider-credentials

Description: Create or rotate provider credentials at tenant scope with optional project override support.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| providerCode | string | required |
| scopeType | string | required |
| scopeId | string | optional |
| credentialPayload | object | required |
| rotateImmediately | boolean | required |

### Response 201

```json
{
  "id": "cred-001",
  "providerCode": "sendgrid",
  "scopeType": "tenant",
  "scopeId": null
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | scope not found |
| 500 | internal server error |

## GET /api/v1/audit-exports/{id}

Description: Get restricted audit export request status or released file metadata.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | id string required |

### Response 200

```json
{
  "id": "aex-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T11:00:00Z",
  "updatedAt": "2026-06-09T11:05:00Z",
  "version": 2,
  "etag": "\"2-aex-001\"",
  "requestedByUserId": "usr-admin-01",
  "status": "released",
  "approvalCount": 2,
  "requiredBusinessAdminApprovals": 1,
  "requiredTechnicalAdminApprovals": 1,
  "requesterCannotApprove": true,
  "deliveryUri": "/tenant/ten-001/audit-exports/aex-001/export.zip",
  "releasedAt": "2026-06-09T11:05:00Z"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | audit export not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/pre-assessments

Description: Start a pre-assessment run.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| inputArtifactIds | array[string] | required |
| includeMasterData | boolean | required |
| scenarioName | string | optional |
| notes | string | optional |

### Response 201

```json
{
  "runId": "pre-001",
  "projectId": "prj-001",
  "status": "queued",
  "createdAt": "2026-06-09T11:10:00Z"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project not found |
| 500 | internal server error |

## GET /api/v1/projects/{projectId}/pre-assessments/{runId}

Description: Get pre-assessment result.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Path | projectId string required |
| Path | runId string required |

### Response 200

```json
{
  "runId": "pre-001",
  "projectId": "prj-001",
  "status": "completed",
  "completedAt": "2026-06-09T11:11:20Z",
  "scores": [],
  "graphs": [],
  "recommendations": [],
  "stakeholderActionItems": [],
  "confidence": 0.91,
  "rationales": ["Derived from project master data and uploaded artifacts"]
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | invalid id |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | run not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/interpretations

Description: Execute credit interpretation.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| creditId | string | required |
| artifactIds | array[string] | optional |
| usePreferences | boolean | required |
| useAddenda | boolean | required |

### Response 201

```json
{
  "id": "int-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T11:20:00Z",
  "updatedAt": "2026-06-09T11:20:00Z",
  "version": 1,
  "etag": "\"1-int-001\"",
  "projectId": "prj-001",
  "creditId": "LEED-EAc1",
  "ratingSystemCode": "LEED",
  "ratingVersion": "v4.1",
  "locationCode": "IN",
  "ruleVersionId": "rule-2026-06",
  "preferenceVersionId": "pref-001",
  "addendumIds": ["add-001"],
  "applicability": "applicable",
  "rationale": "Applicable based on project typology and climate zone",
  "sources": [],
  "confidence": 0.88,
  "modelIdentifier": "openai-gpt-5.1"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project or credit not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/simulations

Description: Submit a simulation job.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| tool | string | required |
| simulationType | string | required |
| regionalProfileId | string | required |
| inputArtifactIds | array[string] | required |
| assistedPrepAllowed | boolean | required |
| runtimeBudgetSeconds | integer | required |

### Response 201

```json
{
  "id": "sim-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T11:30:00Z",
  "updatedAt": "2026-06-09T11:30:00Z",
  "version": 1,
  "etag": "\"1-sim-001\"",
  "projectId": "prj-001",
  "tool": "designBuilder",
  "simulationType": "energy",
  "regionalProfileId": "reg-001",
  "weatherFileCode": "EPW-MUMBAI-ISHRAE",
  "inputArtifactIds": ["art-001"],
  "assistedPrepUsed": false,
  "status": "queued",
  "queueEnteredAt": "2026-06-09T11:30:00Z",
  "startedAt": null,
  "completedAt": null,
  "runtimeBudgetSeconds": 21600,
  "retryCount": 0,
  "maxRetries": 2,
  "workerId": null,
  "reproducibilitySeed": "seed-001",
  "inputFingerprint": "fp-001",
  "outputArtifactIds": []
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project or profile not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/packages

Description: Build a submission package for manual upload.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| artifactIds | array[string] | required |
| templatePins | array[object] | required |
| packageVersion | string | required |

### Response 201

```json
{
  "id": "pkg-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T11:40:00Z",
  "updatedAt": "2026-06-09T11:40:00Z",
  "version": 1,
  "etag": "\"1-pkg-001\"",
  "projectId": "prj-001",
  "packageVersion": "1.0",
  "namingConvention": "ProjectCode_CreditID_DocType_v1.0.ext|v1.1.ext|v2.0.ext",
  "artifactIds": ["art-001"],
  "documentArtifactIds": ["doc-001"],
  "checksumManifest": [],
  "immutableHistorySequence": 1,
  "templatePins": [],
  "approvalStatus": "pending",
  "requiredApprovalCount": 2,
  "approvalCount": 0,
  "approverIds": [],
  "segregationOfDutiesPassed": false,
  "approvalAuditTrail": []
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project or artifact not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/qa/queries

Description: Create an auditor Q and A thread.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| creditId | string | required |
| subject | string | required |
| evidenceLinks | array[string] | required |
| initialMessage | string | required |
| claims | array[object] | optional |

### Response 201

```json
{
  "id": "aq-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T11:50:00Z",
  "updatedAt": "2026-06-09T11:50:00Z",
  "version": 1,
  "etag": "\"1-aq-001\"",
  "projectId": "prj-001",
  "creditId": "LEED-EAc1",
  "subject": "Clarify baseline assumptions",
  "state": "QueryReceived",
  "firstResponseDueAt": "2026-06-11T18:30:00Z",
  "firstResponseSentAt": null,
  "evidenceLinks": ["art-001"],
  "cannedResponseId": null,
  "threadContext": [],
  "slaStatus": "onTrack",
  "claims": []
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project not found |
| 500 | internal server error |

## POST /api/v1/admin/audit-export-requests

Description: Initiate restricted audit export.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| from | string(date-time) | optional |
| to | string(date-time) | optional |
| resourceTypes | array[string] | optional |
| deliveryMethod | string | required |

### Response 201

```json
{
  "id": "aex-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T12:00:00Z",
  "updatedAt": "2026-06-09T12:00:00Z",
  "version": 1,
  "etag": "\"1-aex-001\"",
  "requestedByUserId": "usr-admin-01",
  "status": "pendingApproval",
  "approvalCount": 0,
  "requiredBusinessAdminApprovals": 1,
  "requiredTechnicalAdminApprovals": 1,
  "requesterCannotApprove": true,
  "deliveryUri": null,
  "releasedAt": null
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | tenant not found |
| 500 | internal server error |

## POST /api/v1/admin/training-permissions

Description: Create explicit written permission record for training-data authorization.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| subjectType | string | required |
| subjectId | string | required |
| permissionStatus | string | required |
| writtenApprovalUri | string | required |
| approvedByUserId | string | required |
| effectiveFrom | string(date-time) | required |
| effectiveTo | string(date-time) | optional |
| notes | string | optional |

### Response 201

```json
{
  "id": "tpr-001",
  "tenantId": "ten-001",
  "createdAt": "2026-06-09T12:10:00Z",
  "updatedAt": "2026-06-09T12:10:00Z",
  "version": 1,
  "etag": "\"1-tpr-001\"",
  "subjectType": "dataset",
  "subjectId": "hist-001",
  "permissionStatus": "granted",
  "writtenApprovalUri": "/tenant/ten-001/permissions/tpr-001.pdf",
  "approvedByUserId": "usr-admin-01",
  "effectiveFrom": "2026-06-09T12:10:00Z",
  "effectiveTo": null,
  "notes": "Approved historical dataset usage"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | subject not found |
| 500 | internal server error |

## POST /api/v1/admin/import-templates

Description: Create or update an admin-configurable import template by artifact type.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| artifactType | string | required |
| metadataSchema | object | required |
| mandatoryFields | array[string] | required |
| validationRules | array[string] | required |
| namingRule | string | optional |
| defaultCreditMappings | array[object] | required |
| maxFileSizeMb | integer | required |
| active | boolean | required |

### Response 201

```json
{
  "id": "imt-001",
  "artifactType": "cadUpload",
  "maxFileSizeMb": 100,
  "active": true
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | tenant not found |
| 500 | internal server error |

## POST /api/v1/admin/portal-configurations

Description: Create or update per-portal configuration and enablement controls for future-ready portal placeholders.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| portalCode | string | required |
| enabled | boolean | required |
| mode | string | required |
| packagingTemplateSetId | string | optional |
| notes | string | optional |

### Response 201

```json
{
  "id": "prt-001",
  "portalCode": "leedOnline",
  "enabled": false,
  "mode": "manualPlaceholder"
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | tenant not found |
| 500 | internal server error |

## POST /api/v1/projects/{projectId}/transfer-requests

Description: Create a dual-admin project transfer request with explicit approver tracking.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |
| Path | projectId string required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| sourceOrganizationId | string | required |
| targetOrganizationId | string | required |
| sourceHandoverAccessExpiresAt | string(date-time) | optional |

### Response 201

```json
{
  "id": "ptr-001",
  "projectId": "prj-001",
  "approvalStatus": "pending",
  "requiredApprovalCount": 2,
  "approvalCount": 0,
  "approverIds": [],
  "segregationOfDutiesPassed": false
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | project not found |
| 500 | internal server error |

## POST /api/v1/restores

Description: Request restore of a soft-deleted record within the applied restore window.

### Request

| Item | Value |
| --- | --- |
| Headers | Authorization required |
| Headers | X-Tenant-Id required |
| Headers | Idempotency-Key required |

### Body Schema

| Field | Type | Required |
| --- | --- | --- |
| entityType | string | required |
| entityId | string | required |

### Response 201

```json
{
  "id": "rst-001",
  "entityType": "Artifact",
  "entityId": "art-001",
  "status": "requested",
  "restoreWindowDaysApplied": 30
}
```

### Errors

| HTTP Status | Error |
| --- | --- |
| 400 | validation error or restore window expired |
| 401 | unauthorized |
| 403 | forbidden |
| 404 | entity not found |
| 500 | internal server error |

# Webhook Contracts

## Event Types

| Event Type |
| --- |
| artifact.added |
| classification.changed |
| score.updated |
| simulation.completed |
| submission.packaged |
| auditor.query.received |
| auditor.query.resolved |

## Headers

| Header |
| --- |
| `X-AEON-Event` |
| `X-AEON-Delivery-Id` |
| `X-AEON-Timestamp` |
| `X-AEON-Signature` |

## Signature Algorithm

| Item | Value |
| --- | --- |
| Algorithm | HMAC-SHA256 |
| Signing content | `timestamp + "." + requestBody` |

## Retry Policy

| Attempt Schedule | Value |
| --- | --- |
| Attempt 1 | 1 minute |
| Attempt 2 | 5 minutes |
| Attempt 3 | 15 minutes |
| Attempt 4 | 60 minutes |
| Attempt 5 | 6 hours |
| Maximum attempts | 5 |
| Terminal handling | dead-letter after maximum attempts |

## Secret Management

| Item | Value |
| --- | --- |
| storage | DPAPI-protected secret reference |
| rotation | rotating secrets supported for webhook subscriptions |

## Payload Example

```json
{
  "id": "evt-001",
  "eventType": "simulation.completed",
  "tenantId": "ten-001",
  "occurredAt": "2026-06-09T12:20:00Z",
  "resourceType": "SimulationJob",
  "resourceId": "sim-001",
  "data": {
    "projectId": "prj-001",
    "status": "completed"
  }
}
```

# State Models

## Project Workflow State Model

| Flow |
| --- |
| ProjectIntake → PreAssessment |
| PreAssessment → EvidenceCollection |
| EvidenceCollection → SimulationOrchestration |
| SimulationOrchestration → DocumentationNarrativesCalculators |
| DocumentationNarrativesCalculators → PackagingForSubmission |
| PackagingForSubmission → ManualPortalUpload |
| ManualPortalUpload → AuditorQA |
| AuditorQA → Resubmission |
| Resubmission → ManualPortalUpload |
| AuditorQA → CertifiedClosed |

### Triggers

| Trigger |
| --- |
| intake completed |
| pre-assessment approved by human reviewer |
| minimum evidence state achieved |
| required simulations completed |
| documents approved |
| package approved |
| manual upload completed by user |
| auditor query cycle closed |
| certification closure |

## Evidence Workflow State Model

| Flow |
| --- |
| NotRequested → Requested |
| Requested → InProgress |
| InProgress → Submitted |
| Submitted → UnderReview |
| UnderReview → Approved |
| UnderReview → RevisionRequired |
| RevisionRequired → Resubmitted |
| Resubmitted → UnderReview |
| UnderReview → Rejected |
| Requested/InProgress → Overdue |
| Requested → WaivedNotApplicable |
| Approved/WaivedNotApplicable → LockedForSubmission |

### Triggers

| Trigger |
| --- |
| scheduler created request |
| owner starts work |
| owner submits evidence |
| reviewer approves/rejects |
| due date breach |
| waiver action |
| submission lock |

## Auditor Q and A State Model

| Flow |
| --- |
| QueryReceived → Assigned |
| Assigned → DraftResponse |
| DraftResponse → InternalReview |
| InternalReview → Approved |
| Approved → SentShared |
| SentShared → AuditorFollowUp |
| AuditorFollowUp → DraftResponse |
| SentShared → Resolved |
| Resolved → Reopened |
| Reopened → Assigned |

### Triggers

| Trigger |
| --- |
| query intake |
| assignment |
| draft save |
| internal approval |
| send response |
| auditor follow-up |
| resolution |
| reopen action |

## Simulation Job State Model

| Flow |
| --- |
| queued → preparing |
| preparing → running |
| running → completed |
| running → retrying |
| retrying → queued |
| running → failed |
| failed → deadLettered |
| any active → cancelled |

### Triggers

| Trigger |
| --- |
| worker assignment |
| input preparation success |
| tool success |
| failure with retries remaining |
| failure without retries |
| admin cancel |

## Submission Package State Model

| Flow |
| --- |
| pending → approved |
| pending → rejected |

### Triggers

| Trigger |
| --- |
| package creation starts approval workflow |
| required dual approvals complete |
| rejection recorded |

## Audit Export Request State Model

| Flow |
| --- |
| requested → pendingApproval |
| pendingApproval → approved |
| pendingApproval → rejected |
| approved → released |
| any active → failed |

### Triggers

| Trigger |
| --- |
| request created |
| required business Admin and Technical Admin approvals |
| rejection by approver |
| export generation success |
| export generation failure |

# Queue and Async Design

## Queue Tables

| Queue Table |
| --- |
| JobQueue |
| JobLease |
| JobRetry |
| JobDeadLetter |
| ScheduledTask |

## Job Types

| Job Type |
| --- |
| artifact.scan |
| artifact.ocr |
| artifact.extract |
| artifact.classify |
| project.preAssessment |
| scorecard.recalculate |
| simulation.run |
| simulation.ingest |
| document.generate |
| package.build |
| notification.dispatch |
| analytics.refresh |
| standards.refresh |
| monitor.riskUpdate |
| audit.export |
| training.permission.audit |

## Lease and Retry Rules

| Rule | Value |
| --- | --- |
| standard jobs | service-defined retry handling |
| simulation jobs | 2 retries |
| webhook deliveries | 5 attempts |
| lease timeout renewals | every worker heartbeat |
| dead-letter jobs | remain queryable and auditable |

# Security Architecture

## Authentication

| Control | Value |
| --- | --- |
| authentication mode | local authentication at MVP |
| password | strong password policy |
| MFA | optional MFA |
| session timeout | configurable |
| external API auth | OAuth2 client credentials |

## Authorization

### RBAC scopes

| Scope |
| --- |
| tenant |
| org |
| portfolio |
| project |
| credit |

### Admin-configurable permissions

| Permission Domain |
| --- |
| Projects |
| Evidence |
| Scorecards |
| Simulations |
| Narratives |
| Submissions |
| Q and A |
| Settings |

### Additional Controls

| Control |
| --- |
| policy engine for sensitive actions |
| object-level enforcement in services |

## Data Protection

| Control | Value |
| --- | --- |
| transport | TLS 1.2+ |
| database | SQL TDE |
| storage | encrypted storage |
| secrets | DPAPI secret protection |
| residency | India-only residency for data/logs/backups/DR |
| AI log visibility | prompt/response visibility restricted |
| roadmap | optional CMK/HSM remains roadmap |

## AI Governance

| Control | Value |
| --- | --- |
| AI routing | all AI through internal self-hosted India AI Gateway |
| approved external provider | OpenAI GPT 5.1 only via AI Gateway |
| direct provider access | prohibited for domain services |
| privacy | PII minimization and masking |
| cross-border processing | cross-border processing may occur for LLM API calls with PII minimization as allowed by FRD |
| provider training | no provider training |
| provider retention | no provider retention |
| training authorization | explicit training permission records if customer-authorized historical learning input is used |
| audit | immutable AI audit logs |
| embeddings | tenant-scoped embeddings |
| degradation | eligible services may fall back to rules-based or non-AI-assisted behavior when the external AI provider is unavailable |

## File Security

| Control |
| --- |
| antivirus on ingest |
| file whitelist validation |
| checksum generation |
| duplicate detection |
| quarantine path for infected content |

## Governance Controls

| Control |
| --- |
| restricted audit exports require one business Admin approver and one Technical Admin approver, with requester and approver separation |
| project transfer requires dual admin approval |
| override and submission approvals enforce SoD |
| normal UI cannot delete audit logs |
| DSR SLA 30 days |

# Search Design

| Capability | Value |
| --- | --- |
| filename search | fuzzy |
| extracted content search | fuzzy |
| highlighting | enabled |
| filter facets | project, credit, stakeholder, status, media type, sourceSystem |
| aggregations | enabled |
| lifecycle | lifecycle-managed indices |
| snapshots | India-resident snapshots |

# Mixed-Use Apportionment Design

| Rule | Value |
| --- | --- |
| applicability | apportionment engine invoked only for mixed-use or multi-functional projects |
| bypass | bypassed for single-use projects |
| energy allocations | use EUI precedence |
| non-energy credits | use GFA governance |
| rounding | to one decimal place |
| overrides | supported through audited override workflow |
| recalculation | score recalculation triggered after apportionment changes |

# Localization Design

| Area | Value |
| --- | --- |
| launch locale | English India |
| date/number formatting | Indian defaults |
| unit systems | SI and Imperial at launch |
| timezone default | Asia/Kolkata for India |
| extensibility | all UI strings and unit labels externalized for future localization expansion |

# First-Time Data Load Design

## Scope

| Data Set |
| --- |
| initial master data |
| configuration data |
| rating libraries |
| addenda history |
| regional profiles |
| role templates |
| workflow templates |
| branding defaults |
| notification templates |
| provider metadata |
| licensed corpus metadata |
| approved historical references |

## Input Sources

| Input Source |
| --- |
| flat files |
| spreadsheets |
| approved APIs |
| licensed corpus packages |
| admin-provided configuration bundles |
| legacy exports |
| object storage payloads |

## Execution

| Rule |
| --- |
| controlled admin utility or scripted pipeline |
| allowed in Dev, Test, and UAT for repeated seeding |
| production restricted to approved controlled runs with reconciliation and audit evidence |
| reconciliation report mandatory |
| immutable execution audit required |

## Target Stores

| Target Store |
| --- |
| SQL Server application database |
| Elasticsearch indices |
| object storage or NAS |
| analytics seed structures where required |

## First-Time Load Flow

| Step | Activity | Validation / Control | Output |
| --- | --- | --- | --- |
| 1 | Extract source data | Validate source package integrity, version, authorization, and checksum | Staged source dataset |
| 2 | Validate structure, mandatory fields, duplicates, and referential integrity | Schema validation, mandatory field checks, duplicate detection, reference checks | Validated candidate load set and exception report |
| 3 | Transform and map source values to target schema | Controlled mapping rules, code translation, version compatibility validation | Target-ready transformed dataset |
| 4 | Load data into target store in controlled batches | Batch controls, transaction boundaries, retry rules, dead-letter handling where needed | Loaded records in SQL/search/storage targets |
| 5 | Reconcile loaded counts and business totals | Count reconciliation, spot checks, rule verification, admin sign-off | Reconciliation report and acceptance evidence |
| 6 | Generate execution, exception, and audit reports | Immutable execution logging, operator tracking, timestamping, exception capture | Load audit report, exception report, execution summary |

## Operational Considerations

| Consideration | Design Guidance |
| --- | --- |
| Idempotency / Re-run Strategy | Safe re-run supported using versioned data packs, batch identifiers, duplicate detection, and configurable overwrite/skip behavior for mutable configuration while preserving immutable history where applicable |
| Error Handling | Validation failures produce exception reports; partial failures isolate bad batches/records; dead-letter/error staging for manual correction; production loads require controlled remediation before completion |
| Logging & Audit | Capture batch id, operator, timestamp, input package version, record counts, exception details, target stores affected, and approval references |
| Security | Restrict execution to authorized admins/technical admins; protect secrets using DPAPI; mask PII where applicable; least-privilege access to load targets; validate licensed-content permissions |
| Performance | Batch size and parallelism tuned per data domain; throttling applied for search indexing and storage writes; designed for controlled initialization rather than user-facing latency |

# Sample Data Tool Design

| Rule | Value |
| --- | --- |
| environment | Dev, Test, UAT, and Demo only |
| production exception | Production use prohibited unless explicitly approved for non-live controlled showcases |
| data | synthetic or anonymized data only |
| seed packs | deterministic seed packs |
| reset | repeatable scenario reset |
| audit | execution audit retained |

## Sample Data Coverage

| Category |
| --- |
| users |
| roles |
| organizations |
| portfolios |
| projects |
| intake data |
| artifacts metadata |
| evidence workflows |
| scorecards |
| simulation stubs |
| auditor threads |
| analytics snapshots |
| templates |
| configurations |

## Generation and Volume Profiles

| Area | Design Guidance |
| --- | --- |
| generation method | static seed files, deterministic factory scripts, randomized but bounded generators, and anonymized approved copies where policy permits |
| volume profiles | small for smoke tests, medium for regression/UAT, large for performance and demo scenarios aligned to expected concurrency and artifact volumes |

## Sample Data Controls

| Control Area | Design Guidance |
| --- | --- |
| Data Privacy | Sample data must be synthetic or properly anonymized; no live PII unless specifically approved under policy |
| Referential Integrity | Seed packs must preserve valid relationships across tenants, organizations, projects, evidence, scorecards, and audit references |
| Repeatability | Deterministic seeds or versioned data packs required for repeatable tests and demos |
| Access Control | Only authorized admins, QA, trainers, or DevOps personnel may run the tool in approved non-production environments |
| Audit / Traceability | Log execution id, data pack version, operator, timestamp, scenario name, and affected entities/record counts |

# Deployment Architecture

## Environments

| Environment |
| --- |
| Dev on Windows VMs |
| Test on Windows VMs |
| UAT on Windows VMs |
| Prod on Windows VMs |

## SaaS Multi-Tenant

| Characteristic |
| --- |
| shared control plane |
| shared web/app tier with tenant routing |
| per-tenant storage namespaces |
| per-tenant rate limits and quotas |
| shared application isolation controls supported |

## SaaS Single-Tenant

| Characteristic |
| --- |
| dedicated app tier by tenant |
| dedicated tenant storage namespace |
| shared control plane allowed |
| shared control console may support isolated tenant app instances |
| default preferred SaaS mode |

## On-Prem

| Characteristic |
| --- |
| Windows VMs only |
| IIS + YARP web tier |
| .NET domain services on Windows VMs |
| worker tier on Windows VMs |
| SQL Server 2022 Standard |
| NAS or equivalent encrypted storage |
| Elasticsearch |
| outbound allowlists to AI/tool licensing/weather sources |
| no containerization required |

## Deployment Controls

| Control | Value |
| --- | --- |
| release pattern | blue-green deployments with approvals before production release |
| configuration | environment-specific configuration management |
| feature rollout | feature flags for staged rollout and controlled disablement |
| cloud deployment automation | scripted installers and IaC for cloud |

## Network Layout

| Rule | Value |
| --- | --- |
| inbound | 443 only |
| realtime | WSS on 443 for real-time |
| network | internal private subnets |
| exposure | no public DB/search/storage exposure |
| egress | optional static egress IP |

## DR and Backup

| Control | Value |
| --- | --- |
| backups | daily backups |
| PITR | 15 minutes |
| file snapshots | daily file snapshots with 30-day retention |
| DR residency | India-only DR targets |
| replication | active-active cross-site replication baseline |
| baseline RPO | 24 hours |
| baseline RTO | 24 hours |
| testing | quarterly failover tests |

# Observability

## Metrics

| Metric |
| --- |
| API latency p50/p95/p99 |
| UI latency |
| search latency |
| upload-to-processing latency |
| pre-assessment duration |
| interpretation duration |
| score recalculation duration |
| recommendation duration |
| simulation queue wait |
| simulation runtime |
| export duration |
| package duration |
| notification delivery success |
| dashboard load |
| queue depth |
| throughput |
| availability |
| error budget burn |
| anomaly false-positive rate |
| OCR field accuracy benchmark |
| classification precision and recall benchmark |

## Dashboards

| Dashboard |
| --- |
| technical operations |
| tenant operations |
| analytics executive dashboard |
| AI explainability dashboard |
| notification dashboard |
| DR and backup dashboard |

## SLO and Maintenance Governance

| Control | Value |
| --- | --- |
| availability target | 99.9 percent monthly |
| error budget review | weekly reviews |
| maintenance notice | announced at least 48 hours in advance |

# Dependency Failure Behavior

| Dependency | Behavior |
| --- | --- |
| AI Gateway unavailable | disable only AI-assisted features |
| OpenAI GPT 5.1 unavailable | eligible services fall back to rules-based or non-AI-assisted behavior through AI Gateway-managed degradation |
| SendGrid unavailable | retry email and preserve in-app records |
| Meta Cloud API unavailable | retry WhatsApp and preserve in-app/email |
| Weather source unavailable | use cache if possible, else fail simulation preflight |
| Search unavailable | preserve transactional operations and degrade search features |
| Storage or NAS latency | queue long-running flows and retry workers |
| Simulation tool unavailable or no seats | fail preflight or queue based on admin policy |
| SQL Server issue | critical service impact; invoke backup/restore and DR posture |

# Anomaly and Cross-Credit Consistency Design

## Rule Set

| Rule Code | Check | Threshold / Condition | Output |
| --- | --- | --- | --- |
| ANOM-001 | built-up area consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-002 | occupancy consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-003 | location consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-004 | regularly occupied areas consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-005 | fresh air quantity consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-006 | operational hours and schedules consistency | cross-credit value comparison required | anomaly if inconsistent |
| ANOM-007 | unit and currency consistency | cross-document comparison required | anomaly if inconsistent |
| ANOM-008 | area totals vs BIM | variance must be within 1 percent | anomaly if variance exceeds 1 percent |
| ANOM-009 | ventilation rates | must meet ASHRAE 62.1 minimums | anomaly if below minimum |
| ANOM-010 | weather file climate zone match | selected weather file must match project climate zone | anomaly if mismatch |
| ANOM-011 | prescribed baseline model version | submitted baseline model version must match prescribed version | anomaly if mismatch |
| ANOM-012 | energy end-use totals vs whole-building | variance must be within 0.5 percent | anomaly if variance exceeds 0.5 percent |
| ANOM-013 | calculator cell protections | protected cells must remain protected | anomaly if protection missing or altered |
| ANOM-014 | document date/version consistency | related documents must carry consistent date/version metadata | anomaly if inconsistent |

## Corrective Action Handling

| Step | Behavior |
| --- | --- |
| 1 | Scorecard Service evaluates anomaly rules during recalculation and package validation where applicable |
| 2 | AnomalyCheckResult is persisted with measured value, expected value, severity, and rationale |
| 3 | CorrectiveAction records are created or suggested for open anomalies |
| 4 | Responsible users review and resolve, waive, or remediate anomalies under audit |
| 5 | Resolved anomalies trigger recalculation or package revalidation as applicable |

# Final Check
## Final Check

| Check | Status |
| --- | --- |
| All HLD requirements covered | yes |
| No contract changes introduced | yes |
| All APIs fully defined | yes |
| All schemas complete | yes |
| No missing sections | yes |
| Traceability complete | yes |
# Appendices

## Appendix A: Logical Architecture Diagrams

| Diagram | Description |
| --- | --- |
| System context logical diagram | To be attached as logical architecture diagram covering Web Client → API Gateway / IIS / YARP → SignalR Hub and domain microservices → SQL Server, Elasticsearch, Object Storage or NAS, internal AI Gateway, external OpenAI GPT 5.1 provider, notification providers, and simulation tools |
| Container/component logical diagram | To be attached as logical architecture diagram covering role-based web client, ingress, real-time hub, domain services, background workers, internal AI Gateway, and data/integration components aligned to HLD component architecture |

## Appendix B: Deployment Architecture Diagrams

| Diagram | Description |
| --- | --- |
| Environment topology diagram | To be attached as deployment architecture diagram covering Dev, Test, UAT, and Prod on Windows VMs with web, app, worker, and data tiers |
| Deployment/network layout diagram | To be attached as deployment and network layout diagram covering inbound 443 only, WSS on 443, internal private network segments, India-resident data/logs/backups/DR, optional static egress IP, and outbound controls for AI and provider endpoints |

# Traceability Table

| PrevStepID | PrevStepText | ThisStepID | ThisStepText |
| --- | --- | --- | --- |
| HLD-0001 | The platform shall provide a versioned rating system library with admin-controlled updates, addenda tracking, and supported certification framework taxonomies for all FRD-listed rating systems and versions. | MID_LEVEL_DESIGN-0001 | RatingLibrary service and schema define versioned taxonomies, addenda tracking, publish lifecycle, and admin-managed updates for all supported systems. |
| HLD-0002 | The platform shall generate precertification and certification outputs including simulation summaries, narratives, calculators, and form-ready data aligned to supported rating templates. | MID_LEVEL_DESIGN-0002 | Narrative and Document Generation Service with DocumentArtifact schema and export flows generates aligned simulation summaries, narratives, calculators, and form-ready outputs. |
| HLD-0003 | The platform shall implement regional profiles for India climate zones and supported external geographies with weather file selection, code references, region-specific defaults, SI units, IST default timezone, and per-project regional parameters. | MID_LEVEL_DESIGN-0003 | Region Profile Service and RegionalProfile schema implement geography defaults, weather mappings, standards references, SI units, IST defaults, and project parameter overlays. |
| HLD-0004 | The platform shall implement RBAC with admin-configurable view/create/edit/delete permissions for Projects, Evidence, Scorecards, Simulations, Narratives, Submissions, Q and A, and Settings, with audit logging for every permission change. | MID_LEVEL_DESIGN-0004 | Identity and RBAC Service implements configurable permissions, scoped access, default-false custom roles, and append-only permission change audit. |
| HLD-0005 | The platform shall provide AI-assisted pre-assessment pipelines using project data and optional uploaded artifacts to produce feasibility outputs, score tables, graphs, rationale, and stakeholder action items by stakeholder group Architect, Owner, Landscape, MEP, Construction, and Procurement. | MID_LEVEL_DESIGN-0005 | Pre-assessment flow, APIs, and persistence generate feasibility outputs, graphs, rationales, and stakeholder action items across specified groups. |
| HLD-0006 | The platform shall implement a credit interpretation engine combining configurable rules and AI-assisted reasoning with stored rationale, source references, and regional preferences. | MID_LEVEL_DESIGN-0006 | Credit Interpretation Service and InterpretationResult schema implement rules plus AI reasoning with rationale, sources, addenda, preferences, and audit. |
| HLD-0007 | The platform shall provide evidence collection workflows with monthly cadence due on the 5th, reminders 7/3/1 days before due, overdue reminders every 3 days, escalation to Project Manager at 3 days overdue and to Owner at 10 days overdue, ownership, validation checks, and auditable state transitions. | MID_LEVEL_DESIGN-0007 | Evidence Workflow Service, EvidenceTask schema, scheduler logic, and state model implement due day, reminders, escalations, ownership, validations, and audit. |
| HLD-0008 | The platform shall orchestrate supported simulation workflows and result ingestion using approved tools, EPW weather sources, and acceptable approximations aligned to applicable standards. | MID_LEVEL_DESIGN-0008 | Simulation Orchestrator, worker queueing, EPW selection, reproducibility metadata, and ingestion flows implement supported simulation orchestration. |
| HLD-0009 | The platform shall assemble submission packages using default naming pattern ProjectCode_CreditID_DocType_v1.0.ext with versioning 1.0/1.1/2.0 auto-stamped in footers, checksum tracking, immutable package history, manual-download support, and template version pins. | MID_LEVEL_DESIGN-0009 | Packaging Service and SubmissionPackage schema implement naming convention enforcement, constrained package versions 1.0/1.1/2.0, version footer stamping, checksum manifests, immutable history, and template pins. |
| HLD-0010 | The platform shall implement auditor Q and A workflows with configurable states, evidence-linked responses, SLA handling, escalation rules, and reusable canned response libraries. | MID_LEVEL_DESIGN-0010 | Auditor Q and A Service, AuditorQuery schema, claim-level evidence links, canned responses, default state model, transitions, and SLA controls implement configured workflow behavior. |
| HLD-0011 | The platform shall provide future-ready portal integration placeholders with per-portal configuration and enablement controls while defaulting to manual submission packaging and no RPA at MVP. | MID_LEVEL_DESIGN-0011 | Admin and Governance controls, PortalConfiguration schema, and portal configuration API preserve portal placeholders while enforcing manual packaging only and no RPA at MVP. |
| HLD-0012 | The platform shall support import, export, result ingestion, and schema mapping workflows for supported third-party tools and data sources. | MID_LEVEL_DESIGN-0012 | External integration matrix, Artifact source metadata fields, and Windows On-Prem Connector implement import, export, result ingestion, and interchange workflows. |
| HLD-0013 | The platform shall expose REST APIs and HMAC-signed webhooks for artifact added, classification changed, score updated, simulation completed, submission packaged, and auditor query received or resolved using OAuth2 client credentials, optional IP allowlisting, and rotating secrets. | MID_LEVEL_DESIGN-0013 | API Gateway, webhook contracts, WebhookSubscription schema, event payloads, OAuth2, HMAC signing, retries, allowlisting, and secret rotation implement secure integrations. |
| HLD-0014 | The platform shall ingest supported artifact types at required scale with upload-time validation, indexing, duplicate detection, and performant repository search. | MID_LEVEL_DESIGN-0014 | Ingestion Service, Artifact schema, duplicate detection, search indexing, and search APIs implement scalable ingest and repository search. |
| HLD-0015 | The platform shall perform OCR, structured extraction, and BIM data extraction with human review and correction workflows. | MID_LEVEL_DESIGN-0015 | OCR and Extraction Service, BIM Extraction Service, and ExtractionResult schema implement OCR, structured extraction, BIM extraction, and correction workflows. |
| HLD-0016 | The platform shall classify artifacts to credits using rules and tenant-scoped embeddings without provider-side training and maintain searchable evidence linkage. | MID_LEVEL_DESIGN-0016 | Classification Service and ClassificationResult schema implement rule and embedding classification, no provider training, tenant-scoped embeddings, and searchable linkage. |
| HLD-0017 | The platform shall maintain dynamic scorecards with prerequisites, dependencies, override controls, dual approvals, and auditable scoring history. | MID_LEVEL_DESIGN-0017 | Scorecard Service, Scorecard and ScoreOverride schemas, override workflow, mixed-use apportionment, anomaly checks, and scoring history implement dynamic score control. |
| HLD-0018 | The platform shall generate compliance pathway recommendations with tradeoffs, dependencies, points impact, and what-if comparisons. | MID_LEVEL_DESIGN-0018 | Recommendation Service and what-if scenario flow implement pathways, tradeoffs, dependencies, expected impacts, and comparison support. |
| HLD-0019 | The platform shall generate branded narratives, calculators, and form entries aligned to configured templates and document branding settings. | MID_LEVEL_DESIGN-0019 | Document generation with template pins and branding assets applies configured templates to narratives, calculators, and form entries. |
| HLD-0020 | The platform shall provide standards and CIR Q and A over customer-uploaded licensed and permitted public content with citation style Standard Name/Clause or Section/Year/Page or Paragraph/URL if public, version identifiers, and admin-configurable refresh cadence default monthly. | MID_LEVEL_DESIGN-0020 | Standards and CIR Q and A Service, StandardCorpusItem schema, citation enforcement, version identifiers, refresh cadence, and access controls implement standards QA. |
| HLD-0021 | The platform shall implement anomaly and cross-credit consistency checks for built-up area, occupancy, location, regularly occupied areas, fresh air quantity, operational hours and schedules, unit and currency consistency, area totals vs BIM within 1 percent, ventilation rates meeting ASHRAE 62.1 minimums, weather file climate zone match, prescribed baseline model version, energy end-use totals equal whole-building within 0.5 percent, calculator cell protections, and document date/version consistency, with corrective actions. | MID_LEVEL_DESIGN-0021 | Anomaly and Cross-Credit Consistency Design, AnomalyCheckResult schema, CorrectiveAction schema, and Scorecard Service implement the required rule set, thresholds, and corrective-action handling. |
| HLD-0022 | The platform shall enforce human review and approval controls for extracted data, narratives, overrides, and submissions with full auditability and configurable segregation of duties. | MID_LEVEL_DESIGN-0022 | Review states, approval controls, policy engine checks, and audit records enforce human review and configurable segregation of duties across targeted workflows. |
| HLD-0023 | The platform shall provide explainability views including confidence, rationale, source links, evidence links, and model identifiers, with restricted prompt or response audit access. | MID_LEVEL_DESIGN-0023 | Audit and Explainability Service plus AIInteractionLog schema store confidence, rationale, evidence links, source citations, and restricted AI logs. |
| HLD-0024 | The platform shall produce exports in PDF, DOCX, XLSX, JSON, and PPTX for supported outputs with branding, version footers, optional watermarking, and checksum verification. | MID_LEVEL_DESIGN-0024 | Export formats, footer stamping, watermark flags, checksum storage, and document APIs implement full export contract. |
| HLD-0025 | The platform shall provide real-time in-app, email, and WhatsApp notifications with configurable rules per event and WhatsApp consent capture using in-app explicit opt-in with timestamped audit record, email opt-in link, and user-initiated WhatsApp message methods. | MID_LEVEL_DESIGN-0025 | Notification Service, NotificationRule, NotificationSettings, Notification schema, and WhatsAppConsent schema implement configurable multi-channel notifications, quiet hours, and consent capture with timestamped audit record. |
| HLD-0026 | The platform shall provide project and portfolio analytics dashboards for energy, carbon, water, waste, acceptance rate, and cycle time with filtering and grouping by rating system, version, geography, typology, and time, plus export support. | MID_LEVEL_DESIGN-0026 | Analytics Service, KPIRecord schema, warehouse refresh, dashboard filters/groupings, and export support implement project and portfolio analytics. |
| HLD-0027 | The platform shall scale storage and processing for at least the FRD-defined monthly artifact and data ingestion volumes with responsive pipelines and search. | MID_LEVEL_DESIGN-0027 | Queue design, worker scaling, storage layout, and capacity targets implement scalable processing and storage growth support. |
| HLD-0028 | The platform shall capture and expose operational metrics supporting SLO monitoring, usage tracking, and performance governance. | MID_LEVEL_DESIGN-0028 | Observability design, metrics set, dashboards, and alerts implement operational metrics for SLO and usage governance. |
| HLD-0029 | The platform shall implement security and compliance controls aligned to ISO 27001, SOC 2, DPDP, GDPR/CCPA readiness, encryption, India residency, and auditable access control requirements. | MID_LEVEL_DESIGN-0029 | Security architecture, residency, encryption, RBAC, audit controls, privacy safeguards, and DSR SLA implement the required control posture. |
| HLD-0030 | The platform shall support SaaS multi-tenant, SaaS single-tenant, and on-prem deployment patterns on Windows VMs with required outbound connectivity controls. | MID_LEVEL_DESIGN-0030 | Deployment architecture defines SaaS multi-tenant, SaaS single-tenant, and on-prem Windows VM patterns with outbound allowlists. |
| HLD-0031 | The platform shall support local account management at launch with audit trails and roadmap support for SSO and SCIM. | MID_LEVEL_DESIGN-0031 | Identity design implements local accounts and audit trails while preserving future SSO and SCIM integration path. |
| HLD-0032 | The platform shall implement configurable retention, backup, tenant export, restore, and secure deletion controls. | MID_LEVEL_DESIGN-0032 | RetentionPolicy, TenantExportRequest, RestoreRequest, SecureDeletionRequest, backup rules, and DR design implement configurable retention, export, restore, and deletion. |
| HLD-0033 | The platform shall restrict AI-assisted learning inputs to customer-permitted historical data with configured redaction, tenant isolation, retention controls, and no provider training. | MID_LEVEL_DESIGN-0033 | Historical Data Ingestion Service, TrainingPermissionRecord, AIInteractionLog fields, and AI controls implement explicit permission gating, redaction, tenant isolation, and no provider training. |
| HLD-0034 | The platform shall manage licensed and curated standards corpora with legal validation, access scope control, and expiry enforcement. | MID_LEVEL_DESIGN-0034 | Standards corpus metadata, access validation, expiry blocking, and audit logging implement licensed corpus governance. |
| HLD-0035 | The platform shall support English, SI and Imperial units, and Indian date and number formats with future localization expansion controls and externalized text and units for future i18n. | MID_LEVEL_DESIGN-0035 | Localization design externalizes strings and unit labels and configures English India with Indian date and number formats and SI and Imperial units at launch, while API wire timestamps remain ISO 8601 UTC strings. |
| HLD-0036 | The platform shall define and track success metrics including time to certification, first-pass acceptance, cost savings, and user adoption with baseline capture at onboarding and continuous KPI dashboards. | MID_LEVEL_DESIGN-0036 | KPIRecord model, analytics warehouse, onboarding baseline capture, and dashboards implement success metric tracking. |
| HLD-0037 | The platform shall support milestone and budget tracking with baseline, rebaseline, threshold, variance alert, acknowledgement, stage gates, budget guardrails, and periodic reviews. | MID_LEVEL_DESIGN-0037 | Milestone and Budget schemas, alert logic, acknowledgements, and governance flows implement timeline and budget management. |
| HLD-0038 | The platform shall maintain project and portfolio risk registers with ownership, severity, likelihood, mitigations, linked affected entities, and automated monitors. | MID_LEVEL_DESIGN-0038 | RiskRegisterItem schema, monitor.riskUpdate jobs, and admin controls implement project and portfolio risk registers with ownership and mitigations. |
| HLD-0039 | The platform shall enforce WhatsApp consent, privacy compliance, unsubscribe handling, template approvals, and non-use of RPA for portal operations. | MID_LEVEL_DESIGN-0039 | WhatsAppConsent schema, notification controls, template scope enforcement, and explicit integration constraints implement required privacy and non-RPA behavior. |
| HLD-0040 | The platform shall enforce governance workflows for cross-organization sharing, project transfer controlled by dual-admin approval, and sensitivity-based approval policies. | MID_LEVEL_DESIGN-0040 | AccessInvite, AccessGrant, ProjectTransferRequest, and PolicyRule schemas implement invite-only sharing, project transfer governance, and sensitivity-based approval policies. |
| HLD-0041 | The platform shall generate prioritized design recommendations for energy, carbon, daylight, glare, and visual comfort with quantitative or indicative impact outputs backed by simulations or calculators within runtime budgets where applicable. | MID_LEVEL_DESIGN-0041 | Recommendation Service implements prioritized multi-domain recommendations with quantitative impacts and simulation/calculator backing where applicable. |
| HLD-0042 | The platform shall capture and apply AEON branding assets including logo, colors, and fonts across templates and customer-facing documents. | MID_LEVEL_DESIGN-0042 | Branding administration and document rendering apply AEON branding assets across templates and outputs. |
| HLD-0043 | The platform shall define an explicit MVP project intake data model with mandatory, optional, defaulted, and system-derived fields covering project, site, owner, stakeholder, timeline, budget, and import metadata. | MID_LEVEL_DESIGN-0043 | Project Intake Service and ProjectIntakeRecord schema implement explicit intake fields, defaults, validations, and import metadata handling. |
| HLD-0044 | The platform shall invoke the apportionment rule engine only for mixed-use or multi-functional projects and bypass apportionment for single-use projects. | MID_LEVEL_DESIGN-0044 | Mixed-use apportionment design explicitly invokes engine only for mixed-use or multi-functional projects and bypasses for single-use projects. |
| HLD-0045 | The platform shall implement mixed-use apportionment rules using EUI precedence for energy allocations, GFA governance for non-energy credits, one-decimal rounding, override workflow, and score recalculation. | MID_LEVEL_DESIGN-0045 | Scorecard Service mixed-use apportionment logic applies EUI precedence, GFA governance, one-decimal rounding, override handling, and recalculation triggers. |
| HLD-0046 | The platform shall support limited-scope thermal comfort and carbon outputs through imported results, derived KPIs, downloadable summaries, recommendations, and indicative score impacts where configured. | MID_LEVEL_DESIGN-0046 | Recommendation and analytics design support imported or derived thermal comfort and carbon outputs with summaries and indicative score impacts. |
| HLD-0047 | The platform shall support admin-configurable import templates by artifact type including metadata schema, mandatory fields, validation rules, naming rules, and default credit mappings. | MID_LEVEL_DESIGN-0047 | ImportTemplate schema, import template API, and ingestion enforcement implement admin-configurable artifact-type templates with metadata, validation, naming, and default credit mappings. |
| HLD-0048 | The platform shall enforce a default upload-time maximum file size of 100 MB per artifact with admin-configurable overrides by artifact type. | MID_LEVEL_DESIGN-0048 | Ingestion validation rules, ImportTemplate schema, and artifact upload API enforce default 100 MB artifact limit with admin-configurable per-type override support. |
| HLD-0049 | The platform shall provide default evidence workflow states out of the box and allow tenant-level and project-level workflow customization with complete state transition auditing. | MID_LEVEL_DESIGN-0049 | Evidence state model and Admin/Governance configuration implement default workflows, customization points, and full transition auditing. |
| HLD-0050 | The platform shall support tenant-level business calendar defaults and project-level override for SLA calculations, reminders, and escalations. | MID_LEVEL_DESIGN-0050 | BusinessCalendar schema and evidence scheduling logic implement tenant defaults and project-level override behavior for SLAs and reminders. |
| HLD-0051 | The platform shall emit in-app notifications for assignments, reminders, escalations, approvals, uploads, classification completion, score changes, simulation completion, submission packaging, auditor events, and system alerts. | MID_LEVEL_DESIGN-0051 | Notification Service and NotificationRule schema define event-driven in-app notifications across assignments, reminders, escalations, approvals, uploads, score changes, simulations, packages, auditor events, and alerts. |
| HLD-0052 | The platform shall restrict WhatsApp at MVP to workflow and operational notifications and exclude authentication-related messaging. | MID_LEVEL_DESIGN-0052 | Notification design explicitly restricts WhatsApp usage to workflow and operational events and excludes authentication use. |
| HLD-0053 | The platform shall support lightweight project control workflows for milestone create, edit, baseline, rebaseline, budget thresholding, variance alerting, and acknowledgement actions. | MID_LEVEL_DESIGN-0053 | Milestone and Budget schemas plus alert and acknowledgement logic implement lightweight project control workflows. |
| HLD-0054 | The platform shall implement cross-organization sharing as invite-based and read-only by default, with explicit elevation required for any additional permissions. | MID_LEVEL_DESIGN-0054 | AccessInvite and AccessGrant design enforces invite-based sharing with read-only default and explicit elevation through scoped grant. |
| HLD-0055 | The platform shall transfer full project ownership and history between organizations with configurable source handover access and automatic expiry unless extended. | MID_LEVEL_DESIGN-0055 | ProjectTransferRequest workflow, explicit handover expiry fields, extension indicator, and governance controls preserve project continuity and support controlled handover access patterns with audit. |
| HLD-0056 | The platform shall require dual approval by Admin and Technical Admin for restricted audit exports, with separation between requester and approver. | MID_LEVEL_DESIGN-0056 | AuditExportRequest schema, state model, and APIs enforce Admin plus Technical Admin approval separation for restricted audit exports. |
| HLD-0057 | The platform shall manage provider credentials at tenant scope by default and support project-level overrides where required. | MID_LEVEL_DESIGN-0057 | Provider credentials API and admin design support tenant-scoped credentials with optional project override behavior. |
| HLD-0058 | The platform shall provide basic license management including tool inventory, expiry alerts, low-seat alerts, invalid configuration alerts, seat assignment records, mode flags, and checkout audit. | MID_LEVEL_DESIGN-0058 | License Management Service, LicenseSeatAssignment schema, and operational controls implement inventory, expiry and seat alerts, invalid configuration alerts, seat assignment records, mode flags, and checkout audit. |
| HLD-0059 | The platform shall support historical data ingestion approval workflows using an admin-configurable Data Owner approver role rather than assuming the application Owner role. | MID_LEVEL_DESIGN-0059 | Historical Data Ingestion Service implements approval workflows with an admin-configurable Data Owner approver role and explicit written permission validation. |
| HLD-0060 | The platform shall drive cross-organization sharing approval rules from project sensitivity classification, including dual approval for sensitive projects. | MID_LEVEL_DESIGN-0060 | PolicyRule and governance workflows use project sensitivity inputs to drive approval requirements including dual approval for sensitive projects. |
| HLD-0061 | The platform shall support a default 30 calendar day restore window for soft-deleted records with tenant-level configurability subject to policy. | MID_LEVEL_DESIGN-0061 | RetentionPolicy field, RestoreRequest schema, and restore API define default 30-day soft-delete restore window with tenant policy configurability. |
| HLD-0062 | The platform shall provide a configurable rating system framework supporting all specified systems with versioned credit taxonomies and an update SLA of 15 business days for addenda and interpretations. | MID_LEVEL_DESIGN-0062 | Rating Library Service implements configurable framework, versioned taxonomies, and 15-business-day SLA controls. |
| HLD-0063 | The platform shall provide a simulation orchestration service with p95 peak queue time under 10 minutes, single daylight or glare job max runtime 2 hours, single energy job max runtime 6 hours, and reproducible runs with inputs and seeds logged. | MID_LEVEL_DESIGN-0063 | SimulationOrchestrator and SimulationJob schema enforce queue/runtime budgets and log seeds and fingerprints for reproducibility. |
| HLD-0064 | The platform shall implement region profiles for India and target geographies with code reference libraries, SI units, IST default timezone, and per-project regional parameters. | MID_LEVEL_DESIGN-0064 | Region profiles, standards/code references, SI defaults, IST defaults, and project overlays are fully implemented in Region Profile design. |
| HLD-0065 | The platform shall implement RBAC for listed roles supporting 350 named users and at least 100 concurrent users with p95 UI read latency under 1 second and write under 2 seconds. | MID_LEVEL_DESIGN-0065 | Identity/RBAC architecture, seeded roles, and performance targets support required user counts and UI latency targets. |
| HLD-0066 | The platform shall provide an AI pre assessment pipeline processing 4 MB artifacts with p95 end to end latency under 90 seconds including OCR and extraction, with confidence scores, rationales, and India resident processing. | MID_LEVEL_DESIGN-0066 | Pre-assessment flow, OCR/extraction integration, AI gateway routing, and observability metrics implement required performance and explainability behavior. |
| HLD-0067 | The platform shall provide a credit interpretation engine using rule sets and ML with full audit trails and versioned rules per rating system and location and p95 interpretation under 5 seconds. | MID_LEVEL_DESIGN-0067 | Interpretation design uses versioned rule execution, AI assistance, audit logging, and latency targets for credit interpretation. |
| HLD-0068 | The platform shall provide evidence workflows with assignment, configurable SLAs, weekly reminders where configured, 99 percent monthly delivery success, and auditable state transitions. | MID_LEVEL_DESIGN-0068 | Evidence workflow scheduling, weekly reminder option, notification delivery metrics, and state audit trails implement required evidence operations. |
| HLD-0069 | The platform shall provide simulation connectors via API or CLI using sandboxed Windows VM workers including optional on-prem offline runners, auto retry failed jobs twice, and ingest results within 5 minutes of completion. | MID_LEVEL_DESIGN-0069 | Simulation worker architecture and on-prem connector design implement API or CLI connectivity, two retries, and ingestion within 5 minutes. |
| HLD-0070 | The platform shall provide a submission package assembler that builds portal compliant packages up to 100 artifacts within 30 minutes p95 and stores immutable submission history with checksums and template version pins. | MID_LEVEL_DESIGN-0070 | Packaging design, queue processing, performance targets, immutable history, checksum manifests, and template pins implement package assembly requirements. |
| HLD-0071 | The platform shall provide an auditor Q and A assistant that generates suggestions with evidence citations, requires human approval before sending, preserves conversation context, and targets p95 suggestion time under 10 seconds. | MID_LEVEL_DESIGN-0071 | Auditor Q and A assistant flow, citation handling, human approval gate, and context persistence implement assistant requirements. |
| HLD-0072 | The platform shall prefer official APIs and support future RPA only with explicit authorization, vaulted bot credentials, IP allowlists, and India-resident integration infrastructure. | MID_LEVEL_DESIGN-0072 | Integration constraints, provider credential controls, IP allowlisting, and feature-flagged future capabilities preserve official-API-first strategy and deferred RPA. |
| HLD-0073 | The platform shall provide secure integrations to tools via APIs or file exchange supporting current and previous two major versions and a Windows on-prem connector for air-gapped sites. | MID_LEVEL_DESIGN-0073 | External integration matrix and Windows On-Prem Connector implement secure file or API exchange with supported version compatibility where applicable. |
| HLD-0074 | The platform shall provide OpenAPI-documented REST endpoints for artifact upload, artifact status, score retrieval, package status, auditor query status, and limited admin automation, with 50 requests per second per tenant, 200 burst, p95 latency 500 ms for reads and 1 second for writes, and POST idempotency keys supported. | MID_LEVEL_DESIGN-0074 | Public API contracts, standards, rate limits, idempotency, and latency targets implement the required OpenAPI-documented REST surface. |
| HLD-0075 | The platform shall use Windows Service based background processing with SQL-backed queues, retry handling, and dead-letter patterns for long-running tasks. | MID_LEVEL_DESIGN-0075 | Queue tables, worker service responsibilities, retry rules, and dead-letter handling implement Windows Service based background processing. |
| HLD-0076 | The platform shall use Tesseract English OCR with custom extraction pipelines for tables and key-value data targeting 95 percent field accuracy on curated benchmarks. | MID_LEVEL_DESIGN-0076 | OCR and Extraction Service uses Tesseract English with custom pipelines and benchmark tracking to meet targeted field accuracy. |
| HLD-0077 | The platform shall use IFC and exported schedules as the primary BIM extraction inputs and allow RVT upload for reference only at MVP. | MID_LEVEL_DESIGN-0077 | BIM Extraction Service prioritizes IFC and exported schedules and stores RVT as reference-only artifacts at MVP. |
| HLD-0078 | The platform shall classify artifacts to credits with precision and recall of at least 90 percent and provide a searchable repository with reviewer correction loop. | MID_LEVEL_DESIGN-0078 | Classification quality metrics, searchable repository design, and reviewer correction workflow implement classification performance and usability targets. |
| HLD-0079 | The platform shall maintain a dynamic scorecard recalculating within 5 seconds of changes with auditable overrides and scoring history. | MID_LEVEL_DESIGN-0079 | Score recalculation flow, performance target, override workflow, and scoring history persistence implement dynamic scorecard behavior. |
| HLD-0080 | The platform shall provide recommendation outputs with prerequisite validation, tradeoff analysis, and quantified impact deltas within 10 seconds for a credit set. | MID_LEVEL_DESIGN-0080 | Recommendation logic, prerequisite checks, tradeoff logic, and quantified impact outputs implement recommendation target behavior. |
| HLD-0081 | The platform shall provide document generation for narratives, calculators and forms in PDF, DOCX, XLSX, PPTX, and JSON with AEON branding assets stored securely and applied consistently. | MID_LEVEL_DESIGN-0081 | Document generation formats, secure branding asset handling, and consistent application across outputs implement document generation requirements. |
| HLD-0082 | The platform shall restrict standards and CIR Q and A to licensed corpora and permitted public content with per-answer citations, content version identifiers, and enforced license checks. | MID_LEVEL_DESIGN-0082 | Standards QA schemas and service logic enforce corpus licensing, citations, content version identifiers, and permitted content boundaries. |
| HLD-0083 | The platform shall provide anomaly and cross-credit discrepancy checks with explainable rules targeting under 10 percent false positives and remediation workflow. | MID_LEVEL_DESIGN-0083 | Anomaly and Cross-Credit Consistency Design, AnomalyCheckResult schema, CorrectiveAction schema, and observability metrics implement discrepancy detection and remediation requirements. |
| HLD-0084 | The platform shall enforce reviews for extracted data, narratives, and submissions with dual approval and configurable segregation of duties. | MID_LEVEL_DESIGN-0084 | Review states, approval flows, and policy engine integration enforce configurable review and dual approval behavior. |
| HLD-0085 | The platform shall capture AI rationale, evidence links, and model metadata and maintain a model registry with version history and referenced datasets. | MID_LEVEL_DESIGN-0085 | AIInteractionLog and ModelRegistry schemas implement AI rationale capture and model lineage. |
| HLD-0086 | The platform shall support parallel export with p95 generation under 2 minutes per document up to 50 pages and 10 MB with optional watermarking and checksum verification. | MID_LEVEL_DESIGN-0086 | Export worker design, performance targets, watermark flags, and checksum storage implement parallel export behavior. |
| HLD-0087 | The platform shall provide notifications via in-app, email, and WhatsApp with escalation if unacknowledged after 2 hours and 99 percent monthly channel delivery success. | MID_LEVEL_DESIGN-0087 | Notification states, acknowledgement tracking, escalation scheduler, quiet hours, and delivery metrics implement the 2-hour escalation and 99 percent target. |
| HLD-0088 | The platform shall provide a tenant-scoped analytics warehouse refreshed daily at 01:00 IST with dashboards for energy, carbon, water, waste, and executive summaries loading under 3 seconds p95. | MID_LEVEL_DESIGN-0088 | Analytics Service and warehouse refresh schedule implement tenant-scoped analytics refresh and executive dashboards with required latency target. |
| HLD-0089 | The platform shall use self-hosted Elasticsearch in India for evidence and metadata search with fuzzy search, highlighting, aggregations, and lifecycle management. | MID_LEVEL_DESIGN-0089 | Search architecture uses India-hosted Elasticsearch with fuzzy search, highlighting, aggregations, and lifecycle management. |
| HLD-0090 | The platform shall establish SLOs including 99.9 percent monthly availability excluding announced maintenance, error budgets, weekly reviews, and maintenance windows announced at least 48 hours in advance. | MID_LEVEL_DESIGN-0090 | Observability and SLO sections define 99.9 percent availability, error budgets, weekly reviews, and 48-hour maintenance notice governance. |
| HLD-0091 | The platform shall route all AI-assisted requests through a self-hosted India AI gateway using GPT 5.1 only with PII minimization, configurable audit retention, and no provider training or retention. | MID_LEVEL_DESIGN-0091 | AI Gateway design models the gateway as an internal platform component that routes all AI requests through self-hosted India controls, applies PII minimization, configurable audit retention, and no provider training or retention, and invokes OpenAI GPT 5.1 as the approved external provider only behind the gateway. |
| HLD-0092 | The platform shall support deployability as Windows VM in any India region cloud and on prem without containerization on prem, support scripted installers and IaC for cloud, use default inbound 443 only and restricted egress to required domains, and support optional static egress IP. | MID_LEVEL_DESIGN-0092 | Deployment architecture and network rules implement Windows VM deployment with scripted installers and IaC for cloud, inbound 443, restricted egress, and optional static egress IP. |
| HLD-0093 | The platform shall implement local authentication with strong password policy, optional MFA, RBAC scopes at org, portfolio, project, and credit levels with custom roles and audit trails, and roadmap support for SAML or OIDC SSO and SCIM post-MVP. | MID_LEVEL_DESIGN-0093 | Identity and RBAC design explicitly implements local auth, strong passwords, optional MFA, scoped custom roles, audits, and future SSO/SCIM path. |
| HLD-0094 | The platform shall use SQL Server 2022 Standard as the tenant transactional store with TDE, PITR, daily backups, daily file snapshots with 30-day retention, raw and derived artifacts retained 36 months by default, logs retained 12 months hot plus 7 years archive, tenant-level export, secure deletion verification, and baseline DR targets of RPO 24 hours and RTO 24 hours. | MID_LEVEL_DESIGN-0094 | SQL Server, domain retention coverage, backup, export, secure deletion, and DR sections implement required database, retention, and recovery configuration. |
| HLD-0095 | The platform shall gate training data use behind explicit written permissions stored in system records, isolate data per tenant, log all usage, and default to opt out. | MID_LEVEL_DESIGN-0095 | TrainingPermissionRecord schema, Historical Data Ingestion Service, and AIInteractionLog fields implement explicit written permission gating, tenant isolation, usage logging, and default opt-out. |
| HLD-0096 | The platform shall store licensed content with license metadata, enforce scope and dates, block use after expiry, and log all access. | MID_LEVEL_DESIGN-0096 | StandardCorpusItem schema and standards access controls implement license metadata enforcement, date checks, expiry blocking, and access logs. |
| HLD-0097 | The platform shall launch as English India with SI units and IST timezone and externalize text and units for future i18n and Imperial support. | MID_LEVEL_DESIGN-0097 | Localization and region design implement English India defaults, SI units, IST defaults, and externalized text and unit configuration. |
| HLD-0098 | The platform shall instrument KPIs for time to certification, first-pass acceptance, cost savings, and adoption with baseline capture at onboarding and continuous dashboards. | MID_LEVEL_DESIGN-0098 | KPI instrumentation, onboarding baselines, analytics warehouse, and dashboards implement continuous KPI monitoring. |
| HLD-0099 | The platform shall define milestones, stage gates, and budget guardrails with tracking, variance alerts, and periodic reviews. | MID_LEVEL_DESIGN-0099 | Milestone and Budget entities, alert logic, acknowledgements, and review workflows implement stage gates and budget guardrails. |
| HLD-0100 | The platform shall maintain a risk register with owners, mitigations, and automated monitors for portal limits, auditor variability, data quality, and standards changes. | MID_LEVEL_DESIGN-0100 | Risk register entities, automated monitoring jobs, and notification hooks implement governed risk tracking across specified categories. |
| HLD-0101 | The platform shall implement WhatsApp with explicit consent capture, template approvals, unsubscribe handling, and compliance with privacy and portal terms. | MID_LEVEL_DESIGN-0101 | Notification and consent design implement explicit WhatsApp consent capture, template scope enforcement, unsubscribe handling, and compliance records. |
| HLD-0102 | The platform shall provide a policy engine enforcing role-based permissions and approval workflows at project and portfolio levels with full audit trails. | MID_LEVEL_DESIGN-0102 | PolicyRule schema, policy evaluation logic, and audit persistence implement role-based permissions and approval workflows at project and portfolio scopes. |
| HLD-0103 | The platform shall ensure recommendations are backed by simulations or calculators within runtime budgets and include quantitative impact estimates. | MID_LEVEL_DESIGN-0103 | Recommendation and simulation designs ensure calculator or simulation backing within runtime budgets and include quantified impacts in outputs. |
| HLD-0104 | The platform shall provide branding configuration to capture AEON logo and color scheme, store branding assets securely, and apply them across all customer-facing documents and exports. | MID_LEVEL_DESIGN-0104 | Branding administration and document rendering securely store and consistently apply AEON logo and color scheme across outputs. |
| HLD-0105 | The platform shall store immutable package history, audit history, permission history, and workflow history as part of project continuity and governance records. | MID_LEVEL_DESIGN-0105 | Packaging, audit, RBAC, and workflow persistence design store immutable continuity records for package, audit, permission, and workflow history. |
| HLD-0106 | The platform shall support branding and template administration without requiring dual approval, while still audit logging such configuration changes. | MID_LEVEL_DESIGN-0106 | Branding and template administration actions remain single-admin operations while generating mandatory audit records. |
| HLD-0107 | The platform shall support role templates including Landscape Consultant, Construction Team, Procurement Team, and Technical Admin in addition to previously defined roles. | MID_LEVEL_DESIGN-0107 | Seeded role template set explicitly includes Landscape Consultant, Construction Team, Procurement Team, and Technical Admin. |
| HLD-0108 | The platform shall initialize all newly created role templates with permissions set to false by default until explicitly granted by Admin, except the default Sustainability Consultant role which retains full access by default subject to RBAC. | MID_LEVEL_DESIGN-0108 | Identity and RBAC rules enforce false-by-default new role initialization and preserve Sustainability Consultant seeded full-access template behavior. |
| HLD-0109 | The platform shall support restricted audit export initiation by either Admin or Technical Admin but require one business Admin approver and one Technical Admin approver before release. | MID_LEVEL_DESIGN-0109 | AuditExportRequest schema, APIs, and state transitions enforce initiator eligibility and dual-role release approval requirement. |
| HLD-0110 | The platform shall prevent audit-log deletion through normal UI workflows and restrict hard deletion to policy-driven or dual-approved administrative execution. | MID_LEVEL_DESIGN-0110 | Security and governance controls explicitly prohibit audit deletion via normal UI and restrict hard deletion to policy-driven or dual-approved admin actions. |
| HLD-0111 | The platform shall support source-specific import metadata capture for manual ACC imports, internal file shares, email artifacts, spreadsheet uploads, CAD or BIM uploads, and simulation result files. | MID_LEVEL_DESIGN-0111 | Artifact schema and ingestion flow capture sourceSystem and sourceReference for all specified source origins. |
| HLD-0112 | The platform shall maintain system-of-record status for internally ingested artifacts even when source files originate from external repositories or manual downloads. | MID_LEVEL_DESIGN-0112 | Artifact schema field systemOfRecord and ingestion controls preserve AEON authoritative status for internally ingested artifacts regardless of origin. |

# API Coverage Matrix
## API Coverage Matrix

| HLD Area | API Coverage |
| --- | --- |
| artifact upload, validation, source-specific metadata, duplicate-safe ingestion, status tracking | `POST /api/v1/artifacts`, `GET /api/v1/artifacts/{id}/status` |
| project score retrieval and dynamic scoring visibility | `GET /api/v1/projects/{id}/scorecard` |
| webhook lifecycle and outbound integration control | `POST /api/v1/webhooks/subscriptions` |
| submission package status visibility | `GET /api/v1/submission-packages/{id}/status` |
| auditor query workflow status visibility | `GET /api/v1/auditor-queries/{id}/status` |
| provider credential administration | `POST /api/v1/admin/provider-credentials` |
| restricted audit export status and released metadata | `GET /api/v1/audit-exports/{id}` |
| AI-assisted pre-assessment initiation and result retrieval | `POST /api/v1/projects/{projectId}/pre-assessments`, `GET /api/v1/projects/{projectId}/pre-assessments/{runId}` |
| credit interpretation execution | `POST /api/v1/projects/{projectId}/interpretations` |
| simulation submission and orchestration intake | `POST /api/v1/projects/{projectId}/simulations` |
| package build for manual portal upload | `POST /api/v1/projects/{projectId}/packages` |
| auditor Q and A thread creation | `POST /api/v1/projects/{projectId}/qa/queries` |
| restricted audit export initiation | `POST /api/v1/admin/audit-export-requests` |
| explicit training permission capture | `POST /api/v1/admin/training-permissions` |
| import template administration | `POST /api/v1/admin/import-templates` |
| future-ready portal placeholder administration | `POST /api/v1/admin/portal-configurations` |
| project transfer governance | `POST /api/v1/projects/{projectId}/transfer-requests` |
| soft-delete restore requests | `POST /api/v1/restores` |

## API Completeness Statement

All public APIs currently defined by the HLD scope and explicitly called out in this MLD are present with method, full path, purpose, request headers, path or query inputs where applicable, request body schema where applicable, success response schema and example, and error coverage for 200 or 201, 400, 401 or 403, 404, and 500.

Any additional implementation APIs that may exist internally between services are intentionally documented under internal service interfaces rather than public API contracts because the HLD requires preservation of public contracts and does not require exposure of private service endpoints.

## Third-Party System Implementation Sections
## Certification Portals

### Purpose
Certification portals are supported only through manual package download and user-upload workflows at MVP. No live portal API integration or RPA execution is enabled.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | manual export and user-operated upload only |
| owning components | Packaging Service, Narrative and Document Generation Service, Admin and Governance Service, Web Client Application |
| configuration source | PortalConfiguration records per portalCode |
| enablement control | portal enabled flag may expose portal-specific packaging templates and guidance only |
| credential handling | no runtime portal credentials stored or used at MVP |
| data exchanged | submission packages, checksum manifests, portal-ready forms, generated narratives |
| user flow | user selects portal placeholder, generates package, downloads archive, uploads manually to official portal outside platform |
| audit scope | package generation, download event, selected portal configuration, user acknowledgement of manual upload |
| future readiness | portalCode, template set mapping, and enablement flags preserved for future official API support |

### Validation Rules
| Rule | Enforcement |
| --- | --- |
| MVP restrictions | mode must remain `manualPlaceholder` |
| no live automation | any attempt to enable API or RPA execution is blocked by policy/feature flags |
| package integrity | package checksum manifest must be generated before download |
| template compatibility | selected package template pins must match configured portal template set where mapped |

### Failure Handling
| Failure | Behavior |
| --- | --- |
| missing portal configuration | default generic manual packaging flow |
| invalid template mapping | block package generation with validation error |
| package build failure | package remains unapproved and retryable through queue workflow |

## Autodesk ACC

### Purpose
Autodesk ACC is supported through manual export/import workflows initiated by users. AEON becomes the system of record after ingestion.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | manual file download from ACC and upload into AEON |
| owning components | Ingestion Service, Web Client Application, Artifact repository, Audit and Explainability Service |
| source classification | `sourceType=import`, `sourceSystem=autodeskAcc` |
| accepted content | drawings, schedules, reports, exported models, document packages |
| metadata captured | source system name, source reference, uploader, original file name, checksum, upload timestamp |
| duplicate control | checksum plus normalized metadata comparison |
| downstream processing | antivirus, content validation, OCR or extraction, classification, search indexing |
| user guidance | import template may define mandatory metadata fields for ACC imports |

### Operational Notes
| Area | Design |
| --- | --- |
| traceability | imported artifact keeps ACC-origin metadata while `systemOfRecord=true` in AEON |
| version handling | latest uploaded file is independent artifact version unless linked by metadata/reference |
| security | no ACC credentials persisted at MVP |

## Rhino / Grasshopper

### Purpose
Rhino and Grasshopper support design-option workflows through file exchange and assisted orchestration where applicable.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | file exchange and assisted local or worker-side orchestration |
| owning components | Simulation Orchestrator Service, Ingestion Service, Windows On-Prem Connector Service, worker services |
| supported inputs | geometry exports, parameter files, result files, schedule exports |
| supported outputs | derived result artifacts, simulation-ready inputs, visual summaries |
| execution path | user uploads source files / worker prepares inputs / external tool run occurs through approved workflow / results re-ingested |
| reproducibility | input fingerprints and seeds logged on SimulationJob when used in downstream simulation flow |
| offline support | on-prem connector can relay files for air-gapped runner environments |

### Controls
| Rule | Enforcement |
| --- | --- |
| tool isolation | worker executes only approved CLI or exchange scripts |
| artifact lineage | output artifacts linked to originating project and simulation or recommendation context |
| timeout | runtime budget enforced by SimulationJob.runtimeBudgetSeconds |

## DesignBuilder

### Purpose
DesignBuilder is used for supported energy/daylight related workflows through API or CLI where applicable.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | API/CLI or file exchange depending installed connector mode |
| owning components | Simulation Orchestrator Service, License Management Service, worker services |
| preflight | validates project, region profile, weather file, tool configuration, license seat availability |
| input set | simulation type, regional profile, EPW weather file, input artifacts, runtime budget |
| execution host | sandboxed Windows VM worker or optional offline runner |
| output ingestion | results uploaded as Artifact records and linked to SimulationJob.outputArtifactIds |
| turnaround rule | result ingestion initiated within 5 minutes of external completion signal |

### Failure and Retry
| Failure | Behavior |
| --- | --- |
| no seat available | fail preflight or queue per admin policy |
| tool invocation error | mark run failed and retry up to maxRetries |
| invalid weather mapping | block job before queue execution |

## eQUEST

### Purpose
eQUEST is supported for approved simulation workflows using file exchange and CLI or assisted operation.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | file exchange and CLI/assisted workflow |
| owning components | Simulation Orchestrator Service, worker services, Windows On-Prem Connector Service |
| input preparation | template-based input packaging and artifact normalization |
| execution control | worker lease, timeout budget, retry policy, result polling |
| output handling | simulation outputs stored as artifacts and exposed for recommendation and score recalculation |
| compliance linkage | outputs may back quantitative recommendation generation and score impacts |

### Specific Constraints
| Constraint | Enforcement |
| --- | --- |
| supported usage | energy and related approved simulation workflows only |
| reproducibility | fingerprint and seed persisted on SimulationJob |
| audit | every run records worker, timestamps, retries, and linked artifacts |

## IESVE

### Purpose
IESVE supports result ingestion and import/export workflows.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | inbound result import and supported bidirectional exchange |
| owning components | Simulation Orchestrator Service, Ingestion Service, Recommendation Service |
| primary usage | import externally run simulation results into AEON for analysis and downstream scoring |
| input metadata | tool version, run timestamp, scenario name, source reference, project mapping |
| validation | imported result package must match expected simulation type and project context |
| downstream usage | score recalculation, recommendation generation, narrative generation, package inclusion |

### Data Handling
| Area | Design |
| --- | --- |
| source tracking | `sourceType=simulation` or `sourceType=import` based on ingestion route |
| result lineage | imported files and parsed summaries linked to simulation context or external run reference |
| failure handling | invalid schema or unmapped metrics route item to manual review |

## OneClick LCA

### Purpose
OneClick LCA supports carbon and lifecycle result ingestion only within MVP boundaries.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | file exchange or API where applicable for inbound result ingestion |
| owning components | Ingestion Service, Recommendation Service, Analytics Service |
| supported scope | embodied carbon/lifecycle result import, derived KPI generation, downloadable summaries |
| mapping | imported metrics mapped to configured carbon KPI definitions and project or credit context |
| validation | unit normalization, scenario tagging, project matching, source metadata capture |
| downstream usage | recommendations, score impacts where configured, analytics dashboards, export summaries |

### Constraints
| Constraint | Enforcement |
| --- | --- |
| MVP limit | no full native lifecycle carbon automation |
| result dependency | quantitative carbon recommendations require imported result or calculator backing |
| audit | imported carbon results retain external source identifiers and upload history |

## Revit

### Purpose
Revit is accepted for import/export reference workflows only at MVP and is not used for headless automation.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | manual upload/export only |
| owning components | Ingestion Service, BIM Extraction Service, Web Client Application |
| accepted status | reference artifact only |
| extraction behavior | no direct RVT extraction pipeline; IFC and exported schedules remain primary extraction inputs |
| metadata | sourceSystem may be `revit`, with model reference metadata captured |
| downstream use | viewing, traceability, package inclusion, manual reference for reviewers |

### Guardrails
| Rule | Enforcement |
| --- | --- |
| no headless automation | worker cannot execute Revit automation jobs |
| no primary extraction | BIM extraction jobs reject RVT as extraction source |
| accepted upload | RVT upload allowed for reference storage only |

## IoT Data Sources

### Purpose
IoT data sources support inbound project data ingestion through import or API-based scheduled/on-demand feeds.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | import/export or API-based ingestion |
| owning components | Ingestion Service, Analytics Service, Project Service, Windows On-Prem Connector Service |
| supported patterns | scheduled file drops, API pulls, approved relay through connector |
| typical data | meter readings, operational hours, occupancy proxies, environmental telemetry |
| validation | timestamp normalization, unit normalization, source authentication, duplicate event suppression |
| storage | raw imported artifacts plus normalized SQL records or KPI aggregates |
| usage | analytics dashboards, anomaly checks, derived KPI support, recommendation context |

### Controls
| Rule | Enforcement |
| --- | --- |
| source approval | only configured approved sources allowed |
| ingestion resilience | partial bad records isolated into exception handling path |
| audit | source identity, pull time, record counts, and normalization results logged |

## SendGrid

### Purpose
SendGrid delivers outbound email notifications.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | HTTPS API or SMTP |
| owning components | Notification Service, Admin and Governance Service |
| credentials | provider credentials stored via DPAPI-protected references |
| dispatch flow | notification rule evaluation / queue dispatch / provider submission / callback or polling update |
| payload content | template identifier, recipient, subject/body variables, correlation id |
| delivery tracking | externalMessageId, sent status, delivered status, failure reason |
| fallback posture | in-app notification preserved even if email provider unavailable |

### Provider Controls
| Control | Implementation |
| --- | --- |
| timeout | explicit outbound timeout and retry with backoff |
| retries | queue-based retry before terminal failure |
| suppression | respects user settings and applicable policy rules |
| audit | request outcome and provider message id retained |

## Meta Cloud API

### Purpose
Meta Cloud API is used only for WhatsApp workflow and operational notifications.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | HTTPS REST |
| owning components | Notification Service, Admin and Governance Service |
| prerequisite | valid WhatsAppConsent with `consentStatus=granted` and allowed template scope |
| payload | approved template id, localized variables, recipient phone, correlation metadata |
| send window | quiet hours suppression from 21:00 to 08:00 IST unless policy-approved exception exists |
| tracking | external message id, delivery state, failure state, unsubscribe handling |
| escalation | if not acknowledged within 2 hours, escalation processed through configured channels |

### Compliance Controls
| Rule | Enforcement |
| --- | --- |
| no authentication usage | notification service blocks auth-related WhatsApp event types |
| consent required | send blocked if consent missing/withdrawn |
| template scope | send blocked for non-approved template categories |
| privacy audit | consent evidence, timestamp, and send history retained |

## EPW / Weather Sources

### Purpose
Weather sources provide EPW or equivalent weather files used for regional mapping and simulation input selection.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | HTTPS download |
| owning components | Region Profile Service, Simulation Orchestrator Service, worker services |
| retrieval trigger | on-demand during profile setup, cache refresh, or simulation preflight when missing |
| caching | retrieved files cached in India-resident storage with metadata and checksum |
| mapping | weatherFileCode and weatherFileUri stored on RegionalProfile |
| validation | checksum, climate-zone compatibility, source availability, file readability |

### Degradation Behavior
| Failure | Behavior |
| --- | --- |
| source unavailable with cached file | use cached approved file |
| source unavailable without cache | fail simulation preflight |
| climate mismatch | anomaly or validation failure depending workflow stage |

## AI Gateway

### Purpose
AI Gateway is an internal platform component and the sole approved path for AI-assisted features. It invokes OpenAI GPT 5.1 as the approved external third-party AI model provider.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | internal HTTPS REST for domain services; outbound HTTPS API from AI Gateway to OpenAI GPT 5.1 |
| owning components | AI Gateway as internal platform component; consuming services include Pre-assessment Service, Credit Interpretation Service, Recommendation Service, Auditor Q and A Service, Standards and CIR Q and A Service, and Audit and Explainability Service |
| request preparation | PII minimization, masking, prompt construction, correlation id, tenant scoping, use-case tagging, and policy evaluation |
| routing control | all AI-assisted requests are routed through AI Gateway; direct service-to-provider calls are blocked by policy and implementation |
| response handling | response validation, rationale extraction, confidence mapping, evidence link preservation, and redacted audit logging |
| persistence | AI interaction logs persist model identifiers, gateway policy version, evidence links, confidence scores, and explainability metadata |
| provider constraint | AI Gateway invokes OpenAI GPT 5.1 only as the approved third-party AI provider |
| privacy control | provider configuration enforces no provider training and no provider retention |
| degradation | when the third-party AI provider is unavailable, eligible services fall back to rules-based or non-AI-assisted flows through gateway-managed degradation behavior |

### Safety and Governance
| Rule | Enforcement |
| --- | --- |
| internal gateway only | domain services must use AI Gateway rather than calling external providers directly |
| provider training | always false |
| provider retention | disallowed |
| audit retention | configurable via tenant policy within AI audit controls |
| duplicate safety | retries must not create destructive side effects |
| tenant isolation | gateway enforces tenant-isolated request handling and logging |
| rate limiting | gateway applies platform AI rate limits per tenant and use case |

## OpenAI GPT 5.1

### Purpose
OpenAI GPT 5.1 is the approved external third-party AI model provider used only behind the AI Gateway.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | outbound HTTPS API from AI Gateway only |
| owning components | AI Gateway |
| call path | internal services → AI Gateway → OpenAI GPT 5.1 |
| allowed usage | approved AI-assisted capabilities after gateway policy checks, masking, and sanitization |
| prohibited usage | no direct domain service calls; no bypass of AI Gateway |
| provider configuration | no provider training and no provider retention enforced through approved configuration |
| logged metadata | provider name, model identifier, model version, gateway policy version, confidence metadata, evidence links, and explainability references persisted through AIInteractionLog |

### Safety and Governance
| Rule | Enforcement |
| --- | --- |
| access restriction | reachable only from AI Gateway-controlled integration path |
| data minimization | only masked and minimized prompts may be forwarded |
| failure handling | provider unavailability triggers gateway-managed degradation behavior |
| auditability | provider usage is logged through gateway-controlled immutable audit records |

## External Webhook Subscribers

### Purpose
External webhook subscribers receive outbound event notifications for selected platform events.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | HTTPS callback with HMAC signature |
| owning components | API Gateway, Notification or event dispatcher workers, WebhookSubscription store |
| subscription model | tenant-scoped subscription with eventTypes, targetUrl, retryPolicy, active flag |
| delivery model | async queued delivery with delivery id and signed payload |
| security | HMAC-SHA256 signature, optional IP allowlisting, DPAPI secret reference, rotating secrets |
| idempotency | delivery id acts as unique event delivery key for subscriber deduplication |

### Delivery Lifecycle
| Step | Behavior |
| --- | --- |
| enqueue | domain event persisted for outbound delivery |
| sign | `timestamp + "." + requestBody` signed using active secret |
| dispatch | POST to subscriber target URL |
| retry | 1m, 5m, 15m, 60m, 6h |
| terminal | dead-letter after max attempts |

## Windows On-Prem Connector Service

### Purpose
Windows On-Prem Connector Service provides secure file and API relay for air-gapped or restricted customer environments.

### Implementation Design
| Area | Design |
| --- | --- |
| integration mode | Windows service with file exchange and API relay |
| owning components | Windows On-Prem Connector Service, API Gateway, Ingestion Service, Simulation Orchestrator Service |
| deployment | customer-managed Windows VM within allowed network zone |
| supported use cases | artifact relay, simulation file exchange, result return, approved API relay |
| authentication | connector-specific credentials and allowlisted endpoint trust |
| transfer model | pull or push based on configured job profiles |
| compatibility | supports current and previous two major versions where applicable |

### Security Controls
| Control | Implementation |
| --- | --- |
| network | outbound-only preferred from customer network to AEON endpoints |
| secrets | local secret protection on Windows host with controlled rotation |
| audit | every transfer logs batch id, file count, checksum, timestamps, and operator or service identity |
| resiliency | resumable transfers and queued retry for transient failures |

## Queue Message Contracts
## Queue Message Contracts

All queue messages are persisted in SQL-backed queue tables and serialized as JSON. Every job message uses the common envelope below and a job-specific payload contract.

### Common Queue Envelope

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| messageId | string | yes | Unique queue message identifier |
| jobType | string | yes | One of the supported job types |
| tenantId | string | yes | Tenant identifier |
| correlationId | string | yes | Distributed correlation identifier |
| causationId | string | no | Upstream event or request id |
| idempotencyKey | string | no | Request-level idempotency key when available |
| priority | string | yes | low/normal/high/critical |
| enqueueAt | string(date-time) | yes | Queue insert time |
| visibleAt | string(date-time) | yes | First eligible processing time |
| attempt | integer | yes | Current attempt count starting at 0 |
| maxAttempts | integer | yes | Maximum allowed attempts |
| leaseTimeoutSeconds | integer | yes | Lease timeout for worker processing |
| payload | object | yes | Job-specific payload |
| headers | object | yes | Additional execution metadata |

### Job Type: artifact.scan

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| artifactId | string | yes | Artifact to scan |
| projectId | string | yes | Owning project |
| storageUri | string | yes | Artifact storage location |
| fileName | string | yes | Original file name |
| mediaType | string | yes | MIME type |
| checksumSha256 | string | yes | Artifact checksum |
| sizeBytes | integer | yes | Artifact size |
| quarantineOnDetect | boolean | yes | Whether infected files move to quarantine |

### Job Type: artifact.ocr

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| artifactId | string | yes | Artifact for OCR |
| projectId | string | yes | Owning project |
| storageUri | string | yes | Source file location |
| language | string | yes | OCR language profile, default `eng` |
| extractionProfile | string | yes | OCR extraction profile identifier |
| createReviewerTask | boolean | yes | Whether review workflow is created |

### Job Type: artifact.extract

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| artifactId | string | yes | Artifact to extract from |
| projectId | string | yes | Owning project |
| extractionModes | array[string] | yes | table/keyValue/bimSpatial or applicable modes |
| sourceTextArtifactId | string | no | Referenced OCR output artifact or extraction result |
| provenanceRequired | boolean | yes | Whether provenance must be persisted |

### Job Type: artifact.classify

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| artifactId | string | yes | Artifact to classify |
| projectId | string | yes | Owning project |
| candidateCreditIds | array[string] | no | Optional narrowed candidate credits |
| method | string | yes | rules/embeddings/hybrid |
| noTrainingUsed | boolean | yes | Must remain true |
| reindexAfterClassification | boolean | yes | Whether search index refresh follows classification |

### Job Type: project.preAssessment

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| runId | string | yes | Pre-assessment run identifier |
| projectId | string | yes | Target project |
| inputArtifactIds | array[string] | yes | Artifacts included in run |
| includeMasterData | boolean | yes | Include project master data flag |
| scenarioName | string | no | Optional scenario label |
| notes | string | no | User notes |
| aiAllowed | boolean | yes | Whether AI path is permitted |

### Job Type: scorecard.recalculate

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| projectId | string | yes | Target project |
| scorecardId | string | no | Existing scorecard id |
| triggerSource | string | yes | artifact/interpretation/simulation/override/manual/anomalyResolution |
| changedEntityType | string | no | Entity type that triggered recalculation |
| changedEntityId | string | no | Entity id that triggered recalculation |
| scenarioId | string | no | What-if scenario id if applicable |
| forceAnomalyChecks | boolean | yes | Whether anomaly rules must execute |

### Job Type: simulation.run

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| simulationJobId | string | yes | Simulation job identifier |
| projectId | string | yes | Target project |
| tool | string | yes | Simulation tool |
| simulationType | string | yes | daylight/glare/energy/other |
| regionalProfileId | string | yes | Applied profile |
| weatherFileCode | string | yes | Weather file code |
| inputArtifactIds | array[string] | yes | Simulation inputs |
| assistedPrepAllowed | boolean | yes | Whether prep assistance may be used |
| runtimeBudgetSeconds | integer | yes | Max runtime budget |
| reproducibilitySeed | string | yes | Deterministic seed |
| inputFingerprint | string | yes | Input fingerprint |
| offlineRunnerAllowed | boolean | yes | Whether offline runner may execute |

### Job Type: simulation.ingest

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| simulationJobId | string | yes | Completed simulation job |
| projectId | string | yes | Target project |
| tool | string | yes | Simulation tool |
| resultLocation | string | yes | Worker or connector result location |
| outputArtifactIds | array[string] | yes | Uploaded result artifacts |
| triggerRecalculation | boolean | yes | Whether score recalculation should follow |
| triggerRecommendations | boolean | yes | Whether recommendations should refresh |

### Job Type: document.generate

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| documentRequestId | string | yes | Generation request id |
| projectId | string | yes | Target project |
| documentType | string | yes | Output document type |
| format | string | yes | pdf/docx/xlsx/json/pptx |
| templateId | string | yes | Template id |
| templateVersion | string | yes | Template version |
| brandingApplied | boolean | yes | Whether branding must be applied |
| watermarkApplied | boolean | yes | Whether watermarking is requested |
| sourceEntityRefs | array[object] | yes | Referenced inputs such as scorecard, package, simulation, or narrative data |

### Job Type: package.build

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| packageId | string | yes | Submission package id |
| projectId | string | yes | Target project |
| packageVersion | string | yes | 1.0/1.1/2.0 |
| artifactIds | array[string] | yes | Included evidence artifacts |
| documentArtifactIds | array[string] | no | Included generated documents |
| templatePins | array[object] | yes | Template pins used for assembly |
| namingConvention | string | yes | Naming pattern enforced |
| portalCode | string | no | Optional portal placeholder code |

### Job Type: notification.dispatch

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| notificationId | string | yes | Notification identifier |
| channel | string | yes | inApp/email/whatsapp |
| recipientUserId | string | no | Recipient user id |
| recipientAddress | string | yes | Email or phone or in-app target |
| templateId | string | yes | Notification template id |
| ruleId | string | no | Triggering rule id |
| eventCode | string | yes | Trigger event code |
| quietHoursEvaluated | boolean | yes | Whether quiet-hours suppression already applied |
| escalationEligibleAt | string(date-time) | no | Time after which unacknowledged escalation may occur |

### Job Type: analytics.refresh

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| refreshId | string | yes | Analytics refresh execution id |
| scopeType | string | yes | tenant/portfolio/project |
| scopeId | string | yes | Scope id |
| periodStart | string(date) | no | Refresh lower bound |
| periodEnd | string(date) | no | Refresh upper bound |
| rebuildSnapshots | boolean | yes | Whether KPI snapshots must be rebuilt |
| triggeredBy | string | yes | scheduler/manual/system |

### Job Type: standards.refresh

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| refreshId | string | yes | Standards refresh id |
| tenantId | string | yes | Tenant owner of corpus |
| corpusItemIds | array[string] | no | Specific corpus items to refresh |
| refreshCadenceDays | integer | yes | Configured cadence |
| validateLicenseExpiry | boolean | yes | Whether license date checks must run |
| reindexCorpus | boolean | yes | Whether search reindex follows refresh |

### Job Type: monitor.riskUpdate

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| monitorRunId | string | yes | Risk monitor run id |
| scopeType | string | yes | tenant/portfolio/project |
| scopeId | string | yes | Scope id |
| ruleCodes | array[string] | yes | Risk monitor rules to evaluate |
| createNotifications | boolean | yes | Whether notifications should be emitted |
| createOrUpdateRisks | boolean | yes | Whether risk register items may be created or updated |

### Job Type: audit.export

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| auditExportRequestId | string | yes | Audit export request id |
| requestedByUserId | string | yes | Initiating user |
| from | string(date-time) | no | Export lower time bound |
| to | string(date-time) | no | Export upper time bound |
| resourceTypes | array[string] | no | Filtered resource types |
| deliveryMethod | string | yes | download/sftp |
| approvalVerified | boolean | yes | Required approvals completed |

### Job Type: training.permission.audit

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| trainingPermissionRecordId | string | yes | Permission record identifier |
| subjectType | string | yes | tenant/project/dataset |
| subjectId | string | yes | Subject id |
| permissionStatus | string | yes | granted/withdrawn/expired |
| approvedByUserId | string | yes | Approver user id |
| effectiveFrom | string(date-time) | yes | Permission effective from |
| effectiveTo | string(date-time) | no | Permission effective to |
| writtenApprovalUri | string | yes | Approval evidence URI |

### Queue Processing Rules

| Rule | Value |
| --- | --- |
| serialization | UTF-8 JSON persisted in SQL queue tables |
| tenant isolation | workers must validate message tenantId against leased execution context |
| idempotency | handlers must treat messageId and business ids as idempotent processing keys |
| retries | retries follow job-type-specific limits; simulation.run max 2, webhook delivery max 5, others service-defined |
| dead-letter | terminal failures are copied to JobDeadLetter with original envelope and last error details |
| observability | processing metrics capture dequeue latency, execution duration, attempts, and terminal outcome by jobType |

## AI Gateway Implementation Design
### Purpose
The AI Gateway is the sole internal platform component permitted to invoke external AI model providers. It centralizes AI request routing, privacy enforcement, tenant isolation, auditability, and degradation behavior while preserving the HLD requirement that OpenAI GPT 5.1 is accessed only through the self-hosted India AI Gateway.

### Implementation Responsibilities

| Area | Design |
| --- | --- |
| invocation path | all AI-assisted requests from domain services are sent to the AI Gateway over internal HTTPS REST; direct domain-service calls to OpenAI GPT 5.1 are blocked by code-level client restrictions and network egress policy |
| deployment location | AI Gateway runs on Windows VMs inside the India-resident private application network |
| approved provider | OpenAI GPT 5.1 only |
| ingress controls | mutual service authentication on private network, propagated `X-Correlation-Id`, `X-Tenant-Id`, and `X-User-Id` headers, and service authorization by allowlisted caller service identity |
| request normalization | prompts are wrapped in a deterministic gateway envelope carrying tenant context, use case, policy version, requested output type, citation expectations, and evidence references |
| privacy controls | PII minimization, field masking, document excerpt reduction, and prompt sanitization are applied before outbound provider calls |
| policy enforcement | request is evaluated against tenant policy, use-case policy, training-permission restrictions, corpus licensing restrictions where applicable, and provider safety rules |
| response controls | schema validation, citation structure validation where required, unsafe-content checks, and bounded response size checks are applied before response is returned to caller |
| explainability | rationale, confidence, evidence links, source references, model identifier, and gateway policy version are persisted to AIInteractionLog |
| retention | AI prompt-response audit retention defaults to 180 days and is tenant-configurable through retention policy controls |
| provider training | always disabled |
| provider retention | always disabled through approved provider configuration |
| degradation | when provider access fails, the gateway returns deterministic degradation status so eligible services can fall back to rules-based or non-AI-assisted output |

### Internal Request Contract

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| requestId | string | yes | Unique AI gateway request identifier |
| tenantId | string | yes | Tenant identifier |
| actorUserId | string | no | Initiating user id when user-triggered |
| callerService | string | yes | Calling internal service name |
| useCase | string | yes | preAssessment/creditInterpretation/recommendation/auditorQA/standardsQA/otherApprovedUseCase |
| modelIdentifier | string | yes | Requested model identifier, must be `openai-gpt-5.1` |
| promptTemplateId | string | yes | Approved prompt template identifier |
| promptTemplateVersion | string | yes | Approved prompt template version |
| systemPrompt | string | yes | Gateway-approved system instruction payload |
| userPrompt | string | yes | Sanitized user or service prompt |
| evidenceLinks | array[string] | yes | Linked evidence or source identifiers |
| datasetReferences | array[string] | yes | Referenced datasets or corpus items |
| citationRequired | boolean | yes | Whether citations are mandatory in output |
| responseFormat | string | yes | text/json/structuredJson |
| maxOutputTokens | integer | yes | Maximum outbound response budget |
| temperature | number | yes | Deterministic generation control configured per use case |
| privacyProfile | string | yes | Applied masking and minimization profile |
| gatewayPolicyVersion | string | yes | Gateway policy version used for evaluation |

### Internal Response Contract

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| requestId | string | yes | Gateway request identifier |
| status | string | yes | completed/degraded/rejected/failed |
| modelIdentifier | string | yes | Returned model identifier |
| modelVersion | string | yes | Returned provider model version |
| outputText | string | no | Human-readable output |
| outputJson | object | no | Structured output when requested |
| confidence | number | no | Gateway or use-case confidence score |
| rationale | string | no | Explainability rationale |
| citations | array[object] | yes | Source citations returned or normalized by gateway |
| safetyFlags | array[string] | yes | Safety or validation flags |
| degradationReason | string | no | Reason for degraded non-AI response path |
| traceId | string | yes | Distributed trace id |
| completedAt | string(date-time) | yes | Completion timestamp |

### Processing Flow

| Step | Description |
| --- | --- |
| 1 | Calling service submits normalized AI request with tenant, user, use-case, and evidence context. |
| 2 | AI Gateway authenticates caller and validates service authorization for the requested use case. |
| 3 | Gateway loads applicable policy bundle including tenant policy, privacy profile, retention settings, and training permission restrictions. |
| 4 | Prompt content is sanitized through masking, excerpting, and prohibited-content checks. |
| 5 | Gateway validates that requested model is `openai-gpt-5.1` and that no alternate provider is referenced. |
| 6 | Outbound call is made to OpenAI GPT 5.1 with provider-retention-disabled configuration and correlation metadata. |
| 7 | Gateway validates returned payload structure, size, citations, and safety conditions. |
| 8 | Gateway persists AIInteractionLog with redacted prompt, redacted response, evidence links, confidence, model metadata, and immutable hash. |
| 9 | Gateway returns validated result to caller or returns degraded status for rules-based fallback handling. |

### Security and Privacy Controls

| Control | Implementation |
| --- | --- |
| tenant isolation | each request carries mandatory tenant context and gateway storage partitions logs by tenantId |
| network isolation | only internal services may reach gateway; only gateway may reach OpenAI endpoint |
| prompt masking | configured masking rules remove or obfuscate direct identifiers, contact details, and unnecessary free text before outbound calls |
| audit restriction | raw prompt/response visibility is restricted to authorized audit and explainability roles only |
| immutable logging | AI interaction logs include immutable hash and append-only retention handling |
| policy deny | unsupported use case, missing tenant context, missing evidence controls where required, or disallowed training context causes request rejection before provider invocation |

### Failure and Degradation Behavior

| Failure Condition | Gateway Behavior | Caller Expectation |
| --- | --- | --- |
| OpenAI provider timeout | mark request `degraded` or `failed` based on use-case policy | eligible caller falls back to rules-based or non-AI-assisted behavior |
| provider unavailable | do not retry destructively beyond configured gateway retry policy | caller shows non-AI-assisted status and may queue retry |
| invalid response schema | reject payload and mark failed with validation code | caller receives standard error or degraded status |
| policy violation | reject before outbound call | caller receives forbidden or validation error depending cause |
| masking failure | block request and audit security event | caller receives failed response and retry is not automatic |

### Operational Design

| Area | Design |
| --- | --- |
| scaling | gateway instances scale horizontally on Windows VMs behind internal load balancing, with stateless request handling and SQL-backed audit persistence |
| timeouts | outbound provider timeout is use-case specific and shorter than caller SLA to preserve graceful degradation |
| retries | safe retry with idempotent requestId for transient outbound failures only |
| observability | metrics include request count by use case, success rate, degradation rate, provider latency, masking failures, validation failures, and token usage |
| logging | structured logs carry tenantId, requestId, callerService, modelIdentifier, useCase, and traceId without storing unredacted sensitive content in general application logs |
| configuration | approved model ids, timeout budgets, privacy profiles, prompt templates, and per-use-case safety rules are admin-controlled configuration artifacts under audit |

### Data Persistence

| Store | Data |
| --- | --- |
| SQL Server | AIInteractionLog metadata, gateway policy version, request status, retention markers, model registry links |
| Object Storage or NAS | optional restricted export bundles for approved audit export workflows only; not general prompt archive storage |
| Audit and Explainability Service | linked explainability views, evidence references, confidence, rationale, and restricted access audit trails |

### Traceability Impact
This section elaborates the existing HLD intent already preserved in the document for AI Gateway routing, privacy controls, approved-provider restriction, explainability persistence, and degradation behavior without changing any public contract, schema, or deployment intent.