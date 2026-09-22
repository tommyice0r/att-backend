USE [CLIENTACCESS]
GO

-- ============================================================
-- NPANXX: Mapeo de prefijos telefónicos a operador ATT
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='NPANXX' AND xtype='U')
CREATE TABLE NPANXX (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NPANXX VARCHAR(6) NOT NULL,
    NPA VARCHAR(3) NOT NULL,
    NXX VARCHAR(3) NOT NULL,
    StateCode VARCHAR(2) NOT NULL,
    StateName VARCHAR(50) NOT NULL,
    AttWirelessEntity VARCHAR(200),
    AttWirelessOcn VARCHAR(20),
    AttLandlineEntity VARCHAR(200),
    CricketEntity VARCHAR(200),
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

-- ============================================================
-- Sequences: Secuencias NPA-NXX generadas por cliente
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Sequences' AND xtype='U')
CREATE TABLE Sequences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccessId INT NOT NULL,
    Sequence VARCHAR(6) NOT NULL,
    StateCode VARCHAR(2),
    StateName VARCHAR(50),
    TotalLeads INT DEFAULT 0,
    Status VARCHAR(20) DEFAULT 'pending', -- pending, processing, completed, failed
    CreatedAt DATETIME DEFAULT GETDATE(),
    CompletedAt DATETIME NULL,
    FOREIGN KEY (AccessId) REFERENCES ClientAccessRecords(AccessId)


)
GO

-- ============================================================
-- Leads: Numeros telefonicos generados/depurados
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Leads' AND xtype='U')
CREATE TABLE Leads (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccessId INT NOT NULL,
    SequenceId INT NULL,
    PhoneNumber VARCHAR(15) NOT NULL,
    FullName NVARCHAR(200),
    Address NVARCHAR(500),
    ZipCode VARCHAR(10),
    StateCode VARCHAR(2),
    Source VARCHAR(50) DEFAULT 'generator', -- generator, upload, manual
    Status VARCHAR(20) DEFAULT 'pending',  -- pending, processing, hit, bad, blocked
    CreatedAt DATETIME DEFAULT GETDATE(),
    ProcessedAt DATETIME NULL,
    FOREIGN KEY (AccessId) REFERENCES ClientAccessRecords(AccessId)

,
    FOREIGN KEY (SequenceId) REFERENCES Sequences(Id)
)
GO

-- ============================================================
-- Hits: Numeros detectados como elegibles en ATT
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Hits' AND xtype='U')
CREATE TABLE Hits (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccessId INT NOT NULL,
    LeadId INT NULL,
    PhoneNumber VARCHAR(15) NOT NULL,
    FullName NVARCHAR(200),
    Address NVARCHAR(500),
    ZipCode VARCHAR(10),
    DeviceMessage NVARCHAR(500),
    DeviceType VARCHAR(50),
    HitType VARCHAR(50), -- HIT, HIT_PREVIEW
    ProfileRaw NVARCHAR(MAX),
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (AccessId) REFERENCES ClientAccessRecords(AccessId)

,
    FOREIGN KEY (LeadId) REFERENCES Leads(Id)
)
GO

-- ============================================================
-- BotSessions: Estado de los workers del bot
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BotSessions' AND xtype='U')
CREATE TABLE BotSessions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    WorkerId INT NOT NULL,
    AccessId INT NULL,
    ProfileIdGoLogin VARCHAR(100),
    Status VARCHAR(20) DEFAULT 'idle', -- idle, running, blocked, error
    LeadsProcessed INT DEFAULT 0,
    HitsFound INT DEFAULT 0,
    LastHeartbeat DATETIME,
    StartedAt DATETIME,
    ErrorMessage NVARCHAR(MAX),
    CreatedAt DATETIME DEFAULT GETDATE()
)
GO

-- ============================================================
-- Config: Reemplaza la tabla de SQLite
-- ============================================================
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='GlobalConfig' AND xtype='U')
CREATE TABLE GlobalConfig (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Key] NVARCHAR(100) NOT NULL UNIQUE,
    [Value] NVARCHAR(MAX),
    [Description] NVARCHAR(500),
    UpdatedAt DATETIME DEFAULT GETDATE()
)
GO

-- Insertar config defaults si no existen
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'KEYABS')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('KEYABS', '', 'GoLogin API Key')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'KEY')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('KEY', '', 'Cryptolens Key / Access Key')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'PROXY_TYPE_2')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('PROXY_TYPE_2', 'http', 'Proxy type for generator')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'PROXY_SERVER_2')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('PROXY_SERVER_2', '', 'Proxy server for generator')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'PROXY_PORT_2')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('PROXY_PORT_2', '', 'Proxy port for generator')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'PROXY_USER_2')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('PROXY_USER_2', '', 'Proxy user for generator')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'PROXY_PASS_2')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('PROXY_PASS_2', '', 'Proxy password for generator')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'TELEGRAM_BOT_TOKEN')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('TELEGRAM_BOT_TOKEN', '', 'Telegram Bot Token')
IF NOT EXISTS (SELECT * FROM GlobalConfig WHERE [Key] = 'TELEGRAM_CHAT_ID')
    INSERT INTO GlobalConfig ([Key], [Value], [Description]) VALUES ('TELEGRAM_CHAT_ID', '', 'Telegram Chat ID')
GO

PRINT 'Migration completed successfully'
GO
