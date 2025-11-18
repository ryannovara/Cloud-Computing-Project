-- Add new columns to Games table for validation
ALTER TABLE Games ADD Year INT NULL;
ALTER TABLE Games ADD Publisher NVARCHAR(200) NULL;
