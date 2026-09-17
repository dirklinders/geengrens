-- manual_008_team_barname.sql
-- Adds BarName column to Teams table (idempotent via IF NOT EXISTS).

ALTER TABLE "Teams" ADD COLUMN IF NOT EXISTS "BarName" TEXT;
