-- AgriStream IoT - Azure SQL schema (user management + device registry)
-- Optional: the MVC app creates these tables automatically on first start (EF Core EnsureCreated).
-- Run this in the Azure Portal "Query editor" only if you want to create them yourself BEFORE the first run,
-- and take a screenshot as evidence. Run it on an EMPTY database.

IF OBJECT_ID('dbo.Roles') IS NULL
CREATE TABLE dbo.Roles (
    Id   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name NVARCHAR(50)      NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Roles_Name')
    CREATE UNIQUE INDEX IX_Roles_Name ON dbo.Roles(Name);
GO

IF OBJECT_ID('dbo.Users') IS NULL
CREATE TABLE dbo.Users (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Username     NVARCHAR(100)     NOT NULL,
    PasswordHash NVARCHAR(200)     NOT NULL,
    RoleId       INT               NOT NULL CONSTRAINT FK_Users_Roles_RoleId REFERENCES dbo.Roles(Id),
    FarmId       NVARCHAR(50)      NOT NULL,
    CreatedUtc   DATETIME2         NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Username')
    CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users(Username);
GO

IF OBJECT_ID('dbo.Devices') IS NULL
CREATE TABLE dbo.Devices (
    Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Devices PRIMARY KEY,
    DeviceId       NVARCHAR(50)      NOT NULL,
    FarmId         NVARCHAR(50)      NOT NULL,
    FieldName      NVARCHAR(100)     NOT NULL,
    Status         NVARCHAR(30)      NOT NULL,
    LastCommand    NVARCHAR(200)     NULL,
    LastUpdatedUtc DATETIME2         NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Devices_DeviceId')
    CREATE UNIQUE INDEX IX_Devices_DeviceId ON dbo.Devices(DeviceId);
GO

-- Roles (same Ids the app expects). Users and devices are seeded by the app on first start.
IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    SET IDENTITY_INSERT dbo.Roles ON;
    INSERT INTO dbo.Roles (Id, Name) VALUES (1, N'Farmer'), (2, N'Agronomist'), (3, N'Admin');
    SET IDENTITY_INSERT dbo.Roles OFF;
END
GO

-- Evidence queries for your screenshots:
-- SELECT u.Id, u.Username, r.Name AS Role, u.FarmId FROM dbo.Users u JOIN dbo.Roles r ON r.Id = u.RoleId;
-- SELECT * FROM dbo.Devices;
