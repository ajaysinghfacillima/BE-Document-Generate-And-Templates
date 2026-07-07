IF OBJECT_ID('content.TemplateVersion', 'U') IS NOT NULL
BEGIN
    DROP TABLE content.TemplateVersion;
END;

IF OBJECT_ID('content.Template', 'U') IS NOT NULL
BEGIN
    DROP TABLE content.Template;
END;
