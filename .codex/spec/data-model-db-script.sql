SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;
    ------------------------------------------------------------
    -- Schemas
    ------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'core')
        EXEC(N'CREATE SCHEMA [core] AUTHORIZATION [dbo];');
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'project')
        EXEC(N'CREATE SCHEMA [project] AUTHORIZATION [dbo];');
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'content')
        EXEC(N'CREATE SCHEMA [content] AUTHORIZATION [dbo];');
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'integration')
        EXEC(N'CREATE SCHEMA [integration] AUTHORIZATION [dbo];');
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'ops')
        EXEC(N'CREATE SCHEMA [ops] AUTHORIZATION [dbo];');

    ------------------------------------------------------------
    -- Tables without circular dependencies first
    ------------------------------------------------------------

    CREATE TABLE [core].[Tenant] (
        [TenantId] uniqueidentifier NOT NULL,
        [TenantCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DeploymentMode] nvarchar(30) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DataResidencyCountryCode] char(2) NOT NULL,
        [DefaultLocale] nvarchar(10) NOT NULL,
        [DefaultUnitSystem] nvarchar(10) NOT NULL,
        [DefaultTimezone] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Tenant] PRIMARY KEY CLUSTERED ([TenantId]),
        CONSTRAINT [UQ_Tenant_TenantCode] UNIQUE ([TenantCode]),
        CONSTRAINT [CK_Tenant_DeploymentMode] CHECK ([DeploymentMode] IN ('saasSingleTenant','saasMultiTenant','onPrem')),
        CONSTRAINT [CK_Tenant_Status] CHECK ([Status] IN ('active','disabled','provisioning','deleted')),
        CONSTRAINT [CK_Tenant_DefaultUnitSystem] CHECK ([DefaultUnitSystem] IN ('SI','Imperial')),
        CONSTRAINT [CK_Tenant_DataResidency] CHECK ([DataResidencyCountryCode] = 'IN')
    );

    CREATE TABLE [core].[Permission] (
        [PermissionId] uniqueidentifier NOT NULL,
        [PermissionKey] nvarchar(150) NOT NULL,
        [ResourceType] nvarchar(50) NOT NULL,
        [ActionType] nvarchar(20) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_Permission] PRIMARY KEY CLUSTERED ([PermissionId]),
        CONSTRAINT [UQ_Permission_Key] UNIQUE ([PermissionKey]),
        CONSTRAINT [CK_Permission_ActionType] CHECK ([ActionType] IN ('view','create','edit','delete','approve','send','manage','export'))
    );

    CREATE TABLE [integration].[WeatherFileCache] (
        [WeatherFileCacheId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [WeatherFileCode] nvarchar(100) NOT NULL,
        [SourceUri] nvarchar(1000) NOT NULL,
        [CachedStorageUri] nvarchar(1000) NOT NULL,
        [DatasetPublisher] nvarchar(200) NULL,
        [DatasetVersion] nvarchar(100) NULL,
        [StandardsReferencesJson] nvarchar(max) NULL,
        [CodeReferencesJson] nvarchar(max) NULL,
        [ClimateZoneCode] nvarchar(30) NULL,
        [Timezone] nvarchar(64) NULL,
        [UnitSystem] nvarchar(10) NULL,
        [ChecksumSha256] char(64) NULL,
        [ApprovalStatus] nvarchar(20) NOT NULL,
        [ApprovedAt] datetime2(3) NULL,
        [SourceRetrievedAt] datetime2(3) NULL,
        [LastValidatedAt] datetime2(3) NULL,
        [IsLatestApprovedDataset] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WeatherFileCache] PRIMARY KEY CLUSTERED ([WeatherFileCacheId]),
        CONSTRAINT [UQ_WeatherFileCache_Tenant_WeatherFileCode] UNIQUE ([TenantId],[WeatherFileCode]),
        CONSTRAINT [UQ_WeatherFileCache_WeatherFileCode] UNIQUE ([WeatherFileCode]),
        CONSTRAINT [CK_WeatherFileCache_ApprovalStatus] CHECK ([ApprovalStatus] IN ('pending','approved','rejected','retired')),
        CONSTRAINT [CK_WeatherFileCache_StandardsReferencesJson] CHECK ([StandardsReferencesJson] IS NULL OR ISJSON([StandardsReferencesJson]) = 1),
        CONSTRAINT [CK_WeatherFileCache_CodeReferencesJson] CHECK ([CodeReferencesJson] IS NULL OR ISJSON([CodeReferencesJson]) = 1),
        CONSTRAINT [CK_WeatherFileCache_UnitSystem] CHECK ([UnitSystem] IS NULL OR [UnitSystem] IN ('SI','Imperial')),
        CONSTRAINT [CK_WeatherFileCache_ClimateZoneCode] CHECK ([ClimateZoneCode] IS NULL OR [ClimateZoneCode] IN ('IN-Composite','IN-HotDry','IN-WarmHumid','IN-Temperate','IN-Cold')),
        CONSTRAINT [CK_WeatherFileCache_ChecksumHex] CHECK ([ChecksumSha256] IS NULL OR [ChecksumSha256] NOT LIKE '%[^0-9A-Fa-f]%')
    );

    CREATE TABLE [core].[Organization] (
        [OrganizationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [OrganizationCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [OrganizationType] nvarchar(30) NOT NULL,
        [ParentOrganizationId] uniqueidentifier NULL,
        [CountryCode] char(2) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Organization_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Organization] PRIMARY KEY CLUSTERED ([OrganizationId]),
        CONSTRAINT [UQ_Organization_Tenant_Code] UNIQUE ([TenantId],[OrganizationCode]),
        CONSTRAINT [CK_Organization_Type] CHECK ([OrganizationType] IN ('consultancy','owner','architect','mep','landscape','construction','procurement','pmc','auditor','internal')),
        CONSTRAINT [FK_Organization_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Organization_Parent] FOREIGN KEY ([ParentOrganizationId]) REFERENCES [core].[Organization]([OrganizationId])
    );

    CREATE TABLE [core].[Portfolio] (
        [PortfolioId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [PortfolioCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Portfolio_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Portfolio] PRIMARY KEY CLUSTERED ([PortfolioId]),
        CONSTRAINT [UQ_Portfolio_Tenant_Code] UNIQUE ([TenantId],[PortfolioCode]),
        CONSTRAINT [FK_Portfolio_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Portfolio_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organization]([OrganizationId])
    );

    CREATE TABLE [core].[UserAccount] (
        [UserId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [Email] nvarchar(320) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PasswordPolicyVersion] nvarchar(20) NOT NULL,
        [PasswordHash] varbinary(512) NULL,
        [MfaEnabled] bit NOT NULL,
        [LastLoginAt] datetime2(3) NULL,
        [Locale] nvarchar(10) NOT NULL,
        [UnitSystem] nvarchar(10) NOT NULL,
        [Timezone] nvarchar(64) NOT NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_UserAccount] PRIMARY KEY CLUSTERED ([UserId]),
        CONSTRAINT [UQ_UserAccount_Tenant_Email] UNIQUE ([TenantId],[Email]),
        CONSTRAINT [CK_UserAccount_Status] CHECK ([Status] IN ('active','disabled','invited','locked')),
        CONSTRAINT [CK_UserAccount_UnitSystem] CHECK ([UnitSystem] IN ('SI','Imperial')),
        CONSTRAINT [FK_UserAccount_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_UserAccount_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organization]([OrganizationId])
    );

    CREATE TABLE [core].[RoleTemplate] (
        [RoleId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [SystemRole] bit NOT NULL,
        [DefaultPermissionState] nvarchar(30) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_RoleTemplate_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RoleTemplate] PRIMARY KEY CLUSTERED ([RoleId]),
        CONSTRAINT [UQ_RoleTemplate_Tenant_Name] UNIQUE ([TenantId],[Name]),
        CONSTRAINT [CK_RoleTemplate_DefaultPermissionState] CHECK ([DefaultPermissionState] IN ('falseByDefault','predefinedTemplate')),
        CONSTRAINT [FK_RoleTemplate_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [core].[RolePermission] (
        [RolePermissionId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [Granted] bit NOT NULL,
        [GrantedByUserId] uniqueidentifier NULL,
        [GrantedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_RolePermission] PRIMARY KEY CLUSTERED ([RolePermissionId]),
        CONSTRAINT [UQ_RolePermission_Role_Permission] UNIQUE ([RoleId],[PermissionId]),
        CONSTRAINT [FK_RolePermission_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId]),
        CONSTRAINT [FK_RolePermission_Permission] FOREIGN KEY ([PermissionId]) REFERENCES [core].[Permission]([PermissionId]),
        CONSTRAINT [FK_RolePermission_GrantedBy] FOREIGN KEY ([GrantedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[UserRole] (
        [UserRoleId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2(3) NOT NULL,
        [AssignedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED ([UserRoleId]),
        CONSTRAINT [UQ_UserRole_User_Role] UNIQUE ([UserId],[RoleId]),
        CONSTRAINT [FK_UserRole_User] FOREIGN KEY ([UserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_UserRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId]),
        CONSTRAINT [FK_UserRole_AssignedBy] FOREIGN KEY ([AssignedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[ScopeAssignment] (
        [ScopeAssignmentId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2(3) NOT NULL,
        [AssignedByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_ScopeAssignment] PRIMARY KEY CLUSTERED ([ScopeAssignmentId]),
        CONSTRAINT [UQ_ScopeAssignment] UNIQUE ([TenantId],[UserId],[RoleId],[ScopeType],[ScopeId]),
        CONSTRAINT [CK_ScopeAssignment_ScopeType] CHECK ([ScopeType] IN ('org','portfolio','project','credit')),
        CONSTRAINT [FK_ScopeAssignment_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ScopeAssignment_User] FOREIGN KEY ([UserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_ScopeAssignment_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId]),
        CONSTRAINT [FK_ScopeAssignment_AssignedBy] FOREIGN KEY ([AssignedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[BusinessCalendar] (
        [BusinessCalendarId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NULL,
        [Timezone] nvarchar(64) NOT NULL,
        [WorkingDaysMask] tinyint NOT NULL,
        [Active] bit NOT NULL CONSTRAINT [DF_BusinessCalendar_Active] DEFAULT ((1)),
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_BusinessCalendar] PRIMARY KEY CLUSTERED ([BusinessCalendarId]),
        CONSTRAINT [UQ_BusinessCalendar_Tenant_Scope] UNIQUE ([TenantId],[ScopeType],[ScopeId]),
        CONSTRAINT [CK_BusinessCalendar_WorkingDaysMask] CHECK ([WorkingDaysMask] BETWEEN 1 AND 127),
        CONSTRAINT [CK_BusinessCalendar_ScopeType] CHECK ([ScopeType] IN ('tenant','project')),
        CONSTRAINT [CK_BusinessCalendar_ScopeId] CHECK (([ScopeType] = 'tenant' AND [ScopeId] IS NULL) OR ([ScopeType] = 'project' AND [ScopeId] IS NOT NULL)),
        CONSTRAINT [FK_BusinessCalendar_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [core].[BusinessHoliday] (
        [BusinessHolidayId] uniqueidentifier NOT NULL,
        [BusinessCalendarId] uniqueidentifier NOT NULL,
        [HolidayDate] date NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_BusinessHoliday] PRIMARY KEY CLUSTERED ([BusinessHolidayId]),
        CONSTRAINT [UQ_BusinessHoliday_Calendar_Date] UNIQUE ([BusinessCalendarId],[HolidayDate]),
        CONSTRAINT [FK_BusinessHoliday_Calendar] FOREIGN KEY ([BusinessCalendarId]) REFERENCES [core].[BusinessCalendar]([BusinessCalendarId])
    );

    CREATE TABLE [core].[NotificationSettings] (
        [NotificationSettingsId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [EmailEnabled] bit NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [WhatsAppEnabled] bit NOT NULL,
        [QuietHoursEnabled] bit NOT NULL,
        [QuietHoursStartLocal] char(5) NOT NULL,
        [QuietHoursEndLocal] char(5) NOT NULL,
        [Timezone] nvarchar(64) NOT NULL,
        [DefaultEscalationMinutes] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_NotificationSettings] PRIMARY KEY CLUSTERED ([NotificationSettingsId]),
        CONSTRAINT [UQ_NotificationSettings_Tenant_User] UNIQUE ([TenantId],[UserId]),
        CONSTRAINT [CK_NotificationSettings_Escalation] CHECK ([DefaultEscalationMinutes] >= 0),
        CONSTRAINT [FK_NotificationSettings_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_NotificationSettings_User] FOREIGN KEY ([UserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[NotificationRule] (
        [NotificationRuleId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [EventCode] nvarchar(100) NULL,
        [ConditionExpression] nvarchar(max) NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [EscalationMinutes] int NOT NULL,
        [Active] bit NOT NULL,
        [QuietHoursRespect] bit NOT NULL,
        [QuietHoursStartLocal] char(5) NULL,
        [QuietHoursEndLocal] char(5) NULL,
        [QuietHoursTimezone] nvarchar(64) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_NotificationRule] PRIMARY KEY CLUSTERED ([NotificationRuleId]),
        CONSTRAINT [UQ_NotificationRule_Tenant_Name] UNIQUE ([TenantId],[Name]),
        CONSTRAINT [CK_NotificationRule_Escalation] CHECK ([EscalationMinutes] >= 0),
        CONSTRAINT [FK_NotificationRule_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [core].[RegionalProfile] (
        [RegionalProfileId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [RegionCode] nvarchar(10) NOT NULL,
        [ClimateZoneCode] nvarchar(30) NULL,
        [WeatherFileCode] nvarchar(100) NOT NULL,
        [WeatherFileUri] nvarchar(1000) NOT NULL,
        [Timezone] nvarchar(64) NOT NULL,
        [UnitSystem] nvarchar(10) NOT NULL,
        [ParametersJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RegionalProfile] PRIMARY KEY CLUSTERED ([RegionalProfileId]),
        CONSTRAINT [UQ_RegionalProfile_Tenant_Code] UNIQUE ([TenantId],[Code]),
        CONSTRAINT [CK_RegionalProfile_Status] CHECK ([Status] IN ('draft','published','retired')),
        CONSTRAINT [CK_RegionalProfile_RegionCode] CHECK ([RegionCode] IN ('IN','MV','QA','ME','NP')),
        CONSTRAINT [CK_RegionalProfile_ClimateZone] CHECK ([ClimateZoneCode] IS NULL OR [ClimateZoneCode] IN ('IN-Composite','IN-HotDry','IN-WarmHumid','IN-Temperate','IN-Cold')),
        CONSTRAINT [CK_RegionalProfile_UnitSystem] CHECK ([UnitSystem] IN ('SI','Imperial')),
        CONSTRAINT [CK_RegionalProfile_ParametersJson] CHECK (ISJSON([ParametersJson]) = 1),
        CONSTRAINT [FK_RegionalProfile_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_RegionalProfile_WeatherFileCache] FOREIGN KEY ([WeatherFileCode]) REFERENCES [integration].[WeatherFileCache]([WeatherFileCode])
    );

    CREATE TABLE [core].[RegionalProfileStandardReference] (
        [RegionalProfileStandardReferenceId] uniqueidentifier NOT NULL,
        [RegionalProfileId] uniqueidentifier NOT NULL,
        [ReferenceText] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_RegionalProfileStandardReference] PRIMARY KEY CLUSTERED ([RegionalProfileStandardReferenceId]),
        CONSTRAINT [FK_RegionalProfileStandardReference_Profile] FOREIGN KEY ([RegionalProfileId]) REFERENCES [core].[RegionalProfile]([RegionalProfileId])
    );

    CREATE TABLE [core].[RegionalProfileCodeReference] (
        [RegionalProfileCodeReferenceId] uniqueidentifier NOT NULL,
        [RegionalProfileId] uniqueidentifier NOT NULL,
        [ReferenceText] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_RegionalProfileCodeReference] PRIMARY KEY CLUSTERED ([RegionalProfileCodeReferenceId]),
        CONSTRAINT [FK_RegionalProfileCodeReference_Profile] FOREIGN KEY ([RegionalProfileId]) REFERENCES [core].[RegionalProfile]([RegionalProfileId])
    );

    CREATE TABLE [core].[RatingLibrary] (
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RatingSystemCode] nvarchar(20) NOT NULL,
        [RatingVariant] nvarchar(100) NOT NULL,
        [VersionLabel] nvarchar(30) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [EffectiveDate] date NOT NULL,
        [AuthorUserId] uniqueidentifier NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [PriorVersionId] uniqueidentifier NULL,
        [TaxonomyJson] nvarchar(max) NOT NULL,
        [ChangeSummary] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RatingLibrary] PRIMARY KEY CLUSTERED ([RatingLibraryId]),
        CONSTRAINT [UQ_RatingLibrary_Tenant_System_Variant_Version] UNIQUE ([TenantId],[RatingSystemCode],[RatingVariant],[VersionLabel]),
        CONSTRAINT [CK_RatingLibrary_Status] CHECK ([Status] IN ('draft','published','retired')),
        CONSTRAINT [CK_RatingLibrary_System] CHECK ([RatingSystemCode] IN ('LEED','IGBC','GRIHA','WELL','EDGE')),
        CONSTRAINT [CK_RatingLibrary_TaxonomyJson] CHECK (ISJSON([TaxonomyJson]) = 1),
        CONSTRAINT [FK_RatingLibrary_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_RatingLibrary_Author] FOREIGN KEY ([AuthorUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_RatingLibrary_PriorVersion] FOREIGN KEY ([PriorVersionId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId])
    );

    CREATE TABLE [core].[RatingCredit] (
        [CreditId] uniqueidentifier NOT NULL,
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [CreditCode] nvarchar(100) NOT NULL,
        [CreditName] nvarchar(300) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [IsPrerequisite] bit NOT NULL,
        [MaxPoints] decimal(9,2) NOT NULL,
        [SequenceNo] int NOT NULL,
        [MetadataJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_RatingCredit] PRIMARY KEY CLUSTERED ([CreditId]),
        CONSTRAINT [UQ_RatingCredit_Library_Code] UNIQUE ([RatingLibraryId],[CreditCode]),
        CONSTRAINT [CK_RatingCredit_MaxPoints] CHECK ([MaxPoints] >= 0),
        CONSTRAINT [CK_RatingCredit_SequenceNo] CHECK ([SequenceNo] > 0),
        CONSTRAINT [CK_RatingCredit_MetadataJson] CHECK (ISJSON([MetadataJson]) = 1),
        CONSTRAINT [FK_RatingCredit_RatingLibrary] FOREIGN KEY ([RatingLibraryId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId])
    );

    CREATE TABLE [core].[CreditDependency] (
        [CreditDependencyId] uniqueidentifier NOT NULL,
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        [DependsOnCreditId] uniqueidentifier NOT NULL,
        [DependencyType] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_CreditDependency] PRIMARY KEY CLUSTERED ([CreditDependencyId]),
        CONSTRAINT [UQ_CreditDependency] UNIQUE ([CreditId],[DependsOnCreditId],[DependencyType]),
        CONSTRAINT [CK_CreditDependency_Type] CHECK ([DependencyType] IN ('prerequisite','pointsDependency','evidenceDependency','conditional')),
        CONSTRAINT [CK_CreditDependency_NotSelf] CHECK ([CreditId] <> [DependsOnCreditId]),
        CONSTRAINT [FK_CreditDependency_Library] FOREIGN KEY ([RatingLibraryId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId]),
        CONSTRAINT [FK_CreditDependency_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_CreditDependency_DependsOn] FOREIGN KEY ([DependsOnCreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [core].[Addendum] (
        [AddendumId] uniqueidentifier NOT NULL,
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [EffectiveDate] date NOT NULL,
        [SourceUri] nvarchar(1000) NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [ChangesJson] nvarchar(max) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_Addendum] PRIMARY KEY CLUSTERED ([AddendumId]),
        CONSTRAINT [UQ_Addendum_Library_Code] UNIQUE ([RatingLibraryId],[Code]),
        CONSTRAINT [CK_Addendum_ChangesJson] CHECK (ISJSON([ChangesJson]) = 1),
        CONSTRAINT [FK_Addendum_RatingLibrary] FOREIGN KEY ([RatingLibraryId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId]),
        CONSTRAINT [FK_Addendum_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[AddendaSLAConfig] (
        [AddendaSLAConfigId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TargetBusinessDays] int NOT NULL,
        [AlertThresholdDays] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AddendaSLAConfig] PRIMARY KEY CLUSTERED ([AddendaSLAConfigId]),
        CONSTRAINT [UQ_AddendaSLAConfig_Tenant] UNIQUE ([TenantId]),
        CONSTRAINT [CK_AddendaSLAConfig_TargetBusinessDays] CHECK ([TargetBusinessDays] > 0),
        CONSTRAINT [CK_AddendaSLAConfig_AlertThresholdDays] CHECK ([AlertThresholdDays] > 0 AND [AlertThresholdDays] <= [TargetBusinessDays]),
        CONSTRAINT [FK_AddendaSLAConfig_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [core].[AddendaSLAStatus] (
        [AddendaSLAStatusId] uniqueidentifier NOT NULL,
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [AddendumId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DueBy] date NOT NULL,
        [EvaluatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_AddendaSLAStatus] PRIMARY KEY CLUSTERED ([AddendaSLAStatusId]),
        CONSTRAINT [UQ_AddendaSLAStatus_Addendum] UNIQUE ([AddendumId]),
        CONSTRAINT [CK_AddendaSLAStatus_Status] CHECK ([Status] IN ('onTrack','atRisk','breached')),
        CONSTRAINT [FK_AddendaSLAStatus_RatingLibrary] FOREIGN KEY ([RatingLibraryId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId]),
        CONSTRAINT [FK_AddendaSLAStatus_Addendum] FOREIGN KEY ([AddendumId]) REFERENCES [core].[Addendum]([AddendumId])
    );

    CREATE TABLE [core].[PolicyRule] (
        [PolicyRuleId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Scope] nvarchar(20) NOT NULL,
        [ConditionsJson] nvarchar(max) NOT NULL,
        [ActionsJson] nvarchar(max) NOT NULL,
        [Effect] nvarchar(20) NOT NULL,
        [Priority] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [AuditFieldsJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PolicyRule] PRIMARY KEY CLUSTERED ([PolicyRuleId]),
        CONSTRAINT [UQ_PolicyRule_Tenant_Name] UNIQUE ([TenantId],[Name]),
        CONSTRAINT [CK_PolicyRule_Scope] CHECK ([Scope] IN ('org','portfolio','project','credit')),
        CONSTRAINT [CK_PolicyRule_Effect] CHECK ([Effect] IN ('allow','deny','requireApproval')),
        CONSTRAINT [CK_PolicyRule_Status] CHECK ([Status] IN ('enabled','disabled')),
        CONSTRAINT [CK_PolicyRule_ConditionsJson] CHECK (ISJSON([ConditionsJson]) = 1),
        CONSTRAINT [CK_PolicyRule_ActionsJson] CHECK (ISJSON([ActionsJson]) = 1),
        CONSTRAINT [CK_PolicyRule_AuditFieldsJson] CHECK (ISJSON([AuditFieldsJson]) = 1),
        CONSTRAINT [FK_PolicyRule_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [content].[Template] (
        [TemplateId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DocumentType] nvarchar(30) NOT NULL,
        [CurrentVersion] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Template] PRIMARY KEY CLUSTERED ([TemplateId]),
        CONSTRAINT [UQ_Template_Tenant_Name] UNIQUE ([TenantId],[Name]),
        CONSTRAINT [CK_Template_DocumentType] CHECK ([DocumentType] IN ('narrative','calculator','simulationSummary','formReadyData','scorecard','checklist','report','package','portfolioExport')),
        CONSTRAINT [FK_Template_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [content].[BrandingAsset] (
        [BrandingAssetId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [LogoStorageUri] nvarchar(1000) NULL,
        [FontsStorageUri] nvarchar(1000) NULL,
        [ColorsJson] nvarchar(max) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_BrandingAsset] PRIMARY KEY CLUSTERED ([BrandingAssetId]),
        CONSTRAINT [UQ_BrandingAsset_Tenant] UNIQUE ([TenantId]),
        CONSTRAINT [CK_BrandingAsset_Status] CHECK ([Status] IN ('updated','active','archived')),
        CONSTRAINT [CK_BrandingAsset_ColorsJson] CHECK ([ColorsJson] IS NULL OR ISJSON([ColorsJson]) = 1),
        CONSTRAINT [FK_BrandingAsset_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [content].[CannedResponse] (
        [CannedResponseId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Active] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CannedResponse] PRIMARY KEY CLUSTERED ([CannedResponseId]),
        CONSTRAINT [UQ_CannedResponse_Tenant_Title] UNIQUE ([TenantId],[Title]),
        CONSTRAINT [FK_CannedResponse_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [content].[StandardCorpusItem] (
        [StandardCorpusItemId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [ContentVersionIdentifier] nvarchar(100) NOT NULL,
        [SourceType] nvarchar(20) NOT NULL,
        [LicenseScope] nvarchar(200) NOT NULL,
        [LicenseStartDate] date NOT NULL,
        [LicenseEndDate] date NULL,
        [CitationStyle] nvarchar(20) NOT NULL,
        [RefreshCadenceDays] int NOT NULL,
        [ScrapingProtected] bit NOT NULL,
        [StorageUri] nvarchar(1000) NULL,
        [UploadStatus] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_StandardCorpusItem] PRIMARY KEY CLUSTERED ([StandardCorpusItemId]),
        CONSTRAINT [UQ_StandardCorpusItem_Tenant_Title_Version] UNIQUE ([TenantId],[Title],[ContentVersionIdentifier]),
        CONSTRAINT [CK_StandardCorpusItem_SourceType] CHECK ([SourceType] IN ('licensed','permittedPublic','internal')),
        CONSTRAINT [CK_StandardCorpusItem_CitationStyle] CHECK ([CitationStyle] IN ('aeonDefault','apa','ieee','chicago')),
        CONSTRAINT [CK_StandardCorpusItem_RefreshCadenceDays] CHECK ([RefreshCadenceDays] > 0),
        CONSTRAINT [CK_StandardCorpusItem_LicenseDates] CHECK ([LicenseEndDate] IS NULL OR [LicenseStartDate] <= [LicenseEndDate]),
        CONSTRAINT [CK_StandardCorpusItem_UploadStatus] CHECK ([UploadStatus] IN ('pending','uploaded','processing','ready','failed')),
        CONSTRAINT [FK_StandardCorpusItem_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [integration].[MappingDefinition] (
        [MappingDefinitionId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ToolCode] nvarchar(30) NOT NULL,
        [ToolSchemaVersion] nvarchar(30) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MappingDefinition] PRIMARY KEY CLUSTERED ([MappingDefinitionId]),
        CONSTRAINT [CK_MappingDefinition_ToolCode] CHECK ([ToolCode] IN ('oneclicklca','revit','designbuilder','equest','iesve','pyrosim','iot')),
        CONSTRAINT [FK_MappingDefinition_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [integration].[WebhookSubscription] (
        [WebhookSubscriptionId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [TargetUrl] nvarchar(1000) NOT NULL,
        [SecretRef] nvarchar(500) NOT NULL,
        [Active] bit NOT NULL,
        [IpAllowlistEnabled] bit NOT NULL,
        [RetryPolicyJson] nvarchar(max) NOT NULL,
        [SecretRotationEnabled] bit NOT NULL CONSTRAINT [DF_WebhookSubscription_SecretRotationEnabled] DEFAULT ((1)),
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WebhookSubscription] PRIMARY KEY CLUSTERED ([WebhookSubscriptionId]),
        CONSTRAINT [CK_WebhookSubscription_RetryPolicyJson] CHECK (ISJSON([RetryPolicyJson]) = 1),
        CONSTRAINT [CK_WebhookSubscription_SecretRotationEnabled] CHECK ([SecretRotationEnabled] IN (0,1)),
        CONSTRAINT [FK_WebhookSubscription_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [integration].[ProviderCredential] (
        [ProviderCredentialId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProviderCode] nvarchar(50) NOT NULL,
        [CredentialName] nvarchar(200) NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NULL,
        [SecretRef] nvarchar(500) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RotationPolicyJson] nvarchar(max) NOT NULL,
        [ProviderMetadataJson] nvarchar(max) NULL,
        [LastRotatedAt] datetime2(3) NULL,
        [NextRotationDueAt] datetime2(3) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ProviderCredential] PRIMARY KEY CLUSTERED ([ProviderCredentialId]),
        CONSTRAINT [UQ_ProviderCredential_Tenant_Provider_Scope_Name] UNIQUE ([TenantId],[ProviderCode],[ScopeType],[ScopeId],[CredentialName]),
        CONSTRAINT [CK_ProviderCredential_ScopeType] CHECK ([ScopeType] IN ('tenant','project')),
        CONSTRAINT [CK_ProviderCredential_Status] CHECK ([Status] IN ('active','pendingRotation','rotated','retired','disabled')),
        CONSTRAINT [CK_ProviderCredential_RotationPolicyJson] CHECK (ISJSON([RotationPolicyJson]) = 1),
        CONSTRAINT [CK_ProviderCredential_ProviderMetadataJson] CHECK ([ProviderMetadataJson] IS NULL OR ISJSON([ProviderMetadataJson]) = 1),
        CONSTRAINT [FK_ProviderCredential_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ProviderCredential_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [integration].[PortalConfiguration] (
        [PortalConfigurationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [PortalCode] nvarchar(50) NOT NULL,
        [Enabled] bit NOT NULL,
        [Mode] nvarchar(30) NOT NULL,
        [PackagingTemplateSetId] uniqueidentifier NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PortalConfiguration] PRIMARY KEY CLUSTERED ([PortalConfigurationId]),
        CONSTRAINT [UQ_PortalConfiguration_Tenant_PortalCode] UNIQUE ([TenantId],[PortalCode]),
        CONSTRAINT [CK_PortalConfiguration_Mode] CHECK ([Mode] IN ('manualPlaceholder')),
        CONSTRAINT [FK_PortalConfiguration_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [integration].[LicenseSeatAssignment] (
        [LicenseSeatAssignmentId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ToolCode] nvarchar(30) NOT NULL,
        [SeatIdentifier] nvarchar(100) NOT NULL,
        [AssignedToUserId] uniqueidentifier NULL,
        [ModeFlag] nvarchar(20) NOT NULL,
        [CheckoutStatus] nvarchar(20) NOT NULL,
        [CheckedOutAt] datetime2(3) NULL,
        [ReturnedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_LicenseSeatAssignment] PRIMARY KEY CLUSTERED ([LicenseSeatAssignmentId]),
        CONSTRAINT [UQ_LicenseSeatAssignment_Tenant_Tool_Seat] UNIQUE ([TenantId],[ToolCode],[SeatIdentifier]),
        CONSTRAINT [CK_LicenseSeatAssignment_ToolCode] CHECK ([ToolCode] IN ('rhino','grasshopper','designbuilder','equest','iesve','pyrosim','oneclicklca','revit')),
        CONSTRAINT [CK_LicenseSeatAssignment_ModeFlag] CHECK ([ModeFlag] IN ('named','concurrent','offlineRunner')),
        CONSTRAINT [CK_LicenseSeatAssignment_CheckoutStatus] CHECK ([CheckoutStatus] IN ('available','checkedOut','reserved','invalid')),
        CONSTRAINT [CK_LicenseSeatAssignment_ReturnedAt] CHECK ([CheckedOutAt] IS NULL OR [ReturnedAt] IS NULL OR [CheckedOutAt] <= [ReturnedAt]),
        CONSTRAINT [FK_LicenseSeatAssignment_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_LicenseSeatAssignment_AssignedToUser] FOREIGN KEY ([AssignedToUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [integration].[OnPremConnectorRegistration] (
        [OnPremConnectorRegistrationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ConnectorCode] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DeploymentVersion] nvarchar(50) NULL,
        [OutboundOnlyMode] bit NOT NULL,
        [LastHeartbeatAt] datetime2(3) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_OnPremConnectorRegistration] PRIMARY KEY CLUSTERED ([OnPremConnectorRegistrationId]),
        CONSTRAINT [UQ_OnPremConnectorRegistration_Tenant_ConnectorCode] UNIQUE ([TenantId],[ConnectorCode]),
        CONSTRAINT [CK_OnPremConnectorRegistration_Status] CHECK ([Status] IN ('registered','active','inactive','disabled','retired')),
        CONSTRAINT [CK_OnPremConnectorRegistration_MetadataJson] CHECK ([MetadataJson] IS NULL OR ISJSON([MetadataJson]) = 1),
        CONSTRAINT [FK_OnPremConnectorRegistration_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[TrainingPermissionRecord] (
        [TrainingPermissionRecordId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [SubjectType] nvarchar(20) NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [PermissionStatus] nvarchar(20) NOT NULL,
        [WrittenApprovalUri] nvarchar(1000) NOT NULL,
        [ApprovedByUserId] uniqueidentifier NOT NULL,
        [EffectiveFrom] datetime2(3) NOT NULL,
        [EffectiveTo] datetime2(3) NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_TrainingPermissionRecord] PRIMARY KEY CLUSTERED ([TrainingPermissionRecordId]),
        CONSTRAINT [CK_TrainingPermissionRecord_SubjectType] CHECK ([SubjectType] IN ('tenant','project','dataset')),
        CONSTRAINT [CK_TrainingPermissionRecord_PermissionStatus] CHECK ([PermissionStatus] IN ('granted','withdrawn','expired')),
        CONSTRAINT [CK_TrainingPermissionRecord_Dates] CHECK ([EffectiveTo] IS NULL OR [EffectiveFrom] <= [EffectiveTo]),
        CONSTRAINT [FK_TrainingPermissionRecord_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_TrainingPermissionRecord_ApprovedBy] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[AuditLog] (
        [AuditLogId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorType] nvarchar(20) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [ResourceType] nvarchar(50) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(500) NULL,
        [CorrelationId] nvarchar(100) NOT NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [Reason] nvarchar(2000) NULL,
        [ImmutableHash] char(64) NOT NULL,
        CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([AuditLogId]),
        CONSTRAINT [CK_AuditLog_ActorType] CHECK ([ActorType] IN ('user','service')),
        CONSTRAINT [CK_AuditLog_ScopeType] CHECK ([ScopeType] IN ('org','portfolio','project','credit','tenant','system')),
        CONSTRAINT [CK_AuditLog_Outcome] CHECK ([Outcome] IN ('success','failure')),
        CONSTRAINT [CK_AuditLog_BeforeJson] CHECK ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1),
        CONSTRAINT [CK_AuditLog_AfterJson] CHECK ([AfterJson] IS NULL OR ISJSON([AfterJson]) = 1),
        CONSTRAINT [CK_AuditLog_ImmutableHashHex] CHECK ([ImmutableHash] NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT [FK_AuditLog_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AuditLog_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[WhatsAppConsent] (
        [WhatsAppConsentId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [SubjectType] nvarchar(20) NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [ConsentStatus] nvarchar(20) NOT NULL,
        [CaptureMethod] nvarchar(30) NOT NULL,
        [EvidenceUri] nvarchar(1000) NOT NULL,
        [UnsubscribedAt] datetime2(3) NULL,
        [UnsubscribeReason] nvarchar(500) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WhatsAppConsent] PRIMARY KEY CLUSTERED ([WhatsAppConsentId]),
        CONSTRAINT [CK_WhatsAppConsent_SubjectType] CHECK ([SubjectType] IN ('user','contact')),
        CONSTRAINT [CK_WhatsAppConsent_ConsentStatus] CHECK ([ConsentStatus] IN ('granted','withdrawn')),
        CONSTRAINT [CK_WhatsAppConsent_CaptureMethod] CHECK ([CaptureMethod] IN ('inApp','emailLink','userInitiatedMessage')),
        CONSTRAINT [FK_WhatsAppConsent_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[RetentionPolicy] (
        [RetentionPolicyId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [PolicyName] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(30) NOT NULL,
        [RetentionDays] int NOT NULL,
        [SoftDeleteRestoreWindowDays] int NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RetentionPolicy] PRIMARY KEY CLUSTERED ([RetentionPolicyId]),
        CONSTRAINT [UQ_RetentionPolicy_Tenant_EntityType] UNIQUE ([TenantId],[EntityType]),
        CONSTRAINT [CK_RetentionPolicy_EntityType] CHECK ([EntityType] IN ('artifact','document','audit','aiLog','notification','analytics')),
        CONSTRAINT [CK_RetentionPolicy_RetentionDays] CHECK ([RetentionDays] > 0),
        CONSTRAINT [CK_RetentionPolicy_SoftDeleteRestoreWindowDays] CHECK ([SoftDeleteRestoreWindowDays] IS NULL OR [SoftDeleteRestoreWindowDays] > 0),
        CONSTRAINT [CK_RetentionPolicy_Status] CHECK ([Status] IN ('enabled','disabled')),
        CONSTRAINT [FK_RetentionPolicy_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[KPIRecord] (
        [KPIRecordId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [MetricCode] nvarchar(50) NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [BaselineValue] decimal(18,4) NOT NULL,
        [CurrentValue] decimal(18,4) NOT NULL,
        [TrendDirection] nvarchar(10) NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_KPIRecord] PRIMARY KEY CLUSTERED ([KPIRecordId]),
        CONSTRAINT [CK_KPIRecord_MetricCode] CHECK ([MetricCode] IN ('timeToCertification','firstPassAcceptance','costSavings','adoption','energy','carbon','water','waste','cycleTime')),
        CONSTRAINT [CK_KPIRecord_ScopeType] CHECK ([ScopeType] IN ('tenant','portfolio','project')),
        CONSTRAINT [CK_KPIRecord_TrendDirection] CHECK ([TrendDirection] IN ('up','down','flat')),
        CONSTRAINT [CK_KPIRecord_Period] CHECK ([PeriodStart] <= [PeriodEnd]),
        CONSTRAINT [FK_KPIRecord_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[JobQueue] (
        [JobQueueId] bigint IDENTITY(1,1) NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [JobType] nvarchar(50) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Priority] int NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [AvailableAt] datetime2(3) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [StartedAt] datetime2(3) NULL,
        [CompletedAt] datetime2(3) NULL,
        CONSTRAINT [PK_JobQueue] PRIMARY KEY CLUSTERED ([JobQueueId]),
        CONSTRAINT [CK_JobQueue_JobType] CHECK ([JobType] IN ('artifact.scan','artifact.ocr','artifact.extract','artifact.classify','project.preAssessment','scorecard.recalculate','simulation.run','simulation.ingest','document.generate','package.build','notification.dispatch','analytics.refresh','standards.refresh','monitor.riskUpdate','audit.export','training.permission.audit','webhook.delivery')),
        CONSTRAINT [CK_JobQueue_Status] CHECK ([Status] IN ('queued','leased','completed','failed','deadLettered','cancelled')),
        CONSTRAINT [CK_JobQueue_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1),
        CONSTRAINT [FK_JobQueue_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[ScheduledTask] (
        [ScheduledTaskId] bigint IDENTITY(1,1) NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [TaskType] nvarchar(50) NOT NULL,
        [CronExpression] nvarchar(100) NOT NULL,
        [NextRunAt] datetime2(3) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_ScheduledTask] PRIMARY KEY CLUSTERED ([ScheduledTaskId]),
        CONSTRAINT [CK_ScheduledTask_TaskType] CHECK ([TaskType] IN ('evidence.reminder','notification.escalationCheck','analytics.refresh','standards.refresh','risk.monitor','retention.purge')),
        CONSTRAINT [FK_ScheduledTask_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    ------------------------------------------------------------
    -- Project and content tables with wider dependencies
    ------------------------------------------------------------

    CREATE TABLE [project].[Project] (
        [ProjectId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [PortfolioId] uniqueidentifier NULL,
        [ProjectCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [RatingSystemCode] nvarchar(20) NOT NULL,
        [RatingVersion] nvarchar(30) NOT NULL,
        [RatingLibraryId] uniqueidentifier NOT NULL,
        [RegionCode] nvarchar(10) NOT NULL,
        [ClimateZoneCode] nvarchar(30) NULL,
        [RegionalProfileId] uniqueidentifier NOT NULL,
        [Timezone] nvarchar(64) NOT NULL,
        [UnitSystem] nvarchar(10) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [WorkflowState] nvarchar(50) NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [ProjectManagerUserId] uniqueidentifier NULL,
        [TargetCertificationLevel] nvarchar(100) NULL,
        [Description] nvarchar(2000) NULL,
        [CountryCode] char(2) NOT NULL,
        [Typology] nvarchar(100) NULL,
        [GrossFloorArea] decimal(18,2) NULL,
        [Occupancy] int NULL,
        [SchedulesJson] nvarchar(max) NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([ProjectId]),
        CONSTRAINT [UQ_Project_Tenant_ProjectCode] UNIQUE ([TenantId],[ProjectCode]),
        CONSTRAINT [CK_Project_RatingSystemCode] CHECK ([RatingSystemCode] IN ('LEED','IGBC','GRIHA','WELL','EDGE')),
        CONSTRAINT [CK_Project_RegionCode] CHECK ([RegionCode] IN ('IN','MV','QA','ME','NP')),
        CONSTRAINT [CK_Project_ClimateZoneCode] CHECK ([ClimateZoneCode] IS NULL OR [ClimateZoneCode] IN ('IN-Composite','IN-HotDry','IN-WarmHumid','IN-Temperate','IN-Cold')),
        CONSTRAINT [CK_Project_UnitSystem] CHECK ([UnitSystem] IN ('SI','Imperial')),
        CONSTRAINT [CK_Project_Status] CHECK ([Status] IN ('ProjectIntake','PreAssessment','EvidenceCollection','SimulationOrchestration','DocumentationNarrativesCalculators','PackagingForSubmission','ManualPortalUpload','AuditorQA','Resubmission','CertifiedClosed')),
        CONSTRAINT [CK_Project_WorkflowState] CHECK ([WorkflowState] IN ('Project Intake','Pre-assessment','Evidence Collection','Simulation Orchestration','Documentation/Narratives/Calculators','Packaging for Submission','Manual Portal Upload','Auditor Q&A/Clarifications','Resubmission','Certified/Closed')),
        CONSTRAINT [CK_Project_GrossFloorArea] CHECK ([GrossFloorArea] IS NULL OR [GrossFloorArea] >= 0),
        CONSTRAINT [CK_Project_Occupancy] CHECK ([Occupancy] IS NULL OR [Occupancy] >= 0),
        CONSTRAINT [CK_Project_SchedulesJson] CHECK ([SchedulesJson] IS NULL OR ISJSON([SchedulesJson]) = 1),
        CONSTRAINT [CK_Project_Dates] CHECK ([StartDate] IS NULL OR [EndDate] IS NULL OR [StartDate] <= [EndDate]),
        CONSTRAINT [FK_Project_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organization]([OrganizationId]),
        CONSTRAINT [FK_Project_Portfolio] FOREIGN KEY ([PortfolioId]) REFERENCES [core].[Portfolio]([PortfolioId]),
        CONSTRAINT [FK_Project_RatingLibrary] FOREIGN KEY ([RatingLibraryId]) REFERENCES [core].[RatingLibrary]([RatingLibraryId]),
        CONSTRAINT [FK_Project_RegionalProfile] FOREIGN KEY ([RegionalProfileId]) REFERENCES [core].[RegionalProfile]([RegionalProfileId]),
        CONSTRAINT [FK_Project_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_Project_ProjectManagerUser] FOREIGN KEY ([ProjectManagerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[AccessInvite] (
        [AccessInviteId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [TargetOrganizationId] uniqueidentifier NULL,
        [DefaultAccessLevel] nvarchar(20) NULL,
        [Email] nvarchar(320) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ExpiresAt] datetime2(3) NOT NULL,
        [InvitedByUserId] uniqueidentifier NOT NULL,
        [AcceptedByUserId] uniqueidentifier NULL,
        [AcceptedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AccessInvite] PRIMARY KEY CLUSTERED ([AccessInviteId]),
        CONSTRAINT [CK_AccessInvite_Status] CHECK ([Status] IN ('pending','accepted','rejected','expired','cancelled')),
        CONSTRAINT [CK_AccessInvite_DefaultAccessLevel] CHECK ([DefaultAccessLevel] IS NULL OR [DefaultAccessLevel] IN ('readOnly')),
        CONSTRAINT [FK_AccessInvite_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AccessInvite_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_AccessInvite_TargetOrganization] FOREIGN KEY ([TargetOrganizationId]) REFERENCES [core].[Organization]([OrganizationId]),
        CONSTRAINT [FK_AccessInvite_InvitedBy] FOREIGN KEY ([InvitedByUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_AccessInvite_AcceptedBy] FOREIGN KEY ([AcceptedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [core].[AccessInviteRole] (
        [AccessInviteRoleId] uniqueidentifier NOT NULL,
        [AccessInviteId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AccessInviteRole] PRIMARY KEY CLUSTERED ([AccessInviteRoleId]),
        CONSTRAINT [UQ_AccessInviteRole] UNIQUE ([AccessInviteId],[RoleId]),
        CONSTRAINT [FK_AccessInviteRole_Invite] FOREIGN KEY ([AccessInviteId]) REFERENCES [core].[AccessInvite]([AccessInviteId]),
        CONSTRAINT [FK_AccessInviteRole_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId])
    );

    CREATE TABLE [core].[AccessInviteScope] (
        [AccessInviteScopeId] uniqueidentifier NOT NULL,
        [AccessInviteId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AccessInviteScope] PRIMARY KEY CLUSTERED ([AccessInviteScopeId]),
        CONSTRAINT [UQ_AccessInviteScope] UNIQUE ([AccessInviteId],[ScopeType],[ScopeId],[RoleId]),
        CONSTRAINT [CK_AccessInviteScope_ScopeType] CHECK ([ScopeType] IN ('org','portfolio','project','credit')),
        CONSTRAINT [FK_AccessInviteScope_Invite] FOREIGN KEY ([AccessInviteId]) REFERENCES [core].[AccessInvite]([AccessInviteId]),
        CONSTRAINT [FK_AccessInviteScope_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId])
    );

    CREATE TABLE [core].[AccessGrant] (
        [AccessGrantId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [InviteId] uniqueidentifier NULL,
        [ProjectId] uniqueidentifier NULL,
        [OrganizationId] uniqueidentifier NULL,
        [AccessLevel] nvarchar(20) NULL,
        [ElevatedByUserId] uniqueidentifier NULL,
        [Active] bit NOT NULL CONSTRAINT [DF_AccessGrant_Active] DEFAULT ((1)),
        [SensitivityApprovalRequirement] nvarchar(20) NULL,
        [SubjectUserId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [GrantedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AccessGrant] PRIMARY KEY CLUSTERED ([AccessGrantId]),
        CONSTRAINT [UQ_AccessGrant] UNIQUE ([TenantId],[SubjectUserId],[ScopeType],[ScopeId],[RoleId]),
        CONSTRAINT [CK_AccessGrant_ScopeType] CHECK ([ScopeType] IN ('org','portfolio','project','credit')),
        CONSTRAINT [CK_AccessGrant_AccessLevel] CHECK ([AccessLevel] IS NULL OR [AccessLevel] IN ('readOnly','elevated')),
        CONSTRAINT [CK_AccessGrant_SensitivityApprovalRequirement] CHECK ([SensitivityApprovalRequirement] IS NULL OR [SensitivityApprovalRequirement] IN ('single','dual')),
        CONSTRAINT [FK_AccessGrant_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AccessGrant_Invite] FOREIGN KEY ([InviteId]) REFERENCES [core].[AccessInvite]([AccessInviteId]),
        CONSTRAINT [FK_AccessGrant_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_AccessGrant_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [core].[Organization]([OrganizationId]),
        CONSTRAINT [FK_AccessGrant_SubjectUser] FOREIGN KEY ([SubjectUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_AccessGrant_Role] FOREIGN KEY ([RoleId]) REFERENCES [core].[RoleTemplate]([RoleId]),
        CONSTRAINT [FK_AccessGrant_GrantedBy] FOREIGN KEY ([GrantedByUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_AccessGrant_ElevatedBy] FOREIGN KEY ([ElevatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[ProjectArea] (
        [ProjectAreaId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AreaName] nvarchar(200) NOT NULL,
        [AreaType] nvarchar(50) NOT NULL,
        [ParentProjectAreaId] uniqueidentifier NULL,
        [AreaValue] decimal(18,2) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [UsageType] nvarchar(100) NULL,
        CONSTRAINT [PK_ProjectArea] PRIMARY KEY CLUSTERED ([ProjectAreaId]),
        CONSTRAINT [CK_ProjectArea_AreaType] CHECK ([AreaType] IN ('gross','net','regularlyOccupied','landscape','parking','mixedUse','other')),
        CONSTRAINT [CK_ProjectArea_AreaValue] CHECK ([AreaValue] >= 0),
        CONSTRAINT [FK_ProjectArea_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_ProjectArea_Parent] FOREIGN KEY ([ParentProjectAreaId]) REFERENCES [project].[ProjectArea]([ProjectAreaId])
    );

    CREATE TABLE [project].[ProjectStakeholder] (
        [ProjectStakeholderId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StakeholderGroup] nvarchar(50) NOT NULL,
        [IsPrimary] bit NOT NULL,
        CONSTRAINT [PK_ProjectStakeholder] PRIMARY KEY CLUSTERED ([ProjectStakeholderId]),
        CONSTRAINT [UQ_ProjectStakeholder] UNIQUE ([ProjectId],[UserId],[StakeholderGroup]),
        CONSTRAINT [CK_ProjectStakeholder_Group] CHECK ([StakeholderGroup] IN ('Sustainability Consultant','Owner','Architect','MEP','PMC','Landscape Consultant','Construction Team','Procurement Team','External Auditor','Admin')),
        CONSTRAINT [FK_ProjectStakeholder_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_ProjectStakeholder_User] FOREIGN KEY ([UserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[ProjectTransferRequest] (
        [ProjectTransferRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SourceOrganizationId] uniqueidentifier NULL,
        [TargetOrganizationId] uniqueidentifier NULL,
        [ApprovalStatus] nvarchar(20) NULL,
        [RequiredApprovalCount] int NULL,
        [ApprovalCount] int NULL,
        [SourceHandoverAccessExpiresAt] datetime2(3) NULL,
        [HandoverAccessExtensionApproved] bit NULL,
        [SegregationOfDutiesPassed] bit NULL,
        [FromAdminUserId] uniqueidentifier NOT NULL,
        [ToAdminUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ProjectTransferRequest] PRIMARY KEY CLUSTERED ([ProjectTransferRequestId]),
        CONSTRAINT [CK_ProjectTransferRequest_Status] CHECK ([Status] IN ('requested','confirmed','rejected','completed')),
        CONSTRAINT [CK_ProjectTransferRequest_ApprovalStatus] CHECK ([ApprovalStatus] IS NULL OR [ApprovalStatus] IN ('pending','approved','rejected')),
        CONSTRAINT [CK_ProjectTransferRequest_SoD] CHECK ([FromAdminUserId] <> [ToAdminUserId]),
        CONSTRAINT [CK_ProjectTransferRequest_ApprovalCounts] CHECK ([RequiredApprovalCount] IS NULL OR ([RequiredApprovalCount] >= 1 AND [ApprovalCount] IS NOT NULL AND [ApprovalCount] >= 0 AND [ApprovalCount] <= [RequiredApprovalCount])),
        CONSTRAINT [FK_ProjectTransferRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ProjectTransferRequest_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_ProjectTransferRequest_SourceOrganization] FOREIGN KEY ([SourceOrganizationId]) REFERENCES [core].[Organization]([OrganizationId]),
        CONSTRAINT [FK_ProjectTransferRequest_TargetOrganization] FOREIGN KEY ([TargetOrganizationId]) REFERENCES [core].[Organization]([OrganizationId]),
        CONSTRAINT [FK_ProjectTransferRequest_FromAdmin] FOREIGN KEY ([FromAdminUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_ProjectTransferRequest_ToAdmin] FOREIGN KEY ([ToAdminUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[ProjectIntakeRecord] (
        [ProjectIntakeRecordId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ProjectName] nvarchar(200) NOT NULL,
        [ProjectCode] nvarchar(50) NOT NULL,
        [RatingSystemCode] nvarchar(20) NOT NULL,
        [RatingVersion] nvarchar(30) NOT NULL,
        [SiteName] nvarchar(200) NULL,
        [SiteAddress] nvarchar(1000) NULL,
        [RegionCode] nvarchar(10) NOT NULL,
        [ClimateZoneCode] nvarchar(30) NULL,
        [OwnerOrganizationName] nvarchar(200) NOT NULL,
        [PlannedStartDate] date NULL,
        [PlannedEndDate] date NULL,
        [BaselineBudgetAmount] decimal(18,2) NULL,
        [BudgetCurrencyCode] char(3) NULL,
        [ImportSourceType] nvarchar(20) NULL,
        [ImportSourceSystem] nvarchar(50) NULL,
        [ImportReference] nvarchar(1000) NULL,
        [StakeholderAssignmentsJson] nvarchar(max) NOT NULL,
        [DefaultedFieldsJson] nvarchar(max) NOT NULL,
        [DerivedFieldsJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ProjectIntakeRecord] PRIMARY KEY CLUSTERED ([ProjectIntakeRecordId]),
        CONSTRAINT [UQ_ProjectIntakeRecord_Tenant_ProjectCode] UNIQUE ([TenantId],[ProjectCode]),
        CONSTRAINT [CK_ProjectIntakeRecord_RatingSystemCode] CHECK ([RatingSystemCode] IN ('LEED','IGBC','GRIHA','WELL','EDGE')),
        CONSTRAINT [CK_ProjectIntakeRecord_RegionCode] CHECK ([RegionCode] IN ('IN','MV','QA','ME','NP')),
        CONSTRAINT [CK_ProjectIntakeRecord_ClimateZoneCode] CHECK ([ClimateZoneCode] IS NULL OR [ClimateZoneCode] IN ('IN-Composite','IN-HotDry','IN-WarmHumid','IN-Temperate','IN-Cold')),
        CONSTRAINT [CK_ProjectIntakeRecord_ImportSourceType] CHECK ([ImportSourceType] IS NULL OR [ImportSourceType] IN ('manual','import')),
        CONSTRAINT [CK_ProjectIntakeRecord_StakeholderAssignmentsJson] CHECK (ISJSON([StakeholderAssignmentsJson]) = 1),
        CONSTRAINT [CK_ProjectIntakeRecord_DefaultedFieldsJson] CHECK (ISJSON([DefaultedFieldsJson]) = 1),
        CONSTRAINT [CK_ProjectIntakeRecord_DerivedFieldsJson] CHECK (ISJSON([DerivedFieldsJson]) = 1),
        CONSTRAINT [CK_ProjectIntakeRecord_Dates] CHECK ([PlannedStartDate] IS NULL OR [PlannedEndDate] IS NULL OR [PlannedStartDate] <= [PlannedEndDate]),
        CONSTRAINT [CK_ProjectIntakeRecord_BaselineBudgetAmount] CHECK ([BaselineBudgetAmount] IS NULL OR [BaselineBudgetAmount] >= 0),
        CONSTRAINT [FK_ProjectIntakeRecord_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ProjectIntakeRecord_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId])
    );

    CREATE TABLE [content].[ImportTemplate] (
        [ImportTemplateId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ArtifactType] nvarchar(50) NOT NULL,
        [MetadataSchemaJson] nvarchar(max) NOT NULL,
        [MandatoryFieldsJson] nvarchar(max) NOT NULL,
        [ValidationRulesJson] nvarchar(max) NOT NULL,
        [NamingRule] nvarchar(500) NULL,
        [DefaultCreditMappingsJson] nvarchar(max) NOT NULL,
        [MaxFileSizeMb] int NOT NULL,
        [Active] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ImportTemplate] PRIMARY KEY CLUSTERED ([ImportTemplateId]),
        CONSTRAINT [UQ_ImportTemplate_Tenant_ArtifactType] UNIQUE ([TenantId],[ArtifactType]),
        CONSTRAINT [CK_ImportTemplate_MetadataSchemaJson] CHECK (ISJSON([MetadataSchemaJson]) = 1),
        CONSTRAINT [CK_ImportTemplate_MandatoryFieldsJson] CHECK (ISJSON([MandatoryFieldsJson]) = 1),
        CONSTRAINT [CK_ImportTemplate_ValidationRulesJson] CHECK (ISJSON([ValidationRulesJson]) = 1),
        CONSTRAINT [CK_ImportTemplate_DefaultCreditMappingsJson] CHECK (ISJSON([DefaultCreditMappingsJson]) = 1),
        CONSTRAINT [CK_ImportTemplate_MaxFileSizeMb] CHECK ([MaxFileSizeMb] > 0),
        CONSTRAINT [FK_ImportTemplate_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [content].[Artifact] (
        [ArtifactId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NULL,
        [OwnerUserId] uniqueidentifier NULL,
        [FileName] nvarchar(500) NOT NULL,
        [MediaType] nvarchar(200) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [StorageUri] nvarchar(1000) NOT NULL,
        [ChecksumSha256] char(64) NOT NULL,
        [UploadStatus] nvarchar(20) NOT NULL,
        [AntivirusStatus] nvarchar(20) NOT NULL,
        [ContentValidationStatus] nvarchar(20) NOT NULL,
        [SearchableTextStatus] nvarchar(20) NOT NULL,
        [ExtractionStatus] nvarchar(20) NOT NULL,
        [ClassificationStatus] nvarchar(20) NOT NULL,
        [SourceType] nvarchar(20) NOT NULL,
        [SourceSystem] nvarchar(50) NULL,
        [SourceReference] nvarchar(1000) NULL,
        [SystemOfRecord] bit NOT NULL,
        [MetadataJson] nvarchar(max) NOT NULL,
        [ArtifactDate] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Artifact] PRIMARY KEY CLUSTERED ([ArtifactId]),
        CONSTRAINT [UQ_Artifact_Project_Checksum_FileName] UNIQUE ([ProjectId],[ChecksumSha256],[FileName]),
        CONSTRAINT [CK_Artifact_SizeBytes] CHECK ([SizeBytes] >= 0),
        CONSTRAINT [CK_Artifact_UploadStatus] CHECK ([UploadStatus] IN ('uploaded','scanning','quarantined','validated','rejected','processed')),
        CONSTRAINT [CK_Artifact_AntivirusStatus] CHECK ([AntivirusStatus] IN ('pending','clean','infected','error')),
        CONSTRAINT [CK_Artifact_ContentValidationStatus] CHECK ([ContentValidationStatus] IN ('pending','valid','invalid')),
        CONSTRAINT [CK_Artifact_SearchableTextStatus] CHECK ([SearchableTextStatus] IN ('pending','ready','failed')),
        CONSTRAINT [CK_Artifact_ExtractionStatus] CHECK ([ExtractionStatus] IN ('pending','inReview','approved','rejected','notApplicable')),
        CONSTRAINT [CK_Artifact_ClassificationStatus] CHECK ([ClassificationStatus] IN ('pending','suggested','approved','corrected','rejected')),
        CONSTRAINT [CK_Artifact_SourceType] CHECK ([SourceType] IN ('upload','import','simulation','generated','email')),
        CONSTRAINT [CK_Artifact_SourceSystem] CHECK ([SourceSystem] IS NULL OR [SourceSystem] IN ('accManual','autodeskAcc','internalFileShare','email','spreadsheetUpload','cadUpload','bimUpload','simulationResult','equest','revit','other')),
        CONSTRAINT [CK_Artifact_MetadataJson] CHECK (ISJSON([MetadataJson]) = 1),
        CONSTRAINT [CK_Artifact_ChecksumHex] CHECK ([ChecksumSha256] NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT [FK_Artifact_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Artifact_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_Artifact_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_Artifact_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[PreAssessmentRun] (
        [PreAssessmentRunId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ScenarioName] nvarchar(200) NULL,
        [Status] nvarchar(20) NOT NULL,
        [IncludeMasterData] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [QueuedAt] datetime2(3) NOT NULL,
        [CompletedAt] datetime2(3) NULL,
        [Confidence] decimal(5,4) NULL,
        [ScoresJson] nvarchar(max) NULL,
        [GraphsJson] nvarchar(max) NULL,
        [RecommendationsJson] nvarchar(max) NULL,
        [StakeholderActionItemsJson] nvarchar(max) NULL,
        [RationalesJson] nvarchar(max) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PreAssessmentRun] PRIMARY KEY CLUSTERED ([PreAssessmentRunId]),
        CONSTRAINT [CK_PreAssessmentRun_Status] CHECK ([Status] IN ('queued','running','completed','failed','cancelled')),
        CONSTRAINT [CK_PreAssessmentRun_Confidence] CHECK ([Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)),
        CONSTRAINT [CK_PreAssessmentRun_ScoresJson] CHECK ([ScoresJson] IS NULL OR ISJSON([ScoresJson]) = 1),
        CONSTRAINT [CK_PreAssessmentRun_GraphsJson] CHECK ([GraphsJson] IS NULL OR ISJSON([GraphsJson]) = 1),
        CONSTRAINT [CK_PreAssessmentRun_RecommendationsJson] CHECK ([RecommendationsJson] IS NULL OR ISJSON([RecommendationsJson]) = 1),
        CONSTRAINT [CK_PreAssessmentRun_ActionItemsJson] CHECK ([StakeholderActionItemsJson] IS NULL OR ISJSON([StakeholderActionItemsJson]) = 1),
        CONSTRAINT [CK_PreAssessmentRun_RationalesJson] CHECK ([RationalesJson] IS NULL OR ISJSON([RationalesJson]) = 1),
        CONSTRAINT [FK_PreAssessmentRun_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_PreAssessmentRun_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_PreAssessmentRun_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[PreAssessmentInputArtifact] (
        [PreAssessmentInputArtifactId] uniqueidentifier NOT NULL,
        [PreAssessmentRunId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PreAssessmentInputArtifact] PRIMARY KEY CLUSTERED ([PreAssessmentInputArtifactId]),
        CONSTRAINT [UQ_PreAssessmentInputArtifact] UNIQUE ([PreAssessmentRunId],[ArtifactId]),
        CONSTRAINT [FK_PreAssessmentInputArtifact_Run] FOREIGN KEY ([PreAssessmentRunId]) REFERENCES [project].[PreAssessmentRun]([PreAssessmentRunId]),
        CONSTRAINT [FK_PreAssessmentInputArtifact_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [project].[InterpretationResult] (
        [InterpretationResultId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        [RatingSystemCode] nvarchar(20) NOT NULL,
        [RatingVersion] nvarchar(30) NOT NULL,
        [LocationCode] nvarchar(10) NOT NULL,
        [RuleVersionId] nvarchar(100) NOT NULL,
        [PreferenceVersionId] nvarchar(100) NULL,
        [Applicability] nvarchar(30) NOT NULL,
        [Rationale] nvarchar(max) NOT NULL,
        [Confidence] decimal(5,4) NOT NULL,
        [ModelIdentifier] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_InterpretationResult] PRIMARY KEY CLUSTERED ([InterpretationResultId]),
        CONSTRAINT [CK_InterpretationResult_RatingSystemCode] CHECK ([RatingSystemCode] IN ('LEED','IGBC','GRIHA','WELL','EDGE')),
        CONSTRAINT [CK_InterpretationResult_LocationCode] CHECK ([LocationCode] IN ('IN','MV','QA','ME','NP')),
        CONSTRAINT [CK_InterpretationResult_Applicability] CHECK ([Applicability] IN ('applicable','conditionallyApplicable','notApplicable')),
        CONSTRAINT [CK_InterpretationResult_Confidence] CHECK ([Confidence] >= 0 AND [Confidence] <= 1),
        CONSTRAINT [FK_InterpretationResult_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_InterpretationResult_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_InterpretationResult_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[InterpretationAddendum] (
        [InterpretationAddendumId] uniqueidentifier NOT NULL,
        [InterpretationResultId] uniqueidentifier NOT NULL,
        [AddendumId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InterpretationAddendum] PRIMARY KEY CLUSTERED ([InterpretationAddendumId]),
        CONSTRAINT [UQ_InterpretationAddendum] UNIQUE ([InterpretationResultId],[AddendumId]),
        CONSTRAINT [FK_InterpretationAddendum_Result] FOREIGN KEY ([InterpretationResultId]) REFERENCES [project].[InterpretationResult]([InterpretationResultId]),
        CONSTRAINT [FK_InterpretationAddendum_Addendum] FOREIGN KEY ([AddendumId]) REFERENCES [core].[Addendum]([AddendumId])
    );

    CREATE TABLE [project].[SourceCitation] (
        [SourceCitationId] uniqueidentifier NOT NULL,
        [OwnerEntityType] nvarchar(30) NOT NULL,
        [OwnerEntityId] uniqueidentifier NOT NULL,
        [SourceType] nvarchar(30) NOT NULL,
        [SourceId] nvarchar(100) NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Excerpt] nvarchar(max) NULL,
        [VersionIdentifier] nvarchar(100) NULL,
        [LicenseStatus] nvarchar(30) NULL,
        CONSTRAINT [PK_SourceCitation] PRIMARY KEY CLUSTERED ([SourceCitationId]),
        CONSTRAINT [CK_SourceCitation_OwnerEntityType] CHECK ([OwnerEntityType] IN ('interpretation','recommendation','correctiveAction','standardsAnswer','qaSuggestion')),
        CONSTRAINT [CK_SourceCitation_SourceType] CHECK ([SourceType] IN ('ratingRule','addendum','standard','evidence','licensedCorpus')),
        CONSTRAINT [CK_SourceCitation_LicenseStatus] CHECK ([LicenseStatus] IS NULL OR [LicenseStatus] IN ('licensed','permittedPublic','internal'))
    );

    CREATE TABLE [project].[WhatIfScenario] (
        [WhatIfScenarioId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [ParameterChangesJson] nvarchar(max) NOT NULL,
        [BaselineScore] decimal(9,2) NOT NULL,
        [ScenarioScore] decimal(9,2) NOT NULL,
        [ImpactsJson] nvarchar(max) NOT NULL,
        [Saved] bit NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WhatIfScenario] PRIMARY KEY CLUSTERED ([WhatIfScenarioId]),
        CONSTRAINT [UQ_WhatIfScenario_Project_Name] UNIQUE ([ProjectId],[Name]),
        CONSTRAINT [CK_WhatIfScenario_ParameterChangesJson] CHECK (ISJSON([ParameterChangesJson]) = 1),
        CONSTRAINT [CK_WhatIfScenario_ImpactsJson] CHECK (ISJSON([ImpactsJson]) = 1),
        CONSTRAINT [FK_WhatIfScenario_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_WhatIfScenario_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_WhatIfScenario_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[WhatIfScenarioTargetCredit] (
        [WhatIfScenarioTargetCreditId] uniqueidentifier NOT NULL,
        [WhatIfScenarioId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WhatIfScenarioTargetCredit] PRIMARY KEY CLUSTERED ([WhatIfScenarioTargetCreditId]),
        CONSTRAINT [UQ_WhatIfScenarioTargetCredit] UNIQUE ([WhatIfScenarioId],[CreditId]),
        CONSTRAINT [FK_WhatIfScenarioTargetCredit_Scenario] FOREIGN KEY ([WhatIfScenarioId]) REFERENCES [project].[WhatIfScenario]([WhatIfScenarioId]),
        CONSTRAINT [FK_WhatIfScenarioTargetCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[Scorecard] (
        [ScorecardId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [ScenarioId] uniqueidentifier NULL,
        [TotalPossiblePoints] decimal(9,2) NOT NULL,
        [ProjectedPoints] decimal(9,2) NOT NULL,
        [AchievedPoints] decimal(9,2) NOT NULL,
        [PrerequisiteStatus] nvarchar(20) NOT NULL,
        [DependencyStatus] nvarchar(20) NOT NULL,
        [LastRecalculatedAt] datetime2(3) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Scorecard] PRIMARY KEY CLUSTERED ([ScorecardId]),
        CONSTRAINT [CK_Scorecard_TotalPossiblePoints] CHECK ([TotalPossiblePoints] >= 0),
        CONSTRAINT [CK_Scorecard_ProjectedPoints] CHECK ([ProjectedPoints] >= 0),
        CONSTRAINT [CK_Scorecard_AchievedPoints] CHECK ([AchievedPoints] >= 0),
        CONSTRAINT [CK_Scorecard_PrerequisiteStatus] CHECK ([PrerequisiteStatus] IN ('pass','fail','warning')),
        CONSTRAINT [CK_Scorecard_DependencyStatus] CHECK ([DependencyStatus] IN ('valid','blocked','warning')),
        CONSTRAINT [FK_Scorecard_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Scorecard_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_Scorecard_Scenario] FOREIGN KEY ([ScenarioId]) REFERENCES [project].[WhatIfScenario]([WhatIfScenarioId])
    );

    CREATE TABLE [project].[ScorecardCredit] (
        [ScorecardCreditId] uniqueidentifier NOT NULL,
        [ScorecardId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ProjectedPoints] decimal(9,2) NOT NULL,
        [AchievedPoints] decimal(9,2) NOT NULL,
        [PrerequisiteMet] bit NOT NULL,
        [OverrideStatus] nvarchar(20) NOT NULL,
        [Rationale] nvarchar(max) NULL,
        CONSTRAINT [PK_ScorecardCredit] PRIMARY KEY CLUSTERED ([ScorecardCreditId]),
        CONSTRAINT [UQ_ScorecardCredit_Scorecard_Credit] UNIQUE ([ScorecardId],[CreditId]),
        CONSTRAINT [CK_ScorecardCredit_Status] CHECK ([Status] IN ('notStarted','inProgress','eligible','blocked','submitted','accepted','rejected')),
        CONSTRAINT [CK_ScorecardCredit_ProjectedPoints] CHECK ([ProjectedPoints] >= 0),
        CONSTRAINT [CK_ScorecardCredit_AchievedPoints] CHECK ([AchievedPoints] >= 0),
        CONSTRAINT [CK_ScorecardCredit_OverrideStatus] CHECK ([OverrideStatus] IN ('none','pendingApproval','approved','rejected')),
        CONSTRAINT [FK_ScorecardCredit_Scorecard] FOREIGN KEY ([ScorecardId]) REFERENCES [project].[Scorecard]([ScorecardId]),
        CONSTRAINT [FK_ScorecardCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[ScorecardCreditDependency] (
        [ScorecardCreditDependencyId] uniqueidentifier NOT NULL,
        [ScorecardCreditId] uniqueidentifier NOT NULL,
        [DependsOnCreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ScorecardCreditDependency] PRIMARY KEY CLUSTERED ([ScorecardCreditDependencyId]),
        CONSTRAINT [UQ_ScorecardCreditDependency] UNIQUE ([ScorecardCreditId],[DependsOnCreditId]),
        CONSTRAINT [FK_ScorecardCreditDependency_ScorecardCredit] FOREIGN KEY ([ScorecardCreditId]) REFERENCES [project].[ScorecardCredit]([ScorecardCreditId]),
        CONSTRAINT [FK_ScorecardCreditDependency_DependsOn] FOREIGN KEY ([DependsOnCreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[ScoreOverride] (
        [ScoreOverrideId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [ReasonCode] nvarchar(50) NOT NULL,
        [Justification] nvarchar(max) NOT NULL,
        [RequestedValueJson] nvarchar(max) NOT NULL,
        [ApprovalStatus] nvarchar(20) NOT NULL,
        [ApprovalCount] int NOT NULL,
        [RequiredApprovalCount] int NOT NULL,
        [SegregationOfDutiesPassed] bit NOT NULL,
        [SupersededByOverrideId] uniqueidentifier NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ScoreOverride] PRIMARY KEY CLUSTERED ([ScoreOverrideId]),
        CONSTRAINT [CK_ScoreOverride_ReasonCode] CHECK ([ReasonCode] IN ('manualAssessment','exception','evidenceUpdate','auditorFeedback','calculationCorrection','policyDecision')),
        CONSTRAINT [CK_ScoreOverride_RequestedValueJson] CHECK (ISJSON([RequestedValueJson]) = 1),
        CONSTRAINT [CK_ScoreOverride_ApprovalStatus] CHECK ([ApprovalStatus] IN ('pending','approved','rejected','superseded')),
        CONSTRAINT [CK_ScoreOverride_ApprovalCount] CHECK ([ApprovalCount] >= 0),
        CONSTRAINT [CK_ScoreOverride_RequiredApprovalCount] CHECK ([RequiredApprovalCount] >= 1),
        CONSTRAINT [CK_ScoreOverride_ApprovalCount_Max] CHECK ([ApprovalCount] <= [RequiredApprovalCount]),
        CONSTRAINT [FK_ScoreOverride_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ScoreOverride_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_ScoreOverride_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_ScoreOverride_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_ScoreOverride_SupersededBy] FOREIGN KEY ([SupersededByOverrideId]) REFERENCES [project].[ScoreOverride]([ScoreOverrideId])
    );

    CREATE TABLE [project].[ApprovalEvent] (
        [ApprovalEventId] uniqueidentifier NOT NULL,
        [OwnerEntityType] nvarchar(30) NOT NULL,
        [OwnerEntityId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_ApprovalEvent] PRIMARY KEY CLUSTERED ([ApprovalEventId]),
        CONSTRAINT [CK_ApprovalEvent_OwnerEntityType] CHECK ([OwnerEntityType] IN ('scoreOverride','submissionPackage','auditExport','secureDeletion')),
        CONSTRAINT [CK_ApprovalEvent_Action] CHECK ([Action] IN ('request','approve','reject')),
        CONSTRAINT [FK_ApprovalEvent_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[EvidenceTask] (
        [EvidenceTaskId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [DueDate] datetime2(3) NOT NULL,
        [Cadence] nvarchar(20) NOT NULL,
        [ReminderRule] nvarchar(500) NOT NULL,
        [EscalationRule] nvarchar(500) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [SlaStatus] nvarchar(20) NOT NULL,
        [LastReminderAt] datetime2(3) NULL,
        [LastEscalationAt] datetime2(3) NULL,
        [DueDayOfMonth] tinyint NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_EvidenceTask] PRIMARY KEY CLUSTERED ([EvidenceTaskId]),
        CONSTRAINT [CK_EvidenceTask_Cadence] CHECK ([Cadence] IN ('monthly','weekly','adHoc')),
        CONSTRAINT [CK_EvidenceTask_Status] CHECK ([Status] IN ('NotRequested','Requested','InProgress','Submitted','UnderReview','Approved','RevisionRequired','Resubmitted','Rejected','Overdue','WaivedNotApplicable','LockedForSubmission')),
        CONSTRAINT [CK_EvidenceTask_SlaStatus] CHECK ([SlaStatus] IN ('onTrack','atRisk','breached')),
        CONSTRAINT [CK_EvidenceTask_DueDayOfMonth] CHECK ([DueDayOfMonth] IS NULL OR [DueDayOfMonth] BETWEEN 1 AND 28),
        CONSTRAINT [FK_EvidenceTask_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_EvidenceTask_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_EvidenceTask_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_EvidenceTask_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[EvidenceTaskValidationRule] (
        [EvidenceTaskValidationRuleId] uniqueidentifier NOT NULL,
        [EvidenceTaskId] uniqueidentifier NOT NULL,
        [RuleExpression] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_EvidenceTaskValidationRule] PRIMARY KEY CLUSTERED ([EvidenceTaskValidationRuleId]),
        CONSTRAINT [FK_EvidenceTaskValidationRule_Task] FOREIGN KEY ([EvidenceTaskId]) REFERENCES [project].[EvidenceTask]([EvidenceTaskId])
    );

    CREATE TABLE [project].[SimulationJob] (
        [SimulationJobId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Tool] nvarchar(30) NOT NULL,
        [SimulationType] nvarchar(30) NOT NULL,
        [RegionalProfileId] uniqueidentifier NOT NULL,
        [WeatherFileCode] nvarchar(100) NOT NULL,
        [AssistedPrepUsed] bit NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [QueueEnteredAt] datetime2(3) NOT NULL,
        [StartedAt] datetime2(3) NULL,
        [CompletedAt] datetime2(3) NULL,
        [RuntimeBudgetSeconds] int NOT NULL,
        [RetryCount] int NOT NULL,
        [MaxRetries] int NOT NULL,
        [WorkerId] nvarchar(100) NULL,
        [ReproducibilitySeed] nvarchar(100) NOT NULL,
        [InputFingerprint] nvarchar(128) NOT NULL,
        [ExternalRunReference] nvarchar(200) NULL,
        [ScenarioId] uniqueidentifier NULL,
        [ResultPackageValidationStatus] nvarchar(20) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SimulationJob] PRIMARY KEY CLUSTERED ([SimulationJobId]),
        CONSTRAINT [CK_SimulationJob_Tool] CHECK ([Tool] IN ('rhinoGrasshopper','designBuilder','equest','iesve','pyrosim')),
        CONSTRAINT [CK_SimulationJob_Type] CHECK ([SimulationType] IN ('daylight','glare','energy','other')),
        CONSTRAINT [CK_SimulationJob_Status] CHECK ([Status] IN ('queued','preparing','running','retrying','completed','failed','cancelled','deadLettered')),
        CONSTRAINT [CK_SimulationJob_RuntimeBudgetSeconds] CHECK ([RuntimeBudgetSeconds] > 0),
        CONSTRAINT [CK_SimulationJob_RetryCount] CHECK ([RetryCount] >= 0),
        CONSTRAINT [CK_SimulationJob_MaxRetries] CHECK ([MaxRetries] BETWEEN 0 AND 2),
        CONSTRAINT [CK_SimulationJob_RetryCount_Max] CHECK ([RetryCount] <= [MaxRetries]),
        CONSTRAINT [CK_SimulationJob_ResultPackageValidationStatus] CHECK ([ResultPackageValidationStatus] IS NULL OR [ResultPackageValidationStatus] IN ('pending','valid','invalid','manualReview')),
        CONSTRAINT [FK_SimulationJob_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_SimulationJob_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_SimulationJob_RegionalProfile] FOREIGN KEY ([RegionalProfileId]) REFERENCES [core].[RegionalProfile]([RegionalProfileId]),
        CONSTRAINT [FK_SimulationJob_Scenario] FOREIGN KEY ([ScenarioId]) REFERENCES [project].[WhatIfScenario]([WhatIfScenarioId])
    );

    CREATE TABLE [project].[Milestone] (
        [MilestoneId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [PlannedDate] date NOT NULL,
        [ActualDate] date NULL,
        [Status] nvarchar(20) NOT NULL,
        [StageGate] nvarchar(100) NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Milestone] PRIMARY KEY CLUSTERED ([MilestoneId]),
        CONSTRAINT [CK_Milestone_Status] CHECK ([Status] IN ('notStarted','inProgress','completed','delayed')),
        CONSTRAINT [CK_Milestone_Dates] CHECK ([ActualDate] IS NULL OR [ActualDate] >= [PlannedDate]),
        CONSTRAINT [FK_Milestone_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Milestone_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_Milestone_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[Budget] (
        [BudgetId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [BaselineAmount] decimal(18,2) NOT NULL,
        [CurrentAmount] decimal(18,2) NOT NULL,
        [VarianceAmount] AS ([CurrentAmount]-[BaselineAmount]) PERSISTED,
        [VariancePercent] AS (ROUND((([CurrentAmount]-[BaselineAmount])/NULLIF([BaselineAmount],(0)))*(100),(2))) PERSISTED,
        [AlertThresholdPercent] decimal(9,2) NOT NULL,
        [CurrencyCode] char(3) NOT NULL,
        [AcknowledgedByUserId] uniqueidentifier NULL,
        [AcknowledgedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Budget] PRIMARY KEY CLUSTERED ([BudgetId]),
        CONSTRAINT [CK_Budget_BaselineAmount] CHECK ([BaselineAmount] > 0),
        CONSTRAINT [CK_Budget_CurrentAmount] CHECK ([CurrentAmount] >= 0),
        CONSTRAINT [CK_Budget_AlertThresholdPercent] CHECK ([AlertThresholdPercent] >= 0),
        CONSTRAINT [FK_Budget_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Budget_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_Budget_AcknowledgedBy] FOREIGN KEY ([AcknowledgedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[CarbonResult] (
        [CarbonResultId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NULL,
        [ArtifactId] uniqueidentifier NULL,
        [SourceReference] nvarchar(200) NULL,
        [ScenarioLabel] nvarchar(200) NULL,
        [ResultType] nvarchar(50) NOT NULL,
        [MetricsJson] nvarchar(max) NOT NULL,
        [ProviderCode] nvarchar(50) NULL,
        [SourceMetadataJson] nvarchar(max) NULL,
        [ImportedAt] datetime2(3) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CarbonResult] PRIMARY KEY CLUSTERED ([CarbonResultId]),
        CONSTRAINT [CK_CarbonResult_MetricsJson] CHECK (ISJSON([MetricsJson]) = 1),
        CONSTRAINT [CK_CarbonResult_SourceMetadataJson] CHECK ([SourceMetadataJson] IS NULL OR ISJSON([SourceMetadataJson]) = 1),
        CONSTRAINT [FK_CarbonResult_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_CarbonResult_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_CarbonResult_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_CarbonResult_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [project].[Recommendation] (
        [RecommendationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [ExpectedPoints] decimal(9,2) NOT NULL,
        [QuantitativeImpactsJson] nvarchar(max) NOT NULL,
        [PriorityScore] decimal(9,4) NOT NULL,
        [Rank] int NOT NULL,
        [BackedByType] nvarchar(20) NOT NULL,
        [BackedByReferenceId] uniqueidentifier NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Recommendation] PRIMARY KEY CLUSTERED ([RecommendationId]),
        CONSTRAINT [CK_Recommendation_ExpectedPoints] CHECK ([ExpectedPoints] >= 0),
        CONSTRAINT [CK_Recommendation_PriorityScore] CHECK ([PriorityScore] >= 0 AND [PriorityScore] <= 1),
        CONSTRAINT [CK_Recommendation_Rank] CHECK ([Rank] > 0),
        CONSTRAINT [CK_Recommendation_BackedByType] CHECK ([BackedByType] IN ('simulation','calculator','rule','mixed')),
        CONSTRAINT [CK_Recommendation_QuantitativeImpactsJson] CHECK (ISJSON([QuantitativeImpactsJson]) = 1),
        CONSTRAINT [FK_Recommendation_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Recommendation_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId])
    );

    CREATE TABLE [project].[RecommendationPrerequisite] (
        [RecommendationPrerequisiteId] uniqueidentifier NOT NULL,
        [RecommendationId] uniqueidentifier NOT NULL,
        [PrerequisiteText] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_RecommendationPrerequisite] PRIMARY KEY CLUSTERED ([RecommendationPrerequisiteId]),
        CONSTRAINT [FK_RecommendationPrerequisite_Recommendation] FOREIGN KEY ([RecommendationId]) REFERENCES [project].[Recommendation]([RecommendationId])
    );

    CREATE TABLE [project].[RecommendationTradeoff] (
        [RecommendationTradeoffId] uniqueidentifier NOT NULL,
        [RecommendationId] uniqueidentifier NOT NULL,
        [TradeoffText] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_RecommendationTradeoff] PRIMARY KEY CLUSTERED ([RecommendationTradeoffId]),
        CONSTRAINT [FK_RecommendationTradeoff_Recommendation] FOREIGN KEY ([RecommendationId]) REFERENCES [project].[Recommendation]([RecommendationId])
    );

    CREATE TABLE [project].[RecommendationEvidence] (
        [RecommendationEvidenceId] uniqueidentifier NOT NULL,
        [RecommendationId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RecommendationEvidence] PRIMARY KEY CLUSTERED ([RecommendationEvidenceId]),
        CONSTRAINT [UQ_RecommendationEvidence] UNIQUE ([RecommendationId],[ArtifactId]),
        CONSTRAINT [FK_RecommendationEvidence_Recommendation] FOREIGN KEY ([RecommendationId]) REFERENCES [project].[Recommendation]([RecommendationId]),
        CONSTRAINT [FK_RecommendationEvidence_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [content].[DocumentArtifact] (
        [DocumentArtifactId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(30) NOT NULL,
        [Format] nvarchar(10) NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [TemplateVersion] nvarchar(30) NOT NULL,
        [BrandingApplied] bit NOT NULL,
        [WatermarkApplied] bit NOT NULL,
        [FooterVersionText] nvarchar(100) NOT NULL,
        [StorageUri] nvarchar(1000) NOT NULL,
        [ChecksumSha256] char(64) NOT NULL,
        [ReviewStatus] nvarchar(20) NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_DocumentArtifact] PRIMARY KEY CLUSTERED ([DocumentArtifactId]),
        CONSTRAINT [CK_DocumentArtifact_DocumentType] CHECK ([DocumentType] IN ('narrative','calculator','simulationSummary','formReadyData','scorecard','checklist','report','package','portfolioExport')),
        CONSTRAINT [CK_DocumentArtifact_Format] CHECK ([Format] IN ('pdf','docx','xlsx','json','pptx')),
        CONSTRAINT [CK_DocumentArtifact_ReviewStatus] CHECK ([ReviewStatus] IN ('draft','inReview','approved','rejected')),
        CONSTRAINT [CK_DocumentArtifact_ChecksumHex] CHECK ([ChecksumSha256] NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT [FK_DocumentArtifact_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_DocumentArtifact_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_DocumentArtifact_Template] FOREIGN KEY ([TemplateId]) REFERENCES [content].[Template]([TemplateId]),
        CONSTRAINT [FK_DocumentArtifact_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[RecommendationBackingAnalysis] (
        [RecommendationBackingAnalysisId] uniqueidentifier NOT NULL,
        [RecommendationId] uniqueidentifier NOT NULL,
        [AnalysisType] nvarchar(20) NOT NULL,
        [SimulationJobId] uniqueidentifier NULL,
        [DocumentArtifactId] uniqueidentifier NULL,
        [CarbonResultId] uniqueidentifier NULL,
        [Title] nvarchar(300) NOT NULL,
        [QuantitativeImpactsJson] nvarchar(max) NOT NULL,
        [RuntimeBudgetSeconds] int NULL,
        [RuntimeReferenceText] nvarchar(1000) NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_RecommendationBackingAnalysis] PRIMARY KEY CLUSTERED ([RecommendationBackingAnalysisId]),
        CONSTRAINT [CK_RecommendationBackingAnalysis_AnalysisType] CHECK ([AnalysisType] IN ('simulation','calculator','carbonResult')),
        CONSTRAINT [CK_RecommendationBackingAnalysis_QuantitativeImpactsJson] CHECK (ISJSON([QuantitativeImpactsJson]) = 1),
        CONSTRAINT [CK_RecommendationBackingAnalysis_OneReference] CHECK ((CASE WHEN [SimulationJobId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [DocumentArtifactId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [CarbonResultId] IS NOT NULL THEN 1 ELSE 0 END) = 1),
        CONSTRAINT [CK_RecommendationBackingAnalysis_SortOrder] CHECK ([SortOrder] > 0),
        CONSTRAINT [CK_RecommendationBackingAnalysis_RuntimeBudgetSeconds] CHECK ([RuntimeBudgetSeconds] IS NULL OR [RuntimeBudgetSeconds] > 0),
        CONSTRAINT [FK_RecommendationBackingAnalysis_Recommendation] FOREIGN KEY ([RecommendationId]) REFERENCES [project].[Recommendation]([RecommendationId]),
        CONSTRAINT [FK_RecommendationBackingAnalysis_SimulationJob] FOREIGN KEY ([SimulationJobId]) REFERENCES [project].[SimulationJob]([SimulationJobId]),
        CONSTRAINT [FK_RecommendationBackingAnalysis_DocumentArtifact] FOREIGN KEY ([DocumentArtifactId]) REFERENCES [content].[DocumentArtifact]([DocumentArtifactId]),
        CONSTRAINT [FK_RecommendationBackingAnalysis_CarbonResult] FOREIGN KEY ([CarbonResultId]) REFERENCES [project].[CarbonResult]([CarbonResultId])
    );

    CREATE TABLE [project].[SimulationJobInputArtifact] (
        [SimulationJobInputArtifactId] uniqueidentifier NOT NULL,
        [SimulationJobId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SimulationJobInputArtifact] PRIMARY KEY CLUSTERED ([SimulationJobInputArtifactId]),
        CONSTRAINT [UQ_SimulationJobInputArtifact] UNIQUE ([SimulationJobId],[ArtifactId]),
        CONSTRAINT [FK_SimulationJobInputArtifact_Job] FOREIGN KEY ([SimulationJobId]) REFERENCES [project].[SimulationJob]([SimulationJobId]),
        CONSTRAINT [FK_SimulationJobInputArtifact_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [project].[SimulationJobOutputArtifact] (
        [SimulationJobOutputArtifactId] uniqueidentifier NOT NULL,
        [SimulationJobId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SimulationJobOutputArtifact] PRIMARY KEY CLUSTERED ([SimulationJobOutputArtifactId]),
        CONSTRAINT [UQ_SimulationJobOutputArtifact] UNIQUE ([SimulationJobId],[ArtifactId]),
        CONSTRAINT [FK_SimulationJobOutputArtifact_Job] FOREIGN KEY ([SimulationJobId]) REFERENCES [project].[SimulationJob]([SimulationJobId]),
        CONSTRAINT [FK_SimulationJobOutputArtifact_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [content].[ExtractionResult] (
        [ExtractionResultId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        [ExtractionType] nvarchar(20) NOT NULL,
        [Confidence] decimal(5,4) NOT NULL,
        [RawText] nvarchar(max) NULL,
        [ReviewerStatus] nvarchar(20) NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2(3) NULL,
        [ReviewComments] nvarchar(2000) NULL,
        [ProvenanceJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ExtractionResult] PRIMARY KEY CLUSTERED ([ExtractionResultId]),
        CONSTRAINT [CK_ExtractionResult_Type] CHECK ([ExtractionType] IN ('ocr','table','keyValue','bimSpatial')),
        CONSTRAINT [CK_ExtractionResult_Confidence] CHECK ([Confidence] >= 0 AND [Confidence] <= 1),
        CONSTRAINT [CK_ExtractionResult_ReviewerStatus] CHECK ([ReviewerStatus] IN ('pending','approved','rejected','corrected')),
        CONSTRAINT [CK_ExtractionResult_ProvenanceJson] CHECK (ISJSON([ProvenanceJson]) = 1),
        CONSTRAINT [FK_ExtractionResult_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ExtractionResult_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId]),
        CONSTRAINT [FK_ExtractionResult_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [content].[ExtractedField] (
        [ExtractedFieldId] uniqueidentifier NOT NULL,
        [ExtractionResultId] uniqueidentifier NOT NULL,
        [FieldName] nvarchar(200) NOT NULL,
        [FieldType] nvarchar(20) NOT NULL,
        [FieldValueJson] nvarchar(max) NOT NULL,
        [Confidence] decimal(5,4) NOT NULL,
        [BoundingBoxJson] nvarchar(max) NULL,
        [SourcePage] int NULL,
        [Corrected] bit NOT NULL,
        CONSTRAINT [PK_ExtractedField] PRIMARY KEY CLUSTERED ([ExtractedFieldId]),
        CONSTRAINT [CK_ExtractedField_FieldType] CHECK ([FieldType] IN ('string','number','boolean','date','array','object')),
        CONSTRAINT [CK_ExtractedField_FieldValueJson] CHECK (ISJSON([FieldValueJson]) = 1),
        CONSTRAINT [CK_ExtractedField_Confidence] CHECK ([Confidence] >= 0 AND [Confidence] <= 1),
        CONSTRAINT [CK_ExtractedField_BoundingBoxJson] CHECK ([BoundingBoxJson] IS NULL OR ISJSON([BoundingBoxJson]) = 1),
        CONSTRAINT [CK_ExtractedField_SourcePage] CHECK ([SourcePage] IS NULL OR [SourcePage] > 0),
        CONSTRAINT [FK_ExtractedField_ExtractionResult] FOREIGN KEY ([ExtractionResultId]) REFERENCES [content].[ExtractionResult]([ExtractionResultId])
    );

    CREATE TABLE [content].[ClassificationResult] (
        [ClassificationResultId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Method] nvarchar(20) NOT NULL,
        [PrecisionEstimate] decimal(5,4) NULL,
        [RecallEstimate] decimal(5,4) NULL,
        [ReviewerStatus] nvarchar(20) NOT NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedAt] datetime2(3) NULL,
        [Rationale] nvarchar(max) NOT NULL,
        [NoTrainingUsed] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ClassificationResult] PRIMARY KEY CLUSTERED ([ClassificationResultId]),
        CONSTRAINT [UQ_ClassificationResult_Artifact] UNIQUE ([ArtifactId]),
        CONSTRAINT [CK_ClassificationResult_Method] CHECK ([Method] IN ('rules','embeddings','hybrid')),
        CONSTRAINT [CK_ClassificationResult_PrecisionEstimate] CHECK ([PrecisionEstimate] IS NULL OR ([PrecisionEstimate] >= 0 AND [PrecisionEstimate] <= 1)),
        CONSTRAINT [CK_ClassificationResult_RecallEstimate] CHECK ([RecallEstimate] IS NULL OR ([RecallEstimate] >= 0 AND [RecallEstimate] <= 1)),
        CONSTRAINT [CK_ClassificationResult_ReviewerStatus] CHECK ([ReviewerStatus] IN ('pending','approved','corrected','rejected')),
        CONSTRAINT [FK_ClassificationResult_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_ClassificationResult_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId]),
        CONSTRAINT [FK_ClassificationResult_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_ClassificationResult_ReviewedBy] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [content].[ClassificationSuggestedCredit] (
        [ClassificationSuggestedCreditId] uniqueidentifier NOT NULL,
        [ClassificationResultId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ClassificationSuggestedCredit] PRIMARY KEY CLUSTERED ([ClassificationSuggestedCreditId]),
        CONSTRAINT [UQ_ClassificationSuggestedCredit] UNIQUE ([ClassificationResultId],[CreditId]),
        CONSTRAINT [FK_ClassificationSuggestedCredit_Result] FOREIGN KEY ([ClassificationResultId]) REFERENCES [content].[ClassificationResult]([ClassificationResultId]),
        CONSTRAINT [FK_ClassificationSuggestedCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [content].[ClassificationFinalCredit] (
        [ClassificationFinalCreditId] uniqueidentifier NOT NULL,
        [ClassificationResultId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ClassificationFinalCredit] PRIMARY KEY CLUSTERED ([ClassificationFinalCreditId]),
        CONSTRAINT [UQ_ClassificationFinalCredit] UNIQUE ([ClassificationResultId],[CreditId]),
        CONSTRAINT [FK_ClassificationFinalCredit_Result] FOREIGN KEY ([ClassificationResultId]) REFERENCES [content].[ClassificationResult]([ClassificationResultId]),
        CONSTRAINT [FK_ClassificationFinalCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[ScoreOverrideAttachment] (
        [ScoreOverrideAttachmentId] uniqueidentifier NOT NULL,
        [ScoreOverrideId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ScoreOverrideAttachment] PRIMARY KEY CLUSTERED ([ScoreOverrideAttachmentId]),
        CONSTRAINT [UQ_ScoreOverrideAttachment] UNIQUE ([ScoreOverrideId],[ArtifactId]),
        CONSTRAINT [FK_ScoreOverrideAttachment_Override] FOREIGN KEY ([ScoreOverrideId]) REFERENCES [project].[ScoreOverride]([ScoreOverrideId]),
        CONSTRAINT [FK_ScoreOverrideAttachment_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [content].[ArtifactTag] (
        [ArtifactTagId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        [Tag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ArtifactTag] PRIMARY KEY CLUSTERED ([ArtifactTagId]),
        CONSTRAINT [UQ_ArtifactTag] UNIQUE ([ArtifactId],[Tag]),
        CONSTRAINT [FK_ArtifactTag_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [content].[TemplateVersion] (
        [TemplateVersionId] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [TemplateVersion] nvarchar(30) NOT NULL,
        [StorageUri] nvarchar(1000) NOT NULL,
        [ChecksumSha256] char(64) NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_TemplateVersion] PRIMARY KEY CLUSTERED ([TemplateVersionId]),
        CONSTRAINT [UQ_TemplateVersion_Template_Version] UNIQUE ([TemplateId],[TemplateVersion]),
        CONSTRAINT [FK_TemplateVersion_Template] FOREIGN KEY ([TemplateId]) REFERENCES [content].[Template]([TemplateId]),
        CONSTRAINT [FK_TemplateVersion_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [content].[DocumentSource] (
        [DocumentSourceId] uniqueidentifier NOT NULL,
        [DocumentArtifactId] uniqueidentifier NOT NULL,
        [SourceEntityType] nvarchar(30) NOT NULL,
        [SourceEntityId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DocumentSource] PRIMARY KEY CLUSTERED ([DocumentSourceId]),
        CONSTRAINT [CK_DocumentSource_SourceEntityType] CHECK ([SourceEntityType] IN ('artifact','simulationJob','scorecard','preAssessmentRun','auditorQuery','recommendation')),
        CONSTRAINT [FK_DocumentSource_DocumentArtifact] FOREIGN KEY ([DocumentArtifactId]) REFERENCES [content].[DocumentArtifact]([DocumentArtifactId])
    );

    CREATE TABLE [content].[SubmissionPackage] (
        [SubmissionPackageId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [PackageVersion] nvarchar(10) NOT NULL,
        [NamingConvention] nvarchar(200) NOT NULL,
        [ImmutableHistorySequence] bigint NOT NULL,
        [BuildStatus] nvarchar(20) NOT NULL,
        [ApprovalStatus] nvarchar(20) NOT NULL,
        [RequiredApprovalCount] int NOT NULL,
        [ApprovalCount] int NOT NULL,
        [SegregationOfDutiesPassed] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SubmissionPackage] PRIMARY KEY CLUSTERED ([SubmissionPackageId]),
        CONSTRAINT [UQ_SubmissionPackage_Project_PackageVersion] UNIQUE ([ProjectId],[PackageVersion]),
        CONSTRAINT [CK_SubmissionPackage_BuildStatus] CHECK ([BuildStatus] IN ('building','built','finalized')),
        CONSTRAINT [CK_SubmissionPackage_ApprovalStatus] CHECK ([ApprovalStatus] IN ('notRequested','pending','approved','rejected')),
        CONSTRAINT [CK_SubmissionPackage_RequiredApprovalCount] CHECK ([RequiredApprovalCount] >= 0),
        CONSTRAINT [CK_SubmissionPackage_ApprovalCount] CHECK ([ApprovalCount] >= 0 AND [ApprovalCount] <= [RequiredApprovalCount]),
        CONSTRAINT [CK_SubmissionPackage_ImmutableHistorySequence] CHECK ([ImmutableHistorySequence] > 0),
        CONSTRAINT [CK_SubmissionPackage_NamingConvention] CHECK ([NamingConvention] = 'ProjectCode_CreditID_DocType_vX.Y'),
        CONSTRAINT [CK_SubmissionPackage_PackageVersionPattern] CHECK ([PackageVersion] IN ('1.0','1.1','2.0')),
        CONSTRAINT [FK_SubmissionPackage_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_SubmissionPackage_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId])
    );

    CREATE TABLE [content].[SubmissionPackageArtifact] (
        [SubmissionPackageArtifactId] uniqueidentifier NOT NULL,
        [SubmissionPackageId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NULL,
        [DocumentArtifactId] uniqueidentifier NULL,
        [FileName] nvarchar(500) NOT NULL,
        [ChecksumSha256] char(64) NOT NULL,
        CONSTRAINT [PK_SubmissionPackageArtifact] PRIMARY KEY CLUSTERED ([SubmissionPackageArtifactId]),
        CONSTRAINT [CK_SubmissionPackageArtifact_OneSource] CHECK ((CASE WHEN [ArtifactId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [DocumentArtifactId] IS NOT NULL THEN 1 ELSE 0 END) = 1),
        CONSTRAINT [FK_SubmissionPackageArtifact_Package] FOREIGN KEY ([SubmissionPackageId]) REFERENCES [content].[SubmissionPackage]([SubmissionPackageId]),
        CONSTRAINT [FK_SubmissionPackageArtifact_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId]),
        CONSTRAINT [FK_SubmissionPackageArtifact_DocumentArtifact] FOREIGN KEY ([DocumentArtifactId]) REFERENCES [content].[DocumentArtifact]([DocumentArtifactId])
    );

    CREATE TABLE [content].[SubmissionTemplatePin] (
        [SubmissionTemplatePinId] uniqueidentifier NOT NULL,
        [SubmissionPackageId] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [TemplateVersion] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_SubmissionTemplatePin] PRIMARY KEY CLUSTERED ([SubmissionTemplatePinId]),
        CONSTRAINT [UQ_SubmissionTemplatePin] UNIQUE ([SubmissionPackageId],[TemplateId],[TemplateVersion]),
        CONSTRAINT [FK_SubmissionTemplatePin_Package] FOREIGN KEY ([SubmissionPackageId]) REFERENCES [content].[SubmissionPackage]([SubmissionPackageId]),
        CONSTRAINT [FK_SubmissionTemplatePin_Template] FOREIGN KEY ([TemplateId]) REFERENCES [content].[Template]([TemplateId])
    );

    CREATE TABLE [project].[AuditorQuery] (
        [AuditorQueryId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [State] nvarchar(30) NOT NULL,
        [FirstResponseDueAt] datetime2(3) NOT NULL,
        [FirstResponseSentAt] datetime2(3) NULL,
        [CannedResponseId] uniqueidentifier NULL,
        [ThreadContextJson] nvarchar(max) NOT NULL,
        [SlaStatus] nvarchar(20) NOT NULL,
        [UsedAISuggestion] bit NOT NULL,
        [HumanApprovalRecorded] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AuditorQuery] PRIMARY KEY CLUSTERED ([AuditorQueryId]),
        CONSTRAINT [CK_AuditorQuery_State] CHECK ([State] IN ('QueryReceived','Assigned','DraftResponse','InternalReview','Approved','SentShared','AuditorFollowUp','Resolved','Reopened')),
        CONSTRAINT [CK_AuditorQuery_SlaStatus] CHECK ([SlaStatus] IN ('onTrack','atRisk','breached')),
        CONSTRAINT [CK_AuditorQuery_ThreadContextJson] CHECK (ISJSON([ThreadContextJson]) = 1),
        CONSTRAINT [FK_AuditorQuery_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AuditorQuery_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_AuditorQuery_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_AuditorQuery_CannedResponse] FOREIGN KEY ([CannedResponseId]) REFERENCES [content].[CannedResponse]([CannedResponseId])
    );

    CREATE TABLE [project].[AuditorClaim] (
        [AuditorClaimId] uniqueidentifier NOT NULL,
        [AuditorQueryId] uniqueidentifier NOT NULL,
        [ClaimText] nvarchar(max) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_AuditorClaim] PRIMARY KEY CLUSTERED ([AuditorClaimId]),
        CONSTRAINT [FK_AuditorClaim_Query] FOREIGN KEY ([AuditorQueryId]) REFERENCES [project].[AuditorQuery]([AuditorQueryId]),
        CONSTRAINT [FK_AuditorClaim_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[AuditorClaimEvidence] (
        [AuditorClaimEvidenceId] uniqueidentifier NOT NULL,
        [AuditorClaimId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AuditorClaimEvidence] PRIMARY KEY CLUSTERED ([AuditorClaimEvidenceId]),
        CONSTRAINT [UQ_AuditorClaimEvidence] UNIQUE ([AuditorClaimId],[ArtifactId]),
        CONSTRAINT [FK_AuditorClaimEvidence_Claim] FOREIGN KEY ([AuditorClaimId]) REFERENCES [project].[AuditorClaim]([AuditorClaimId]),
        CONSTRAINT [FK_AuditorClaimEvidence_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [project].[AuditorQueryEvidence] (
        [AuditorQueryEvidenceId] uniqueidentifier NOT NULL,
        [AuditorQueryId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AuditorQueryEvidence] PRIMARY KEY CLUSTERED ([AuditorQueryEvidenceId]),
        CONSTRAINT [UQ_AuditorQueryEvidence] UNIQUE ([AuditorQueryId],[ArtifactId]),
        CONSTRAINT [FK_AuditorQueryEvidence_Query] FOREIGN KEY ([AuditorQueryId]) REFERENCES [project].[AuditorQuery]([AuditorQueryId]),
        CONSTRAINT [FK_AuditorQueryEvidence_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [project].[Anomaly] (
        [AnomalyId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [RuleId] nvarchar(100) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [FalsePositiveFlag] bit NOT NULL,
        [AuditJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Anomaly] PRIMARY KEY CLUSTERED ([AnomalyId]),
        CONSTRAINT [CK_Anomaly_Severity] CHECK ([Severity] IN ('low','medium','high','critical')),
        CONSTRAINT [CK_Anomaly_Status] CHECK ([Status] IN ('open','investigating','needsEvidenceFix','needsOverride','falsePositive','remediated','closed')),
        CONSTRAINT [CK_Anomaly_AuditJson] CHECK (ISJSON([AuditJson]) = 1),
        CONSTRAINT [FK_Anomaly_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Anomaly_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId])
    );

    CREATE TABLE [project].[AnomalyCredit] (
        [AnomalyCreditId] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnomalyCredit] PRIMARY KEY CLUSTERED ([AnomalyCreditId]),
        CONSTRAINT [UQ_AnomalyCredit] UNIQUE ([AnomalyId],[CreditId]),
        CONSTRAINT [FK_AnomalyCredit_Anomaly] FOREIGN KEY ([AnomalyId]) REFERENCES [project].[Anomaly]([AnomalyId]),
        CONSTRAINT [FK_AnomalyCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [project].[AnomalyRemediationTask] (
        [AnomalyRemediationTaskId] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [EvidenceTaskId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnomalyRemediationTask] PRIMARY KEY CLUSTERED ([AnomalyRemediationTaskId]),
        CONSTRAINT [UQ_AnomalyRemediationTask] UNIQUE ([AnomalyId],[EvidenceTaskId]),
        CONSTRAINT [FK_AnomalyRemediationTask_Anomaly] FOREIGN KEY ([AnomalyId]) REFERENCES [project].[Anomaly]([AnomalyId]),
        CONSTRAINT [FK_AnomalyRemediationTask_EvidenceTask] FOREIGN KEY ([EvidenceTaskId]) REFERENCES [project].[EvidenceTask]([EvidenceTaskId])
    );

    CREATE TABLE [project].[AnomalyLinkedOverride] (
        [AnomalyLinkedOverrideId] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [ScoreOverrideId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnomalyLinkedOverride] PRIMARY KEY CLUSTERED ([AnomalyLinkedOverrideId]),
        CONSTRAINT [UQ_AnomalyLinkedOverride] UNIQUE ([AnomalyId],[ScoreOverrideId]),
        CONSTRAINT [FK_AnomalyLinkedOverride_Anomaly] FOREIGN KEY ([AnomalyId]) REFERENCES [project].[Anomaly]([AnomalyId]),
        CONSTRAINT [FK_AnomalyLinkedOverride_ScoreOverride] FOREIGN KEY ([ScoreOverrideId]) REFERENCES [project].[ScoreOverride]([ScoreOverrideId])
    );

    CREATE TABLE [project].[CorrectiveAction] (
        [CorrectiveActionId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ExpectedPointImpact] decimal(9,2) NOT NULL,
        [ProposedByType] nvarchar(20) NOT NULL,
        [ProposedById] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [LinkedEvidenceTaskId] uniqueidentifier NULL,
        [LinkedOverrideId] uniqueidentifier NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CorrectiveAction] PRIMARY KEY CLUSTERED ([CorrectiveActionId]),
        CONSTRAINT [CK_CorrectiveAction_ExpectedPointImpact] CHECK ([ExpectedPointImpact] >= 0),
        CONSTRAINT [CK_CorrectiveAction_ProposedByType] CHECK ([ProposedByType] IN ('system','user')),
        CONSTRAINT [CK_CorrectiveAction_Status] CHECK ([Status] IN ('proposed','accepted','rejected','applied')),
        CONSTRAINT [FK_CorrectiveAction_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_CorrectiveAction_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_CorrectiveAction_Anomaly] FOREIGN KEY ([AnomalyId]) REFERENCES [project].[Anomaly]([AnomalyId]),
        CONSTRAINT [FK_CorrectiveAction_ProposedBy] FOREIGN KEY ([ProposedById]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_CorrectiveAction_LinkedEvidenceTask] FOREIGN KEY ([LinkedEvidenceTaskId]) REFERENCES [project].[EvidenceTask]([EvidenceTaskId]),
        CONSTRAINT [FK_CorrectiveAction_LinkedOverride] FOREIGN KEY ([LinkedOverrideId]) REFERENCES [project].[ScoreOverride]([ScoreOverrideId])
    );

    CREATE TABLE [project].[AuditorQueryTransition] (
        [AuditorQueryTransitionId] uniqueidentifier NOT NULL,
        [AuditorQueryId] uniqueidentifier NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [FromState] nvarchar(20) NULL,
        [ToState] nvarchar(20) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Message] nvarchar(2000) NULL,
        [AIApprovalFlag] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_AuditorQueryTransition] PRIMARY KEY CLUSTERED ([AuditorQueryTransitionId]),
        CONSTRAINT [CK_AuditorQueryTransition_Action] CHECK ([Action] IN ('submitForReview','approve','send','close','reopen')),
        CONSTRAINT [FK_AuditorQueryTransition_AuditorQuery] FOREIGN KEY ([AuditorQueryId]) REFERENCES [project].[AuditorQuery]([AuditorQueryId]),
        CONSTRAINT [FK_AuditorQueryTransition_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [project].[AnomalyProposedAction] (
        [AnomalyProposedActionId] uniqueidentifier NOT NULL,
        [AnomalyId] uniqueidentifier NOT NULL,
        [CorrectiveActionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnomalyProposedAction] PRIMARY KEY CLUSTERED ([AnomalyProposedActionId]),
        CONSTRAINT [UQ_AnomalyProposedAction] UNIQUE ([AnomalyId],[CorrectiveActionId]),
        CONSTRAINT [FK_AnomalyProposedAction_Anomaly] FOREIGN KEY ([AnomalyId]) REFERENCES [project].[Anomaly]([AnomalyId]),
        CONSTRAINT [FK_AnomalyProposedAction_CorrectiveAction] FOREIGN KEY ([CorrectiveActionId]) REFERENCES [project].[CorrectiveAction]([CorrectiveActionId])
    );

    CREATE TABLE [project].[CorrectiveActionCitation] (
        [CorrectiveActionCitationId] uniqueidentifier NOT NULL,
        [CorrectiveActionId] uniqueidentifier NOT NULL,
        [SourceType] nvarchar(30) NOT NULL,
        [SourceId] nvarchar(100) NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Excerpt] nvarchar(max) NULL,
        [VersionIdentifier] nvarchar(100) NULL,
        [LicenseStatus] nvarchar(30) NULL,
        CONSTRAINT [PK_CorrectiveActionCitation] PRIMARY KEY CLUSTERED ([CorrectiveActionCitationId]),
        CONSTRAINT [CK_CorrectiveActionCitation_SourceType] CHECK ([SourceType] IN ('ratingRule','addendum','standard','evidence','licensedCorpus')),
        CONSTRAINT [CK_CorrectiveActionCitation_LicenseStatus] CHECK ([LicenseStatus] IS NULL OR [LicenseStatus] IN ('licensed','permittedPublic','internal')),
        CONSTRAINT [FK_CorrectiveActionCitation_CorrectiveAction] FOREIGN KEY ([CorrectiveActionId]) REFERENCES [project].[CorrectiveAction]([CorrectiveActionId])
    );

    CREATE TABLE [project].[EvidenceTaskTransition] (
        [EvidenceTaskTransitionId] uniqueidentifier NOT NULL,
        [EvidenceTaskId] uniqueidentifier NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [PreviousOwnerUserId] uniqueidentifier NULL,
        [NewOwnerUserId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Message] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_EvidenceTaskTransition] PRIMARY KEY CLUSTERED ([EvidenceTaskTransitionId]),
        CONSTRAINT [FK_EvidenceTaskTransition_EvidenceTask] FOREIGN KEY ([EvidenceTaskId]) REFERENCES [project].[EvidenceTask]([EvidenceTaskId]),
        CONSTRAINT [FK_EvidenceTaskTransition_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_EvidenceTaskTransition_PreviousOwnerUser] FOREIGN KEY ([PreviousOwnerUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_EvidenceTaskTransition_NewOwnerUser] FOREIGN KEY ([NewOwnerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [content].[CannedResponseTag] (
        [CannedResponseTagId] uniqueidentifier NOT NULL,
        [CannedResponseId] uniqueidentifier NOT NULL,
        [Tag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CannedResponseTag] PRIMARY KEY CLUSTERED ([CannedResponseTagId]),
        CONSTRAINT [UQ_CannedResponseTag] UNIQUE ([CannedResponseId],[Tag]),
        CONSTRAINT [FK_CannedResponseTag_CannedResponse] FOREIGN KEY ([CannedResponseId]) REFERENCES [content].[CannedResponse]([CannedResponseId])
    );

    CREATE TABLE [content].[DocumentReviewEvent] (
        [DocumentReviewEventId] uniqueidentifier NOT NULL,
        [DocumentArtifactId] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Comments] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_DocumentReviewEvent] PRIMARY KEY CLUSTERED ([DocumentReviewEventId]),
        CONSTRAINT [CK_DocumentReviewEvent_Action] CHECK ([Action] IN ('submit','startReview','approve','reject')),
        CONSTRAINT [FK_DocumentReviewEvent_DocumentArtifact] FOREIGN KEY ([DocumentArtifactId]) REFERENCES [content].[DocumentArtifact]([DocumentArtifactId]),
        CONSTRAINT [FK_DocumentReviewEvent_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [content].[ExtractionReviewEvent] (
        [ExtractionReviewEventId] uniqueidentifier NOT NULL,
        [ExtractionResultId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [Comments] nvarchar(2000) NULL,
        [BeforeSnapshotJson] nvarchar(max) NULL,
        [AfterSnapshotJson] nvarchar(max) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_ExtractionReviewEvent] PRIMARY KEY CLUSTERED ([ExtractionReviewEventId]),
        CONSTRAINT [CK_ExtractionReviewEvent_Action] CHECK ([Action] IN ('approve','reject','correct')),
        CONSTRAINT [CK_ExtractionReviewEvent_BeforeSnapshotJson] CHECK ([BeforeSnapshotJson] IS NULL OR ISJSON([BeforeSnapshotJson]) = 1),
        CONSTRAINT [CK_ExtractionReviewEvent_AfterSnapshotJson] CHECK ([AfterSnapshotJson] IS NULL OR ISJSON([AfterSnapshotJson]) = 1),
        CONSTRAINT [FK_ExtractionReviewEvent_ExtractionResult] FOREIGN KEY ([ExtractionResultId]) REFERENCES [content].[ExtractionResult]([ExtractionResultId]),
        CONSTRAINT [FK_ExtractionReviewEvent_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [integration].[MappingFieldMap] (
        [MappingFieldMapId] uniqueidentifier NOT NULL,
        [MappingDefinitionId] uniqueidentifier NOT NULL,
        [SourceFieldId] nvarchar(200) NOT NULL,
        [TargetFieldId] nvarchar(200) NOT NULL,
        [TransformExpression] nvarchar(1000) NULL,
        CONSTRAINT [PK_MappingFieldMap] PRIMARY KEY CLUSTERED ([MappingFieldMapId]),
        CONSTRAINT [UQ_MappingFieldMap] UNIQUE ([MappingDefinitionId],[SourceFieldId],[TargetFieldId]),
        CONSTRAINT [FK_MappingFieldMap_MappingDefinition] FOREIGN KEY ([MappingDefinitionId]) REFERENCES [integration].[MappingDefinition]([MappingDefinitionId])
    );

    CREATE TABLE [integration].[IoTStream] (
        [IoTStreamId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [DeviceId] nvarchar(100) NOT NULL,
        [StreamType] nvarchar(100) NOT NULL,
        [Unit] nvarchar(20) NOT NULL,
        [SamplingInterval] nvarchar(30) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [MappingDefinitionId] uniqueidentifier NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_IoTStream] PRIMARY KEY CLUSTERED ([IoTStreamId]),
        CONSTRAINT [CK_IoTStream_Source] CHECK ([Source] IN ('import','api')),
        CONSTRAINT [FK_IoTStream_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_IoTStream_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_IoTStream_MappingDefinition] FOREIGN KEY ([MappingDefinitionId]) REFERENCES [integration].[MappingDefinition]([MappingDefinitionId])
    );

    CREATE TABLE [integration].[IoTStreamCredit] (
        [IoTStreamCreditId] uniqueidentifier NOT NULL,
        [IoTStreamId] uniqueidentifier NOT NULL,
        [CreditId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_IoTStreamCredit] PRIMARY KEY CLUSTERED ([IoTStreamCreditId]),
        CONSTRAINT [UQ_IoTStreamCredit] UNIQUE ([IoTStreamId],[CreditId]),
        CONSTRAINT [FK_IoTStreamCredit_Stream] FOREIGN KEY ([IoTStreamId]) REFERENCES [integration].[IoTStream]([IoTStreamId]),
        CONSTRAINT [FK_IoTStreamCredit_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId])
    );

    CREATE TABLE [integration].[IoTMeasurement] (
        [IoTMeasurementId] bigint IDENTITY(1,1) NOT NULL,
        [IoTStreamId] uniqueidentifier NOT NULL,
        [SampleTimestamp] datetime2(3) NOT NULL,
        [Value] decimal(18,6) NOT NULL,
        [NormalizedUnit] nvarchar(20) NULL,
        [NormalizedTimestampUtc] datetime2(3) NULL,
        CONSTRAINT [PK_IoTMeasurement] PRIMARY KEY CLUSTERED ([IoTMeasurementId]),
        CONSTRAINT [UQ_IoTMeasurement_Stream_Timestamp] UNIQUE ([IoTStreamId],[SampleTimestamp]),
        CONSTRAINT [FK_IoTMeasurement_Stream] FOREIGN KEY ([IoTStreamId]) REFERENCES [integration].[IoTStream]([IoTStreamId])
    );

    CREATE TABLE [integration].[WebhookSubscriptionEventType] (
        [WebhookSubscriptionEventTypeId] uniqueidentifier NOT NULL,
        [WebhookSubscriptionId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WebhookSubscriptionEventType] PRIMARY KEY CLUSTERED ([WebhookSubscriptionEventTypeId]),
        CONSTRAINT [UQ_WebhookSubscriptionEventType] UNIQUE ([WebhookSubscriptionId],[EventType]),
        CONSTRAINT [FK_WebhookSubscriptionEventType_Subscription] FOREIGN KEY ([WebhookSubscriptionId]) REFERENCES [integration].[WebhookSubscription]([WebhookSubscriptionId])
    );

    CREATE TABLE [integration].[WebhookDelivery] (
        [WebhookDeliveryId] uniqueidentifier NOT NULL,
        [WebhookSubscriptionId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [DeliveryKind] nvarchar(20) NOT NULL,
        [DeliveryStatus] nvarchar(20) NOT NULL,
        [AttemptNumber] int NOT NULL,
        [HttpStatusCode] int NULL,
        [RequestHeadersJson] nvarchar(max) NULL,
        [RequestBodyJson] nvarchar(max) NULL,
        [PayloadHash] char(64) NULL,
        [SignatureHeader] nvarchar(500) NULL,
        [SignatureAlgorithm] nvarchar(50) NULL,
        [SentAt] datetime2(3) NULL,
        [ResponseReceivedAt] datetime2(3) NULL,
        [FailureReason] nvarchar(2000) NULL,
        [ExternalDeliveryId] nvarchar(200) NULL,
        [CorrelationId] nvarchar(100) NULL,
        [ResourceType] nvarchar(50) NULL,
        [ResourceId] uniqueidentifier NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_WebhookDelivery] PRIMARY KEY CLUSTERED ([WebhookDeliveryId]),
        CONSTRAINT [CK_WebhookDelivery_DeliveryKind] CHECK ([DeliveryKind] IN ('test','event')),
        CONSTRAINT [CK_WebhookDelivery_DeliveryStatus] CHECK ([DeliveryStatus] IN ('queued','sent','delivered','failed','deadLettered')),
        CONSTRAINT [CK_WebhookDelivery_AttemptNumber] CHECK ([AttemptNumber] > 0),
        CONSTRAINT [CK_WebhookDelivery_RequestHeadersJson] CHECK ([RequestHeadersJson] IS NULL OR ISJSON([RequestHeadersJson]) = 1),
        CONSTRAINT [CK_WebhookDelivery_RequestBodyJson] CHECK ([RequestBodyJson] IS NULL OR ISJSON([RequestBodyJson]) = 1),
        CONSTRAINT [CK_WebhookDelivery_PayloadSource] CHECK ([RequestBodyJson] IS NOT NULL OR [PayloadHash] IS NOT NULL),
        CONSTRAINT [CK_WebhookDelivery_ResponseTiming] CHECK ([SentAt] IS NULL OR [ResponseReceivedAt] IS NULL OR [SentAt] <= [ResponseReceivedAt]),
        CONSTRAINT [CK_WebhookDelivery_DeliveryKind_Status] CHECK ([DeliveryKind] IN ('test','event') AND [DeliveryStatus] IN ('queued','sent','delivered','failed','deadLettered')),
        CONSTRAINT [FK_WebhookDelivery_Subscription] FOREIGN KEY ([WebhookSubscriptionId]) REFERENCES [integration].[WebhookSubscription]([WebhookSubscriptionId]),
        CONSTRAINT [FK_WebhookDelivery_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [integration].[ProviderCredentialRotationEvent] (
        [ProviderCredentialRotationEventId] uniqueidentifier NOT NULL,
        [ProviderCredentialId] uniqueidentifier NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [FromStatus] nvarchar(20) NULL,
        [ToStatus] nvarchar(20) NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_ProviderCredentialRotationEvent] PRIMARY KEY CLUSTERED ([ProviderCredentialRotationEventId]),
        CONSTRAINT [CK_ProviderCredentialRotationEvent_Action] CHECK ([Action] IN ('create','requestRotation','approveRotation','completeRotation','retire','disable')),
        CONSTRAINT [FK_ProviderCredentialRotationEvent_ProviderCredential] FOREIGN KEY ([ProviderCredentialId]) REFERENCES [integration].[ProviderCredential]([ProviderCredentialId]),
        CONSTRAINT [FK_ProviderCredentialRotationEvent_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[Notification] (
        [NotificationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Channel] nvarchar(20) NOT NULL,
        [RecipientUserId] uniqueidentifier NULL,
        [RecipientAddress] nvarchar(320) NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [RuleId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [QuietHoursApplied] bit NOT NULL,
        [ConsentVerified] bit NOT NULL,
        [ProviderCode] nvarchar(50) NULL,
        [SuppressedReason] nvarchar(200) NULL,
        [ExternalMessageId] nvarchar(200) NULL,
        [DeliveredAt] datetime2(3) NULL,
        [AcknowledgedAt] datetime2(3) NULL,
        [FailureReason] nvarchar(1000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([NotificationId]),
        CONSTRAINT [CK_Notification_Channel] CHECK ([Channel] IN ('inApp','email','whatsapp')),
        CONSTRAINT [CK_Notification_Status] CHECK ([Status] IN ('queued','suppressed','sent','delivered','failed')),
        CONSTRAINT [FK_Notification_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_Notification_RecipientUser] FOREIGN KEY ([RecipientUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_Notification_Template] FOREIGN KEY ([TemplateId]) REFERENCES [content].[Template]([TemplateId]),
        CONSTRAINT [FK_Notification_Rule] FOREIGN KEY ([RuleId]) REFERENCES [core].[NotificationRule]([NotificationRuleId])
    );

    CREATE TABLE [ops].[WhatsAppConsentTemplateScope] (
        [WhatsAppConsentTemplateScopeId] uniqueidentifier NOT NULL,
        [WhatsAppConsentId] uniqueidentifier NOT NULL,
        [TemplateCategory] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_WhatsAppConsentTemplateScope] PRIMARY KEY CLUSTERED ([WhatsAppConsentTemplateScopeId]),
        CONSTRAINT [UQ_WhatsAppConsentTemplateScope] UNIQUE ([WhatsAppConsentId],[TemplateCategory]),
        CONSTRAINT [FK_WhatsAppConsentTemplateScope_Consent] FOREIGN KEY ([WhatsAppConsentId]) REFERENCES [ops].[WhatsAppConsent]([WhatsAppConsentId])
    );

    CREATE TABLE [ops].[AIInteractionLog] (
        [AIInteractionLogId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [UseCase] nvarchar(30) NOT NULL,
        [PromptRedacted] nvarchar(max) NOT NULL,
        [ResponseRedacted] nvarchar(max) NOT NULL,
        [Rationale] nvarchar(max) NULL,
        [Confidence] decimal(5,4) NULL,
        [ModelIdentifier] nvarchar(100) NOT NULL,
        [ModelVersion] nvarchar(100) NOT NULL,
        [GatewayPolicyVersion] nvarchar(50) NOT NULL,
        [GatewayRequestId] nvarchar(100) NULL,
        [RetentionDays] int NOT NULL,
        [ImmutableHash] char(64) NOT NULL,
        [TrainingPermissionRecordId] uniqueidentifier NULL,
        [ProviderTrainingAllowed] bit NOT NULL,
        [CrossBorderProcessingAllowed] bit NOT NULL CONSTRAINT [DF_AIInteractionLog_CrossBorderProcessingAllowed] DEFAULT ((0)),
        CONSTRAINT [PK_AIInteractionLog] PRIMARY KEY CLUSTERED ([AIInteractionLogId]),
        CONSTRAINT [CK_AIInteractionLog_UseCase] CHECK ([UseCase] IN ('preAssessment','interpretation','qaAssistant','standardsQa','recommendation')),
        CONSTRAINT [CK_AIInteractionLog_Confidence] CHECK ([Confidence] IS NULL OR ([Confidence] >= 0 AND [Confidence] <= 1)),
        CONSTRAINT [CK_AIInteractionLog_RetentionDays] CHECK ([RetentionDays] > 0),
        CONSTRAINT [CK_AIInteractionLog_ProviderTrainingAllowed] CHECK ([ProviderTrainingAllowed] = 0),
        CONSTRAINT [CK_AIInteractionLog_ImmutableHashHex] CHECK ([ImmutableHash] NOT LIKE '%[^0-9A-Fa-f]%'),
        CONSTRAINT [CK_AIInteractionLog_CrossBorderProcessingAllowed] CHECK ([CrossBorderProcessingAllowed] IN (0,1)),
        CONSTRAINT [FK_AIInteractionLog_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AIInteractionLog_ActorUser] FOREIGN KEY ([ActorUserId]) REFERENCES [core].[UserAccount]([UserId]),
        CONSTRAINT [FK_AIInteractionLog_TrainingPermissionRecord] FOREIGN KEY ([TrainingPermissionRecordId]) REFERENCES [ops].[TrainingPermissionRecord]([TrainingPermissionRecordId])
    );

    CREATE TABLE [ops].[AIInteractionEvidence] (
        [AIInteractionEvidenceId] uniqueidentifier NOT NULL,
        [AIInteractionLogId] uniqueidentifier NOT NULL,
        [ArtifactId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AIInteractionEvidence] PRIMARY KEY CLUSTERED ([AIInteractionEvidenceId]),
        CONSTRAINT [UQ_AIInteractionEvidence] UNIQUE ([AIInteractionLogId],[ArtifactId]),
        CONSTRAINT [FK_AIInteractionEvidence_Log] FOREIGN KEY ([AIInteractionLogId]) REFERENCES [ops].[AIInteractionLog]([AIInteractionLogId]),
        CONSTRAINT [FK_AIInteractionEvidence_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    CREATE TABLE [ops].[AIInteractionDatasetReference] (
        [AIInteractionDatasetReferenceId] uniqueidentifier NOT NULL,
        [AIInteractionLogId] uniqueidentifier NOT NULL,
        [DatasetReference] nvarchar(300) NOT NULL,
        CONSTRAINT [PK_AIInteractionDatasetReference] PRIMARY KEY CLUSTERED ([AIInteractionDatasetReferenceId]),
        CONSTRAINT [FK_AIInteractionDatasetReference_Log] FOREIGN KEY ([AIInteractionLogId]) REFERENCES [ops].[AIInteractionLog]([AIInteractionLogId])
    );

    CREATE TABLE [ops].[RiskRegisterItem] (
        [RiskRegisterItemId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [CreditId] uniqueidentifier NULL,
        [Title] nvarchar(300) NOT NULL,
        [Category] nvarchar(30) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Likelihood] nvarchar(20) NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [MitigationPlan] nvarchar(max) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [LastReviewedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RiskRegisterItem] PRIMARY KEY CLUSTERED ([RiskRegisterItemId]),
        CONSTRAINT [CK_RiskRegisterItem_Category] CHECK ([Category] IN ('portalLimit','auditorVariability','dataQuality','standardsChange','security','operational','other')),
        CONSTRAINT [CK_RiskRegisterItem_Severity] CHECK ([Severity] IN ('low','medium','high','critical')),
        CONSTRAINT [CK_RiskRegisterItem_Likelihood] CHECK ([Likelihood] IN ('rare','unlikely','possible','likely','almostCertain')),
        CONSTRAINT [CK_RiskRegisterItem_Status] CHECK ([Status] IN ('open','monitoring','mitigated','closed')),
        CONSTRAINT [CK_RiskRegisterItem_Source] CHECK ([Source] IN ('manual','automatedMonitor')),
        CONSTRAINT [FK_RiskRegisterItem_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_RiskRegisterItem_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_RiskRegisterItem_Credit] FOREIGN KEY ([CreditId]) REFERENCES [core].[RatingCredit]([CreditId]),
        CONSTRAINT [FK_RiskRegisterItem_OwnerUser] FOREIGN KEY ([OwnerUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[RiskRegisterMonitorEvent] (
        [RiskRegisterMonitorEventId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RiskRegisterItemId] uniqueidentifier NOT NULL,
        [MonitorIdentifier] nvarchar(100) NOT NULL,
        [MonitorType] nvarchar(50) NOT NULL,
        [EventKey] nvarchar(200) NOT NULL,
        [PayloadSnapshotJson] nvarchar(max) NOT NULL,
        [ProcessingOutcome] nvarchar(20) NOT NULL,
        [OutcomeMessage] nvarchar(2000) NULL,
        [ProcessedAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_RiskRegisterMonitorEvent] PRIMARY KEY CLUSTERED ([RiskRegisterMonitorEventId]),
        CONSTRAINT [UQ_RiskRegisterMonitorEvent_Tenant_EventKey] UNIQUE ([TenantId],[EventKey]),
        CONSTRAINT [CK_RiskRegisterMonitorEvent_PayloadSnapshotJson] CHECK (ISJSON([PayloadSnapshotJson]) = 1),
        CONSTRAINT [CK_RiskRegisterMonitorEvent_ProcessingOutcome] CHECK ([ProcessingOutcome] IN ('received','processed','created','updated','duplicate','failed')),
        CONSTRAINT [CK_RiskRegisterMonitorEvent_ProcessedAt] CHECK ([ProcessedAt] IS NULL OR [CreatedAt] <= [ProcessedAt]),
        CONSTRAINT [FK_RiskRegisterMonitorEvent_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_RiskRegisterMonitorEvent_RiskRegisterItem] FOREIGN KEY ([RiskRegisterItemId]) REFERENCES [ops].[RiskRegisterItem]([RiskRegisterItemId])
    );

    CREATE TABLE [ops].[TenantExportRequest] (
        [TenantExportRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DeliveryMethod] nvarchar(20) NOT NULL,
        [DeliveryUri] nvarchar(1000) NULL,
        [ExpiresAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_TenantExportRequest] PRIMARY KEY CLUSTERED ([TenantExportRequestId]),
        CONSTRAINT [CK_TenantExportRequest_Status] CHECK ([Status] IN ('requested','preparing','ready','expired','failed')),
        CONSTRAINT [CK_TenantExportRequest_DeliveryMethod] CHECK ([DeliveryMethod] IN ('download','sftp')),
        CONSTRAINT [FK_TenantExportRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_TenantExportRequest_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[SecureDeletionRequest] (
        [SecureDeletionRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [ScopeType] nvarchar(20) NOT NULL,
        [ScopeId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Justification] nvarchar(max) NOT NULL,
        [ApprovalCount] int NOT NULL,
        [RequiredApprovalCount] int NOT NULL,
        [Irreversible] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SecureDeletionRequest] PRIMARY KEY CLUSTERED ([SecureDeletionRequestId]),
        CONSTRAINT [CK_SecureDeletionRequest_ScopeType] CHECK ([ScopeType] IN ('project','portfolio','tenant','object')),
        CONSTRAINT [CK_SecureDeletionRequest_Status] CHECK ([Status] IN ('pending','approved','rejected','inProgress','completed','failed')),
        CONSTRAINT [CK_SecureDeletionRequest_ApprovalCount] CHECK ([ApprovalCount] >= 0 AND [ApprovalCount] <= [RequiredApprovalCount]),
        CONSTRAINT [CK_SecureDeletionRequest_RequiredApprovalCount] CHECK ([RequiredApprovalCount] >= 1),
        CONSTRAINT [FK_SecureDeletionRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_SecureDeletionRequest_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[AuditExportRequest] (
        [AuditExportRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ApprovalCount] int NOT NULL,
        [RequiredBusinessAdminApprovals] int NOT NULL,
        [RequiredTechnicalAdminApprovals] int NOT NULL,
        [DeliveryUri] nvarchar(1000) NULL,
        [ReleasedAt] datetime2(3) NULL,
        [FromDateTime] datetime2(3) NULL,
        [ToDateTime] datetime2(3) NULL,
        [DeliveryMethod] nvarchar(20) NOT NULL,
        [SegregationOfDutiesPassed] bit NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AuditExportRequest] PRIMARY KEY CLUSTERED ([AuditExportRequestId]),
        CONSTRAINT [CK_AuditExportRequest_Status] CHECK ([Status] IN ('requested','pendingApproval','approved','rejected','released','failed')),
        CONSTRAINT [CK_AuditExportRequest_BusinessApprovals] CHECK ([RequiredBusinessAdminApprovals] >= 1),
        CONSTRAINT [CK_AuditExportRequest_TechnicalApprovals] CHECK ([RequiredTechnicalAdminApprovals] >= 1),
        CONSTRAINT [CK_AuditExportRequest_ApprovalCount] CHECK ([ApprovalCount] >= 0),
        CONSTRAINT [CK_AuditExportRequest_DeliveryMethod] CHECK ([DeliveryMethod] IN ('download','sftp')),
        CONSTRAINT [CK_AuditExportRequest_DateRange] CHECK ([FromDateTime] IS NULL OR [ToDateTime] IS NULL OR [FromDateTime] <= [ToDateTime]),
        CONSTRAINT [CK_AuditExportRequest_SegregationOfDutiesPassed] CHECK ([SegregationOfDutiesPassed] IN (0,1)),
        CONSTRAINT [FK_AuditExportRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_AuditExportRequest_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[AuditExportResourceType] (
        [AuditExportResourceTypeId] uniqueidentifier NOT NULL,
        [AuditExportRequestId] uniqueidentifier NOT NULL,
        [ResourceType] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AuditExportResourceType] PRIMARY KEY CLUSTERED ([AuditExportResourceTypeId]),
        CONSTRAINT [UQ_AuditExportResourceType] UNIQUE ([AuditExportRequestId],[ResourceType]),
        CONSTRAINT [FK_AuditExportResourceType_Request] FOREIGN KEY ([AuditExportRequestId]) REFERENCES [ops].[AuditExportRequest]([AuditExportRequestId])
    );

    CREATE TABLE [ops].[ModelRegistry] (
        [ModelRegistryId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Provider] nvarchar(100) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ModelRegistry] PRIMARY KEY CLUSTERED ([ModelRegistryId]),
        CONSTRAINT [UQ_ModelRegistry_Tenant_Name] UNIQUE ([TenantId],[Name]),
        CONSTRAINT [CK_ModelRegistry_Status] CHECK ([Status] IN ('active','deprecated','retired')),
        CONSTRAINT [FK_ModelRegistry_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId])
    );

    CREATE TABLE [ops].[ModelRegistryVersion] (
        [ModelRegistryVersionId] uniqueidentifier NOT NULL,
        [ModelRegistryId] uniqueidentifier NOT NULL,
        [ModelVersion] nvarchar(100) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Changelog] nvarchar(2000) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_ModelRegistryVersion] PRIMARY KEY CLUSTERED ([ModelRegistryVersionId]),
        CONSTRAINT [UQ_ModelRegistryVersion] UNIQUE ([ModelRegistryId],[ModelVersion]),
        CONSTRAINT [CK_ModelRegistryVersion_Status] CHECK ([Status] IN ('active','deprecated','retired')),
        CONSTRAINT [FK_ModelRegistryVersion_ModelRegistry] FOREIGN KEY ([ModelRegistryId]) REFERENCES [ops].[ModelRegistry]([ModelRegistryId])
    );

    CREATE TABLE [ops].[ModelRegistryDataset] (
        [ModelRegistryDatasetId] uniqueidentifier NOT NULL,
        [ModelRegistryId] uniqueidentifier NOT NULL,
        [DatasetId] nvarchar(100) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [DatasetVersion] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ModelRegistryDataset] PRIMARY KEY CLUSTERED ([ModelRegistryDatasetId]),
        CONSTRAINT [UQ_ModelRegistryDataset] UNIQUE ([ModelRegistryId],[DatasetId],[DatasetVersion]),
        CONSTRAINT [FK_ModelRegistryDataset_ModelRegistry] FOREIGN KEY ([ModelRegistryId]) REFERENCES [ops].[ModelRegistry]([ModelRegistryId])
    );

    CREATE TABLE [ops].[RestoreRequest] (
        [RestoreRequestId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RestoreWindowDaysApplied] int NOT NULL,
        [RestoredAt] datetime2(3) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        [Version] int NOT NULL,
        [Etag] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RestoreRequest] PRIMARY KEY CLUSTERED ([RestoreRequestId]),
        CONSTRAINT [CK_RestoreRequest_Status] CHECK ([Status] IN ('requested','approved','rejected','restored','failed')),
        CONSTRAINT [CK_RestoreRequest_RestoreWindowDaysApplied] CHECK ([RestoreWindowDaysApplied] > 0),
        CONSTRAINT [FK_RestoreRequest_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_RestoreRequest_RequestedBy] FOREIGN KEY ([RequestedByUserId]) REFERENCES [core].[UserAccount]([UserId])
    );

    CREATE TABLE [ops].[JobLease] (
        [JobLeaseId] bigint IDENTITY(1,1) NOT NULL,
        [JobQueueId] bigint NOT NULL,
        [WorkerId] nvarchar(100) NOT NULL,
        [LeasedAt] datetime2(3) NOT NULL,
        [LeaseExpiresAt] datetime2(3) NOT NULL,
        [ReleasedAt] datetime2(3) NULL,
        CONSTRAINT [PK_JobLease] PRIMARY KEY CLUSTERED ([JobLeaseId]),
        CONSTRAINT [CK_JobLease_LeaseWindow] CHECK ([LeasedAt] < [LeaseExpiresAt]),
        CONSTRAINT [FK_JobLease_JobQueue] FOREIGN KEY ([JobQueueId]) REFERENCES [ops].[JobQueue]([JobQueueId])
    );

    CREATE TABLE [ops].[JobRetry] (
        [JobRetryId] bigint IDENTITY(1,1) NOT NULL,
        [JobQueueId] bigint NOT NULL,
        [RetryNumber] int NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ScheduledAt] datetime2(3) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_JobRetry] PRIMARY KEY CLUSTERED ([JobRetryId]),
        CONSTRAINT [UQ_JobRetry_JobQueue_RetryNumber] UNIQUE ([JobQueueId],[RetryNumber]),
        CONSTRAINT [CK_JobRetry_RetryNumber] CHECK ([RetryNumber] > 0),
        CONSTRAINT [FK_JobRetry_JobQueue] FOREIGN KEY ([JobQueueId]) REFERENCES [ops].[JobQueue]([JobQueueId])
    );

    CREATE TABLE [ops].[JobDeadLetter] (
        [JobDeadLetterId] bigint IDENTITY(1,1) NOT NULL,
        [JobQueueId] bigint NOT NULL,
        [DeadLetteredAt] datetime2(3) NOT NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_JobDeadLetter] PRIMARY KEY CLUSTERED ([JobDeadLetterId]),
        CONSTRAINT [UQ_JobDeadLetter_JobQueue] UNIQUE ([JobQueueId]),
        CONSTRAINT [CK_JobDeadLetter_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1),
        CONSTRAINT [FK_JobDeadLetter_JobQueue] FOREIGN KEY ([JobQueueId]) REFERENCES [ops].[JobQueue]([JobQueueId])
    );

    CREATE TABLE [integration].[IoTIngestionException] (
        [IoTIngestionExceptionId] uniqueidentifier NOT NULL,
        [IoTStreamId] uniqueidentifier NOT NULL,
        [MappingDefinitionId] uniqueidentifier NULL,
        [SourceRowReference] nvarchar(200) NULL,
        [RawPayload] nvarchar(max) NULL,
        [ValidationErrorDetails] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_IoTIngestionException] PRIMARY KEY CLUSTERED ([IoTIngestionExceptionId]),
        CONSTRAINT [FK_IoTIngestionException_IoTStream] FOREIGN KEY ([IoTStreamId]) REFERENCES [integration].[IoTStream]([IoTStreamId]),
        CONSTRAINT [FK_IoTIngestionException_MappingDefinition] FOREIGN KEY ([MappingDefinitionId]) REFERENCES [integration].[MappingDefinition]([MappingDefinitionId])
    );

    CREATE TABLE [integration].[OnPremConnectorTransfer] (
        [OnPremConnectorTransferId] uniqueidentifier NOT NULL,
        [ConnectorRegistrationId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Direction] nvarchar(20) NOT NULL,
        [TransferType] nvarchar(20) NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ArtifactId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [ChecksumOrPayloadHash] nvarchar(128) NULL,
        [StartedAt] datetime2(3) NULL,
        [CompletedAt] datetime2(3) NULL,
        [FailureReason] nvarchar(2000) NULL,
        [CorrelationId] nvarchar(100) NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_OnPremConnectorTransfer] PRIMARY KEY CLUSTERED ([OnPremConnectorTransferId]),
        CONSTRAINT [CK_OnPremConnectorTransfer_Direction] CHECK ([Direction] IN ('inbound','outbound')),
        CONSTRAINT [CK_OnPremConnectorTransfer_TransferType] CHECK ([TransferType] IN ('file','api')),
        CONSTRAINT [CK_OnPremConnectorTransfer_Status] CHECK ([Status] IN ('queued','running','completed','failed','cancelled')),
        CONSTRAINT [CK_OnPremConnectorTransfer_Timing] CHECK ([StartedAt] IS NULL OR [CompletedAt] IS NULL OR [StartedAt] <= [CompletedAt]),
        CONSTRAINT [FK_OnPremConnectorTransfer_ConnectorRegistration] FOREIGN KEY ([ConnectorRegistrationId]) REFERENCES [integration].[OnPremConnectorRegistration]([OnPremConnectorRegistrationId]),
        CONSTRAINT [FK_OnPremConnectorTransfer_Tenant] FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenant]([TenantId]),
        CONSTRAINT [FK_OnPremConnectorTransfer_Project] FOREIGN KEY ([ProjectId]) REFERENCES [project].[Project]([ProjectId]),
        CONSTRAINT [FK_OnPremConnectorTransfer_Artifact] FOREIGN KEY ([ArtifactId]) REFERENCES [content].[Artifact]([ArtifactId])
    );

    ------------------------------------------------------------
    -- Add deferred FK from NotificationRule to Template
    ------------------------------------------------------------
    ALTER TABLE [core].[NotificationRule]
        ADD CONSTRAINT [FK_NotificationRule_Template]
        FOREIGN KEY ([TemplateId]) REFERENCES [content].[Template]([TemplateId]);

    ------------------------------------------------------------
    -- Indexes
    ------------------------------------------------------------

    CREATE INDEX [IX_Tenant_Name] ON [core].[Tenant] ([Name]);

    CREATE INDEX [IX_Organization_Tenant_Name] ON [core].[Organization] ([TenantId],[Name]);
    CREATE INDEX [IX_Organization_Parent] ON [core].[Organization] ([ParentOrganizationId]);

    CREATE INDEX [IX_Portfolio_Tenant_Name] ON [core].[Portfolio] ([TenantId],[Name]);

    CREATE INDEX [IX_UserAccount_Tenant_Status] ON [core].[UserAccount] ([TenantId],[Status]);
    CREATE INDEX [IX_UserAccount_Organization] ON [core].[UserAccount] ([OrganizationId]);

    CREATE INDEX [IX_RoleTemplate_Tenant_Active] ON [core].[RoleTemplate] ([TenantId],[IsActive]);

    CREATE INDEX [IX_Permission_ResourceType] ON [core].[Permission] ([ResourceType]);

    CREATE INDEX [IX_RolePermission_Permission] ON [core].[RolePermission] ([PermissionId]);

    CREATE INDEX [IX_UserRole_Role] ON [core].[UserRole] ([RoleId]);

    CREATE INDEX [IX_ScopeAssignment_User] ON [core].[ScopeAssignment] ([UserId]);
    CREATE INDEX [IX_ScopeAssignment_Scope] ON [core].[ScopeAssignment] ([ScopeType],[ScopeId]);

    CREATE INDEX [IX_AccessInvite_Tenant_Email_Status] ON [core].[AccessInvite] ([TenantId],[Email],[Status]);

    CREATE INDEX [IX_AccessInviteScope_Scope] ON [core].[AccessInviteScope] ([ScopeType],[ScopeId]);

    CREATE INDEX [IX_AccessGrant_SubjectUser] ON [core].[AccessGrant] ([SubjectUserId]);
    CREATE INDEX [IX_AccessGrant_Scope] ON [core].[AccessGrant] ([ScopeType],[ScopeId]);

    CREATE INDEX [IX_BusinessCalendar_Tenant_Active] ON [core].[BusinessCalendar] ([TenantId],[Active]);
    CREATE INDEX [IX_BusinessHoliday_Date] ON [core].[BusinessHoliday] ([HolidayDate]);

    CREATE INDEX [IX_NotificationSettings_Tenant_User] ON [core].[NotificationSettings] ([TenantId],[UserId]);

    CREATE INDEX [IX_NotificationRule_Tenant_Active] ON [core].[NotificationRule] ([TenantId],[Active]);
    CREATE INDEX [IX_NotificationRule_EventCode] ON [core].[NotificationRule] ([EventCode]);

    CREATE INDEX [IX_NotificationRuleEventType_EventType] ON [core].[NotificationRuleEventType] ([EventType]);

    CREATE INDEX [IX_RegionalProfile_Tenant_Region_Climate] ON [core].[RegionalProfile] ([TenantId],[RegionCode],[ClimateZoneCode]);
    CREATE INDEX [IX_RegionalProfileStandardReference_Profile] ON [core].[RegionalProfileStandardReference] ([RegionalProfileId]);
    CREATE INDEX [IX_RegionalProfileCodeReference_Profile] ON [core].[RegionalProfileCodeReference] ([RegionalProfileId]);

    CREATE INDEX [IX_RatingLibrary_Tenant_System_Status] ON [core].[RatingLibrary] ([TenantId],[RatingSystemCode],[Status]);
    CREATE INDEX [IX_RatingCredit_Library_Category] ON [core].[RatingCredit] ([RatingLibraryId],[Category]);
    CREATE INDEX [IX_CreditDependency_Credit] ON [core].[CreditDependency] ([CreditId]);
    CREATE INDEX [IX_CreditDependency_DependsOn] ON [core].[CreditDependency] ([DependsOnCreditId]);
    CREATE INDEX [IX_Addendum_EffectiveDate] ON [core].[Addendum] ([EffectiveDate]);
    CREATE INDEX [IX_AddendaSLAStatus_Status] ON [core].[AddendaSLAStatus] ([Status]);
    CREATE INDEX [IX_PolicyRule_Tenant_Scope_Priority] ON [core].[PolicyRule] ([TenantId],[Scope],[Priority]);

    CREATE INDEX [IX_Project_Tenant_Status] ON [project].[Project] ([TenantId],[Status]);
    CREATE INDEX [IX_Project_Portfolio] ON [project].[Project] ([PortfolioId]);
    CREATE INDEX [IX_Project_RatingRegion] ON [project].[Project] ([TenantId],[RatingSystemCode],[RatingVersion],[RegionCode]);

    CREATE INDEX [IX_ProjectArea_Project] ON [project].[ProjectArea] ([ProjectId]);
    CREATE INDEX [IX_ProjectArea_Parent] ON [project].[ProjectArea] ([ParentProjectAreaId]);

    CREATE INDEX [IX_ProjectStakeholder_User] ON [project].[ProjectStakeholder] ([UserId]);
    CREATE INDEX [IX_ProjectStakeholder_Group] ON [project].[ProjectStakeholder] ([ProjectId],[StakeholderGroup]);

    CREATE INDEX [IX_ProjectTransferRequest_Project_Status] ON [project].[ProjectTransferRequest] ([ProjectId],[Status]);

    CREATE INDEX [IX_PreAssessmentRun_Project_Status] ON [project].[PreAssessmentRun] ([ProjectId],[Status]);
    CREATE INDEX [IX_PreAssessmentRun_QueuedAt] ON [project].[PreAssessmentRun] ([QueuedAt]);
    CREATE INDEX [IX_PreAssessmentInputArtifact_Artifact] ON [project].[PreAssessmentInputArtifact] ([ArtifactId]);

    CREATE INDEX [IX_InterpretationResult_Project_Credit] ON [project].[InterpretationResult] ([ProjectId],[CreditId]);
    CREATE INDEX [IX_SourceCitation_Owner] ON [project].[SourceCitation] ([OwnerEntityType],[OwnerEntityId]);

    CREATE INDEX [IX_Scorecard_Project] ON [project].[Scorecard] ([ProjectId]);
    CREATE INDEX [IX_WhatIfScenario_Project] ON [project].[WhatIfScenario] ([ProjectId]);
    CREATE INDEX [IX_ScorecardCredit_Credit] ON [project].[ScorecardCredit] ([CreditId]);
    CREATE INDEX [IX_ScoreOverride_Project_Credit_Status] ON [project].[ScoreOverride] ([ProjectId],[CreditId],[ApprovalStatus]);
    CREATE INDEX [IX_ApprovalEvent_Owner] ON [project].[ApprovalEvent] ([OwnerEntityType],[OwnerEntityId]);
    CREATE INDEX [IX_ApprovalEvent_Owner_Actor_Action] ON [project].[ApprovalEvent] ([OwnerEntityType],[OwnerEntityId],[ActorUserId],[Action]);

    CREATE INDEX [IX_EvidenceTask_Project_Status_DueDate] ON [project].[EvidenceTask] ([ProjectId],[Status],[DueDate]);
    CREATE INDEX [IX_EvidenceTask_OwnerUser] ON [project].[EvidenceTask] ([OwnerUserId]);
    CREATE INDEX [IX_EvidenceTaskValidationRule_Task] ON [project].[EvidenceTaskValidationRule] ([EvidenceTaskId]);

    CREATE INDEX [IX_SimulationJob_Project_Status] ON [project].[SimulationJob] ([ProjectId],[Status]);
    CREATE INDEX [IX_SimulationJob_QueueEnteredAt] ON [project].[SimulationJob] ([QueueEnteredAt]);
    CREATE INDEX [IX_SimulationJob_WorkerDispatch] ON [project].[SimulationJob] ([Status],[QueueEnteredAt],[Tool],[SimulationType]);

    CREATE INDEX [IX_Milestone_Project_Status_PlannedDate] ON [project].[Milestone] ([ProjectId],[Status],[PlannedDate]);
    CREATE INDEX [IX_Budget_Project] ON [project].[Budget] ([ProjectId]);

    CREATE INDEX [IX_Recommendation_Project_Rank] ON [project].[Recommendation] ([ProjectId],[Rank]);
    CREATE INDEX [IX_RecommendationPrerequisite_Recommendation] ON [project].[RecommendationPrerequisite] ([RecommendationId]);
    CREATE INDEX [IX_RecommendationTradeoff_Recommendation] ON [project].[RecommendationTradeoff] ([RecommendationId]);
    CREATE INDEX [IX_RecommendationBackingAnalysis_Recommendation_SortOrder] ON [project].[RecommendationBackingAnalysis] ([RecommendationId],[SortOrder]);
    CREATE INDEX [IX_RecommendationBackingAnalysis_SimulationJob] ON [project].[RecommendationBackingAnalysis] ([SimulationJobId]);
    CREATE INDEX [IX_RecommendationBackingAnalysis_DocumentArtifact] ON [project].[RecommendationBackingAnalysis] ([DocumentArtifactId]);
    CREATE INDEX [IX_RecommendationBackingAnalysis_CarbonResult] ON [project].[RecommendationBackingAnalysis] ([CarbonResultId]);

    CREATE INDEX [IX_AuditorQuery_Project_State_Due] ON [project].[AuditorQuery] ([ProjectId],[State],[FirstResponseDueAt]);
    CREATE INDEX [IX_AuditorClaim_Query] ON [project].[AuditorClaim] ([AuditorQueryId]);
    CREATE INDEX [IX_AuditorQueryTransition_AuditorQuery_CreatedAt] ON [project].[AuditorQueryTransition] ([AuditorQueryId],[CreatedAt]);

    CREATE INDEX [IX_Anomaly_Project_Status_Severity] ON [project].[Anomaly] ([ProjectId],[Status],[Severity]);
    CREATE INDEX [IX_CorrectiveAction_Anomaly_Status] ON [project].[CorrectiveAction] ([AnomalyId],[Status]);
    CREATE INDEX [IX_AnomalyProposedAction_Anomaly] ON [project].[AnomalyProposedAction] ([AnomalyId]);
    CREATE INDEX [IX_CorrectiveActionCitation_CorrectiveAction] ON [project].[CorrectiveActionCitation] ([CorrectiveActionId]);
    CREATE INDEX [IX_EvidenceTaskTransition_EvidenceTask_CreatedAt] ON [project].[EvidenceTaskTransition] ([EvidenceTaskId],[CreatedAt]);

    CREATE INDEX [IX_ProjectIntakeRecord_Tenant_Project] ON [project].[ProjectIntakeRecord] ([TenantId],[ProjectId]);

    CREATE INDEX [IX_CarbonResult_Tenant_Project_ImportedAt] ON [project].[CarbonResult] ([TenantId],[ProjectId],[ImportedAt]);
    CREATE INDEX [IX_CarbonResult_Project_ResultType] ON [project].[CarbonResult] ([ProjectId],[ResultType]);

    CREATE INDEX [IX_Artifact_Project_Credit_Status] ON [content].[Artifact] ([ProjectId],[CreditId],[UploadStatus]);
    CREATE INDEX [IX_Artifact_Project_FileName] ON [content].[Artifact] ([ProjectId],[FileName]);
    CREATE INDEX [IX_Artifact_Checksum] ON [content].[Artifact] ([ChecksumSha256]);
    CREATE INDEX [IX_Artifact_Project_OwnerUser] ON [content].[Artifact] ([ProjectId],[OwnerUserId]);
    CREATE INDEX [IX_Artifact_Project_Credit_ArtifactDate] ON [content].[Artifact] ([ProjectId],[CreditId],[ArtifactDate]);
    CREATE INDEX [IX_Artifact_Project_MediaType_ArtifactDate] ON [content].[Artifact] ([ProjectId],[MediaType],[ArtifactDate]);
    CREATE INDEX [IX_Artifact_SearchCovering] ON [content].[Artifact] ([TenantId],[ProjectId],[CreditId],[MediaType],[UploadStatus],[ClassificationStatus],[CreatedAt]) INCLUDE ([FileName],[SourceType],[SourceSystem],[ChecksumSha256]);

    CREATE INDEX [IX_ArtifactTag_Tag] ON [content].[ArtifactTag] ([Tag]);

    CREATE INDEX [IX_ExtractionResult_Artifact_Type] ON [content].[ExtractionResult] ([ArtifactId],[ExtractionType]);
    CREATE INDEX [IX_ExtractedField_ExtractionResult_FieldName] ON [content].[ExtractedField] ([ExtractionResultId],[FieldName]);

    CREATE INDEX [IX_ClassificationResult_Project] ON [content].[ClassificationResult] ([ProjectId]);
    CREATE INDEX [IX_ClassificationResult_ReviewedByUser] ON [content].[ClassificationResult] ([ReviewedByUserId]);

    CREATE INDEX [IX_Template_Tenant_DocumentType] ON [content].[Template] ([TenantId],[DocumentType]);
    CREATE INDEX [IX_TemplateVersion_Template_Published] ON [content].[TemplateVersion] ([TemplateId],[IsPublished]);

    CREATE INDEX [IX_DocumentArtifact_Project_DocumentType_Format] ON [content].[DocumentArtifact] ([ProjectId],[DocumentType],[Format]);
    CREATE INDEX [IX_DocumentSource_DocumentArtifact] ON [content].[DocumentSource] ([DocumentArtifactId]);

    CREATE INDEX [IX_SubmissionPackage_Project_ApprovalStatus] ON [content].[SubmissionPackage] ([ProjectId],[ApprovalStatus]);
    CREATE INDEX [IX_SubmissionPackageArtifact_Package] ON [content].[SubmissionPackageArtifact] ([SubmissionPackageId]);

    CREATE INDEX [IX_CannedResponse_Tenant_Active] ON [content].[CannedResponse] ([TenantId],[Active]);
    CREATE INDEX [IX_CannedResponseTag_Tag] ON [content].[CannedResponseTag] ([Tag]);

    CREATE INDEX [IX_StandardCorpusItem_Tenant_SourceType] ON [content].[StandardCorpusItem] ([TenantId],[SourceType]);
    CREATE INDEX [IX_StandardCorpusItem_LicenseEndDate] ON [content].[StandardCorpusItem] ([LicenseEndDate]);

    CREATE INDEX [IX_DocumentReviewEvent_DocumentArtifact_CreatedAt] ON [content].[DocumentReviewEvent] ([DocumentArtifactId],[CreatedAt]);
    CREATE INDEX [IX_ExtractionReviewEvent_ExtractionResult_CreatedAt] ON [content].[ExtractionReviewEvent] ([ExtractionResultId],[CreatedAt]);

    CREATE INDEX [IX_ImportTemplate_Tenant_Active] ON [content].[ImportTemplate] ([TenantId],[Active]);

    CREATE INDEX [IX_MappingDefinition_Tenant_ToolCode] ON [integration].[MappingDefinition] ([TenantId],[ToolCode]);
    CREATE INDEX [IX_MappingFieldMap_MappingDefinition] ON [integration].[MappingFieldMap] ([MappingDefinitionId]);

    CREATE INDEX [IX_IoTStream_Project_DeviceId] ON [integration].[IoTStream] ([ProjectId],[DeviceId]);
    CREATE INDEX [IX_IoTMeasurement_Stream_Timestamp] ON [integration].[IoTMeasurement] ([IoTStreamId],[SampleTimestamp]);

    CREATE INDEX [IX_WebhookSubscription_Tenant_Active] ON [integration].[WebhookSubscription] ([TenantId],[Active]);
    CREATE INDEX [IX_WebhookSubscriptionEventType_EventType] ON [integration].[WebhookSubscriptionEventType] ([EventType]);

    CREATE INDEX [IX_WebhookDelivery_Subscription_CreatedAt] ON [integration].[WebhookDelivery] ([WebhookSubscriptionId],[CreatedAt]);
    CREATE INDEX [IX_WebhookDelivery_Tenant_Kind_CreatedAt] ON [integration].[WebhookDelivery] ([TenantId],[DeliveryKind],[CreatedAt]);
    CREATE INDEX [IX_WebhookDelivery_Resource] ON [integration].[WebhookDelivery] ([ResourceType],[ResourceId]);

    CREATE INDEX [IX_ProviderCredential_Tenant_Provider_Scope_Status] ON [integration].[ProviderCredential] ([TenantId],[ProviderCode],[ScopeType],[ScopeId],[Status]);
    CREATE INDEX [IX_ProviderCredentialRotationEvent_ProviderCredential_CreatedAt] ON [integration].[ProviderCredentialRotationEvent] ([ProviderCredentialId],[CreatedAt]);

    CREATE INDEX [IX_PortalConfiguration_Tenant_Enabled] ON [integration].[PortalConfiguration] ([TenantId],[Enabled]);
    CREATE INDEX [IX_LicenseSeatAssignment_Tenant_Tool_Status] ON [integration].[LicenseSeatAssignment] ([TenantId],[ToolCode],[CheckoutStatus]);

    CREATE INDEX [IX_IoTIngestionException_IoTStream_CreatedAt] ON [integration].[IoTIngestionException] ([IoTStreamId],[CreatedAt]);
    CREATE INDEX [IX_IoTIngestionException_MappingDefinition] ON [integration].[IoTIngestionException] ([MappingDefinitionId]);

    CREATE INDEX [IX_OnPremConnectorRegistration_Tenant_Status] ON [integration].[OnPremConnectorRegistration] ([TenantId],[Status]);
    CREATE INDEX [IX_OnPremConnectorRegistration_LastHeartbeatAt] ON [integration].[OnPremConnectorRegistration] ([LastHeartbeatAt]);
    CREATE INDEX [IX_OnPremConnectorTransfer_Connector_Status_CreatedAt] ON [integration].[OnPremConnectorTransfer] ([ConnectorRegistrationId],[Status],[CreatedAt]);
    CREATE INDEX [IX_OnPremConnectorTransfer_Tenant_CorrelationId] ON [integration].[OnPremConnectorTransfer] ([TenantId],[CorrelationId]);

    CREATE INDEX [IX_Notification_Tenant_Status_CreatedAt] ON [ops].[Notification] ([TenantId],[Status],[CreatedAt]);
    CREATE INDEX [IX_Notification_RecipientUser] ON [ops].[Notification] ([RecipientUserId]);
    CREATE INDEX [IX_Notification_Tenant_Channel_ProviderCode_Status_CreatedAt] ON [ops].[Notification] ([TenantId],[Channel],[ProviderCode],[Status],[CreatedAt]);
    CREATE INDEX [IX_Notification_EscalationScan] ON [ops].[Notification] ([Status],[AcknowledgedAt],[CreatedAt],[DeliveredAt]);

    CREATE INDEX [IX_WhatsAppConsent_Subject] ON [ops].[WhatsAppConsent] ([SubjectType],[SubjectId]);
    CREATE INDEX [IX_WhatsAppConsent_PhoneNumber] ON [ops].[WhatsAppConsent] ([PhoneNumber]);

    CREATE INDEX [IX_AuditLog_Tenant_CreatedAt] ON [ops].[AuditLog] ([TenantId],[CreatedAt]);
    CREATE INDEX [IX_AuditLog_Resource] ON [ops].[AuditLog] ([ResourceType],[ResourceId]);
    CREATE INDEX [IX_AuditLog_Actor] ON [ops].[AuditLog] ([ActorUserId],[CreatedAt]);
    CREATE INDEX [IX_AuditLog_Search] ON [ops].[AuditLog] ([TenantId],[ResourceType],[Action],[CreatedAt]) INCLUDE ([ActorUserId],[Outcome],[CorrelationId]);

    CREATE INDEX [IX_AIInteractionLog_Tenant_CreatedAt] ON [ops].[AIInteractionLog] ([TenantId],[CreatedAt]);
    CREATE INDEX [IX_AIInteractionLog_UseCase] ON [ops].[AIInteractionLog] ([UseCase]);
    CREATE INDEX [IX_AIInteractionLog_GatewayRequestId] ON [ops].[AIInteractionLog] ([GatewayRequestId]);
    CREATE INDEX [IX_AIInteractionDatasetReference_Log] ON [ops].[AIInteractionDatasetReference] ([AIInteractionLogId]);

    CREATE INDEX [IX_TrainingPermissionRecord_Tenant_Subject] ON [ops].[TrainingPermissionRecord] ([TenantId],[SubjectType],[SubjectId]);

    CREATE INDEX [IX_RiskRegisterItem_Tenant_Status_Category] ON [ops].[RiskRegisterItem] ([TenantId],[Status],[Category]);
    CREATE INDEX [IX_RiskRegisterItem_Project] ON [ops].[RiskRegisterItem] ([ProjectId]);
    CREATE INDEX [IX_RiskRegisterMonitorEvent_Tenant_RiskRegisterItem_CreatedAt] ON [ops].[RiskRegisterMonitorEvent] ([TenantId],[RiskRegisterItemId],[CreatedAt]);

    CREATE INDEX [IX_KPIRecord_Tenant_MetricCode_Period] ON [ops].[KPIRecord] ([TenantId],[MetricCode],[PeriodStart],[PeriodEnd]);
    CREATE INDEX [IX_KPIRecord_Scope] ON [ops].[KPIRecord] ([ScopeType],[ScopeId]);

    CREATE INDEX [IX_TenantExportRequest_Tenant_Status] ON [ops].[TenantExportRequest] ([TenantId],[Status]);
    CREATE INDEX [IX_SecureDeletionRequest_Tenant_Status] ON [ops].[SecureDeletionRequest] ([TenantId],[Status]);
    CREATE INDEX [IX_AuditExportRequest_Tenant_Status] ON [ops].[AuditExportRequest] ([TenantId],[Status]);

    CREATE INDEX [IX_ModelRegistry_Tenant_Status] ON [ops].[ModelRegistry] ([TenantId],[Status]);
    CREATE INDEX [IX_ModelRegistryVersion_ModelRegistry_Status] ON [ops].[ModelRegistryVersion] ([ModelRegistryId],[Status]);

    CREATE INDEX [IX_JobQueue_Status_AvailableAt_Priority] ON [ops].[JobQueue] ([Status],[AvailableAt],[Priority]);
    CREATE INDEX [IX_JobQueue_Tenant_JobType] ON [ops].[JobQueue] ([TenantId],[JobType]);

    CREATE INDEX [IX_JobLease_JobQueue] ON [ops].[JobLease] ([JobQueueId]);
    CREATE INDEX [IX_JobLease_WorkerId] ON [ops].[JobLease] ([WorkerId]);

    CREATE INDEX [IX_JobRetry_JobQueue] ON [ops].[JobRetry] ([JobQueueId]);
    CREATE INDEX [IX_JobDeadLetter_DeadLetteredAt] ON [ops].[JobDeadLetter] ([DeadLetteredAt]);

    CREATE INDEX [IX_ScheduledTask_Enabled_NextRunAt] ON [ops].[ScheduledTask] ([Enabled],[NextRunAt]);

    CREATE INDEX [IX_RestoreRequest_Tenant_Status] ON [ops].[RestoreRequest] ([TenantId],[Status]);
    CREATE INDEX [IX_RestoreRequest_Entity] ON [ops].[RestoreRequest] ([EntityType],[EntityId]);

    CREATE INDEX [IX_WeatherFileCache_Tenant_WeatherFileCode_ApprovalStatus_IsLatestApprovedDataset]
        ON [integration].[WeatherFileCache] ([TenantId],[WeatherFileCode],[ApprovalStatus],[IsLatestApprovedDataset]);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO