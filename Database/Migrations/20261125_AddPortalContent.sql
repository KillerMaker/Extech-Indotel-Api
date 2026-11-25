IF OBJECT_ID(N'dbo.PortalContent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContent (
        [Key] nvarchar(100) NOT NULL CONSTRAINT PK_PortalContent PRIMARY KEY,
        [Value] nvarchar(2000) NOT NULL,
        ModifiedAt datetime2 NOT NULL CONSTRAINT DF_PortalContent_ModifiedAt DEFAULT SYSUTCDATETIME(),
        ModifiedByUserId int NULL,
        CONSTRAINT FK_PortalContent_AppUser FOREIGN KEY (ModifiedByUserId) REFERENCES dbo.AppUser(UserId)
    );
END;
