-- Apply before deploying the locality-aware API. Existing clients remain unclassified.
IF COL_LENGTH('dbo.Client', 'Locality') IS NULL
BEGIN
    ALTER TABLE dbo.Client ADD Locality nvarchar(100) NULL;
END;
