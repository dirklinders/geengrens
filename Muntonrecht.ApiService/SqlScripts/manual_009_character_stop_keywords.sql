-- manual_009_character_stop_keywords.sql
-- Adds stop-condition keyword columns to Characters table (idempotent via IF NOT EXISTS).

ALTER TABLE "Characters" ADD COLUMN IF NOT EXISTS "StopKeywordAlibi"      TEXT;
ALTER TABLE "Characters" ADD COLUMN IF NOT EXISTS "StopKeywordConnection"  TEXT;
ALTER TABLE "Characters" ADD COLUMN IF NOT EXISTS "StopKeywordHint"        TEXT;
