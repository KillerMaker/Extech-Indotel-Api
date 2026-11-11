-- Apply before deploying scheduled Siuben activation.
-- For previously activated phase 2 clients, set SiubenActivated = 1 after verification.
IF COL_LENGTH('dbo.Client', 'SiubenActivated') IS NULL
BEGIN
    ALTER TABLE dbo.Client ADD SiubenActivated bit NOT NULL
        CONSTRAINT DF_Client_SiubenActivated DEFAULT (0) WITH VALUES;
END;
