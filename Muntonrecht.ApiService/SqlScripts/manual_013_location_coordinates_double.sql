-- manual_013_location_coordinates_double.sql
-- SqlScriptGenerator.MapType() had no mapping for double, so the generated
-- auto_LocationModel.sql created "Latitude"/"Longitude" as TEXT columns.
-- EF/Npgsql reads them as double precision, which fails with:
--   InvalidCastException: Reading as 'System.Double' is not supported for
--   fields having DataTypeName 'text'
-- Convert the existing TEXT columns to double precision. Values written via
-- the CSV upload are numeric strings; NULLIF guards against empty strings.
-- Guarded per column so it also runs harmlessly on fresh databases where the
-- regenerated auto_LocationModel.sql already creates double precision columns.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Locations' AND column_name = 'Latitude' AND data_type = 'text'
    ) THEN
        ALTER TABLE "Locations" ALTER COLUMN "Latitude" TYPE double precision
            USING NULLIF("Latitude", '')::double precision;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Locations' AND column_name = 'Longitude' AND data_type = 'text'
    ) THEN
        ALTER TABLE "Locations" ALTER COLUMN "Longitude" TYPE double precision
            USING NULLIF("Longitude", '')::double precision;
    END IF;
END $$;
