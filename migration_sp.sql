USE [CLIENTACCESS]
GO

-- ============================================================
-- SP: Importar NPANXX desde CSV
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_ImportNPANXX' AND xtype='P')
    DROP PROCEDURE sp_ImportNPANXX
GO
CREATE PROCEDURE sp_ImportNPANXX
    @NPANXX VARCHAR(6),
    @NPA VARCHAR(3),
    @NXX VARCHAR(3),
    @StateCode VARCHAR(2),
    @StateName VARCHAR(50),
    @AttWirelessEntity VARCHAR(200) = NULL,
    @AttWirelessOcn VARCHAR(20) = NULL,
    @AttLandlineEntity VARCHAR(200) = NULL,
    @CricketEntity VARCHAR(200) = NULL
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM NPANXX WHERE NPANXX = @NPANXX)
        INSERT INTO NPANXX (NPANXX, NPA, NXX, StateCode, StateName, AttWirelessEntity, AttWirelessOcn, AttLandlineEntity, CricketEntity)
        VALUES (@NPANXX, @NPA, @NXX, @StateCode, @StateName, @AttWirelessEntity, @AttWirelessOcn, @AttLandlineEntity, @CricketEntity)
END
GO

-- ============================================================
-- SP: Obtener config global
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetGlobalConfig' AND xtype='P')
    DROP PROCEDURE sp_GetGlobalConfig
GO
CREATE PROCEDURE sp_GetGlobalConfig
AS
BEGIN
    SELECT [Key], [Value], [Description] FROM GlobalConfig
END
GO

-- ============================================================
-- SP: Guardar config global
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_SaveGlobalConfig' AND xtype='P')
    DROP PROCEDURE sp_SaveGlobalConfig
GO
CREATE PROCEDURE sp_SaveGlobalConfig
    @Key NVARCHAR(100),
    @Value NVARCHAR(MAX)
AS
BEGIN
    IF EXISTS (SELECT 1 FROM GlobalConfig WHERE [Key] = @Key)
        UPDATE GlobalConfig SET [Value] = @Value, UpdatedAt = GETDATE() WHERE [Key] = @Key
    ELSE
        INSERT INTO GlobalConfig ([Key], [Value]) VALUES (@Key, @Value)
END
GO

-- ============================================================
-- SP: Crear secuencia
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_CreateSequence' AND xtype='P')
    DROP PROCEDURE sp_CreateSequence
GO
CREATE PROCEDURE sp_CreateSequence
    @AccessId INT,
    @Sequence VARCHAR(6),
    @StateCode VARCHAR(2) = NULL,
    @StateName VARCHAR(50) = NULL
AS
BEGIN
    DECLARE @ExistingId INT
    SELECT @ExistingId = Id FROM Sequences WHERE AccessId = @AccessId AND Sequence = @Sequence
    IF @ExistingId IS NULL
    BEGIN
        INSERT INTO Sequences (AccessId, [Sequence], StateCode, StateName)
        VALUES (@AccessId, @Sequence, @StateCode, @StateName);
        SELECT SCOPE_IDENTITY() AS Id
    END
    ELSE
        SELECT @ExistingId AS Id
END
GO

-- ============================================================
-- SP: Listar secuencias por cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetSequencesByAccess' AND xtype='P')
    DROP PROCEDURE sp_GetSequencesByAccess
GO
CREATE PROCEDURE sp_GetSequencesByAccess
    @AccessId INT
AS
BEGIN
    SELECT Id, [Sequence], StateCode, StateName, TotalLeads, [Status], CreatedAt, CompletedAt
    FROM Sequences WHERE AccessId = @AccessId ORDER BY CreatedAt DESC
END
GO

-- ============================================================
-- SP: Insertar lead
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_InsertLead' AND xtype='P')
    DROP PROCEDURE sp_InsertLead
GO
CREATE PROCEDURE sp_InsertLead
    @AccessId INT,
    @SequenceId INT = NULL,
    @PhoneNumber VARCHAR(15),
    @FullName NVARCHAR(200) = NULL,
    @Address NVARCHAR(500) = NULL,
    @ZipCode VARCHAR(10) = NULL,
    @StateCode VARCHAR(2) = NULL,
    @Source VARCHAR(50) = 'generator',
    @Status VARCHAR(20) = 'pending'
AS
BEGIN
    -- Evitar duplicados por numero
    IF NOT EXISTS (SELECT 1 FROM Leads WHERE AccessId = @AccessId AND PhoneNumber = @PhoneNumber)
        INSERT INTO Leads (AccessId, SequenceId, PhoneNumber, FullName, [Address], ZipCode, StateCode, [Source], [Status])
        VALUES (@AccessId, @SequenceId, @PhoneNumber, @FullName, @Address, @ZipCode, @StateCode, @Source, @Status)
END
GO

-- ============================================================
-- SP: Obtener leads pendientes por cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetPendingLeads' AND xtype='P')
    DROP PROCEDURE sp_GetPendingLeads
GO
CREATE PROCEDURE sp_GetPendingLeads
    @AccessId INT,
    @Limit INT = 100
AS
BEGIN
    SELECT TOP (@Limit) Id, PhoneNumber, FullName, [Address], ZipCode, StateCode
    FROM Leads WHERE AccessId = @AccessId AND [Status] = 'pending' ORDER BY Id ASC
END
GO

-- ============================================================
-- SP: Marcar lead como procesado
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_UpdateLeadStatus' AND xtype='P')
    DROP PROCEDURE sp_UpdateLeadStatus
GO
CREATE PROCEDURE sp_UpdateLeadStatus
    @LeadId INT,
    @Status VARCHAR(20),
    @ProcessedAt DATETIME = NULL
AS
BEGIN
    UPDATE Leads SET [Status] = @Status, ProcessedAt = ISNULL(@ProcessedAt, GETDATE()) WHERE Id = @LeadId
END
GO

-- ============================================================
-- SP: Contar leads por cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_CountLeads' AND xtype='P')
    DROP PROCEDURE sp_CountLeads
GO
CREATE PROCEDURE sp_CountLeads
    @AccessId INT,
    @Status VARCHAR(20) = NULL
AS
BEGIN
    IF @Status IS NULL
        SELECT COUNT(*) AS [Count] FROM Leads WHERE AccessId = @AccessId
    ELSE
        SELECT COUNT(*) AS [Count] FROM Leads WHERE AccessId = @AccessId AND [Status] = @Status
END
GO

-- ============================================================
-- SP: Insertar hit
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_InsertHit' AND xtype='P')
    DROP PROCEDURE sp_InsertHit
GO
CREATE PROCEDURE sp_InsertHit
    @AccessId INT,
    @LeadId INT = NULL,
    @PhoneNumber VARCHAR(15),
    @FullName NVARCHAR(200) = NULL,
    @Address NVARCHAR(500) = NULL,
    @ZipCode VARCHAR(10) = NULL,
    @DeviceMessage NVARCHAR(500) = NULL,
    @DeviceType VARCHAR(50) = NULL,
    @HitType VARCHAR(50) = NULL,
    @ProfileRaw NVARCHAR(MAX) = NULL
AS
BEGIN
    INSERT INTO Hits (AccessId, LeadId, PhoneNumber, FullName, [Address], ZipCode, DeviceMessage, DeviceType, HitType, ProfileRaw)
    VALUES (@AccessId, @LeadId, @PhoneNumber, @FullName, @Address, @ZipCode, @DeviceMessage, @DeviceType, @HitType, @ProfileRaw)
    
    -- Si hay leadId, marcar como hit
    IF @LeadId IS NOT NULL
        UPDATE Leads SET [Status] = 'hit', ProcessedAt = GETDATE() WHERE Id = @LeadId
    
    SELECT SCOPE_IDENTITY() AS Id
END
GO

-- ============================================================
-- SP: Listar hits por cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetHitsByAccess' AND xtype='P')
    DROP PROCEDURE sp_GetHitsByAccess
GO
CREATE PROCEDURE sp_GetHitsByAccess
    @AccessId INT
AS
BEGIN
    SELECT Id, PhoneNumber, FullName, [Address], ZipCode, DeviceMessage, DeviceType, HitType, CreatedAt
    FROM Hits WHERE AccessId = @AccessId ORDER BY CreatedAt DESC
END
GO

-- ============================================================
-- SP: Contar hits por cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_CountHitsByAccess' AND xtype='P')
    DROP PROCEDURE sp_CountHitsByAccess
GO
CREATE PROCEDURE sp_CountHitsByAccess
    @AccessId INT
AS
BEGIN
    SELECT COUNT(*) AS [Count] FROM Hits WHERE AccessId = @AccessId
END
GO

-- ============================================================
-- SP: Obtener dashboard stats del cliente
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetDashboardStats' AND xtype='P')
    DROP PROCEDURE sp_GetDashboardStats
GO
CREATE PROCEDURE sp_GetDashboardStats
    @AccessId INT
AS
BEGIN
    DECLARE @TotalLeads INT, @PendingLeads INT, @Hits INT, @Sequences INT
    
    SELECT @TotalLeads = COUNT(*) FROM Leads WHERE AccessId = @AccessId
    SELECT @PendingLeads = COUNT(*) FROM Leads WHERE AccessId = @AccessId AND [Status] = 'pending'
    SELECT @Hits = COUNT(*) FROM Hits WHERE AccessId = @AccessId
    SELECT @Sequences = COUNT(*) FROM Sequences WHERE AccessId = @AccessId
    
    SELECT
        @TotalLeads AS TotalLeads,
        @PendingLeads AS PendingLeads,
        @Hits AS Hits,
        @Sequences AS TotalSequences
END
GO

-- ============================================================
-- SP: Obtener un lead pendiente (para el bot, consume y marca)
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetNextLead' AND xtype='P')
    DROP PROCEDURE sp_GetNextLead
GO
CREATE PROCEDURE sp_GetNextLead
    @AccessId INT
AS
BEGIN
    DECLARE @LeadId INT, @PhoneNumber VARCHAR(15), @FullName NVARCHAR(200), @Address NVARCHAR(500), @ZipCode VARCHAR(10), @StateCode VARCHAR(2)
    
    SELECT TOP 1 @LeadId = Id, @PhoneNumber = PhoneNumber, @FullName = FullName, @Address = [Address], @ZipCode = ZipCode, @StateCode = StateCode
    FROM Leads WHERE AccessId = @AccessId AND [Status] = 'pending' ORDER BY Id ASC
    
    IF @LeadId IS NOT NULL
    BEGIN
        UPDATE Leads SET [Status] = 'processing', ProcessedAt = GETDATE() WHERE Id = @LeadId
        
        SELECT @LeadId AS LeadId, @PhoneNumber AS PhoneNumber, @FullName AS FullName, @Address AS [Address], @ZipCode AS ZipCode, @StateCode AS StateCode
    END
    ELSE
        SELECT NULL AS LeadId, NULL AS PhoneNumber
END
GO

-- ============================================================
-- SP: Actualizar secuencia (total leads, status)
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_UpdateSequenceStats' AND xtype='P')
    DROP PROCEDURE sp_UpdateSequenceStats
GO
CREATE PROCEDURE sp_UpdateSequenceStats
    @SequenceId INT,
    @TotalLeads INT = NULL,
    @Status VARCHAR(20) = NULL,
    @CompletedAt DATETIME = NULL
AS
BEGIN
    IF @TotalLeads IS NOT NULL
        UPDATE Sequences SET TotalLeads = @TotalLeads WHERE Id = @SequenceId
    IF @Status IS NOT NULL
        UPDATE Sequences SET [Status] = @Status WHERE Id = @SequenceId
    IF @CompletedAt IS NOT NULL
        UPDATE Sequences SET CompletedAt = @CompletedAt WHERE Id = @SequenceId
END
GO

-- ============================================================
-- SP: Obtener AccessId por AccessKey
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_GetAccessIdByKey' AND xtype='P')
    DROP PROCEDURE sp_GetAccessIdByKey
GO
CREATE PROCEDURE sp_GetAccessIdByKey
    @AccessKey VARCHAR(50)
AS
BEGIN
    SELECT AccessId AS Id FROM ClientAccessRecords WHERE AccessKey = @AccessKey
END
GO

-- ============================================================
-- SP: Reclamar siguiente secuencia disponible
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_ClaimSequence' AND xtype='P')
    DROP PROCEDURE sp_ClaimSequence
GO
CREATE PROCEDURE sp_ClaimSequence
    @AccessId INT
AS
BEGIN
    DECLARE @SeqId INT, @Sequence VARCHAR(6), @StateCode VARCHAR(2), @StateName VARCHAR(50)

    SELECT TOP 1 @SeqId = Id, @Sequence = [Sequence], @StateCode = StateCode, @StateName = StateName
    FROM Sequences WHERE AccessId = @AccessId AND [Status] = 'pending' ORDER BY Id ASC

    IF @SeqId IS NOT NULL
    BEGIN
        UPDATE Sequences SET [Status] = 'processing' WHERE Id = @SeqId
        SELECT @SeqId AS Id, @Sequence AS [Sequence], @StateCode AS StateCode, @StateName AS StateName
    END
    ELSE
        SELECT NULL AS Id, NULL AS [Sequence]
END
GO

-- ============================================================
-- SP: Completar secuencia con estadisticas
-- ============================================================
IF EXISTS (SELECT * FROM sysobjects WHERE name='sp_CompleteSequence' AND xtype='P')
    DROP PROCEDURE sp_CompleteSequence
GO
CREATE PROCEDURE sp_CompleteSequence
    @SequenceId INT,
    @TotalLeads INT,
    @Status VARCHAR(20) = 'completed'
AS
BEGIN
    UPDATE Sequences SET TotalLeads = @TotalLeads, [Status] = @Status, CompletedAt = GETDATE() WHERE Id = @SequenceId
END
GO

PRINT 'Stored procedures created successfully'
GO
