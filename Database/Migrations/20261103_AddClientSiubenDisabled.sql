-- Apply before deploying scheduled Siuben deactivation.
IF COL_LENGTH('dbo.Client', 'SiubenDisabled') IS NULL
BEGIN
    ALTER TABLE dbo.Client ADD SiubenDisabled bit NOT NULL
        CONSTRAINT DF_Client_SiubenDisabled DEFAULT (0) WITH VALUES;
END;
