-- Repository-compatible schema. Run in the target database with sqlcmd -b -x.
-- Existing tables and application data are preserved; incremental upgrades follow.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
GO
IF OBJECT_ID(N'dbo.UserRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRole (
        RoleId int NOT NULL CONSTRAINT PK_UserRole PRIMARY KEY,
        RoleName nvarchar(50) NOT NULL CONSTRAINT UQ_UserRole_RoleName UNIQUE,
        RoleDescription nvarchar(255) NULL
    );
END;
GO
IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUser (
        UserId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
        Email nvarchar(320) NOT NULL CONSTRAINT UQ_AppUser_Email UNIQUE,
        Password varchar(256) NOT NULL,
        Name nvarchar(255) NOT NULL,
        Phone nvarchar(50) NOT NULL CONSTRAINT DF_AppUser_Phone DEFAULT (N''),
        RoleId int NOT NULL,
        CONSTRAINT FK_AppUser_UserRole FOREIGN KEY (RoleId) REFERENCES dbo.UserRole(RoleId)
    );
END;
GO
IF OBJECT_ID(N'dbo.Client', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Client (
        ClientId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Client PRIMARY KEY,
        PublicId int NOT NULL,
        WisproId nvarchar(128) NOT NULL CONSTRAINT UQ_Client_WisproId UNIQUE,
        CreatedById int NOT NULL,
        Name nvarchar(255) NOT NULL,
        PobertyLevel int NOT NULL CONSTRAINT DF_Client_PobertyLevel DEFAULT (0),
        ContractNumber nvarchar(255) NULL,
        SiubenActivated bit NOT NULL CONSTRAINT DF_Client_SiubenActivated DEFAULT (0),
        SiubenDisabled bit NOT NULL CONSTRAINT DF_Client_SiubenDisabled DEFAULT (0),
        Email nvarchar(320) NULL,
        Street nvarchar(500) NOT NULL CONSTRAINT DF_Client_Street DEFAULT (N''),
        Number nvarchar(50) NOT NULL CONSTRAINT DF_Client_Number DEFAULT (N''),
        Phone nvarchar(50) NULL,
        PhoneMobile nvarchar(50) NULL,
        NationalIdentificationNumber nvarchar(32) NOT NULL,
        DocumentNumber AS REPLACE(NationalIdentificationNumber, N'-', N'') PERSISTED,
        Locality nvarchar(100) NULL,
        City nvarchar(255) NULL,
        State nvarchar(255) NULL,
        CONSTRAINT FK_Client_AppUser FOREIGN KEY (CreatedById) REFERENCES dbo.AppUser(UserId)
    );
    CREATE UNIQUE INDEX UX_Client_DocumentNumber ON dbo.Client(DocumentNumber);
    CREATE INDEX IX_Client_Email ON dbo.Client(Email);
    CREATE INDEX IX_Client_CreatedById ON dbo.Client(CreatedById);
END;
GO
IF OBJECT_ID(N'dbo.EmailTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplate (
        Name nvarchar(128) NOT NULL CONSTRAINT PK_EmailTemplate PRIMARY KEY,
        Template nvarchar(max) NOT NULL
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.UserRole WHERE RoleId = 1)
    INSERT dbo.UserRole(RoleId, RoleName, RoleDescription) VALUES (1, N'Admin', N'Administracion del portal y sincronizacion Siuben');
IF NOT EXISTS (SELECT 1 FROM dbo.UserRole WHERE RoleId = 2)
    INSERT dbo.UserRole(RoleId, RoleName, RoleDescription) VALUES (2, N'Seller', N'Consulta y registro de clientes');
GO
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE Name = N'ClientCreated')
    INSERT dbo.EmailTemplate(Name, Template) VALUES (N'ClientCreated', N'<p>Cliente registrado: $$Name$$</p><p>Cedula: $$NationalIdentificationNumber$$</p><p>Wispro: $$WisproId$$</p>');
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE Name = N'ClientUpdated')
    INSERT dbo.EmailTemplate(Name, Template) VALUES (N'ClientUpdated', N'<p>Solicitud de cambio de contrato de $$Name$$.</p><p>Cedula: $$NationalIdentificationNumber$$</p><p>Contrato: $$ContractNumber$$</p>');
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE Name = N'UserCreated')
    INSERT dbo.EmailTemplate(Name, Template) VALUES (N'UserCreated', N'<p>Se ha creado el usuario $$Name$$.</p><p>Correo: $$Email$$</p>');
GO
COMMIT TRANSACTION;
GO
