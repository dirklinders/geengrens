-- auto_GameSettingModel.sql creates nullable text columns before manual_010
-- seeds the settings row. manual_011's ADD COLUMN IF NOT EXISTS then skips
-- those existing columns, leaving intro/rules values NULL. EF cannot read
-- these into GameSettingModel's required strings, even on /game/Speluitleg.
-- Empty text means use GameDefaults; preserve all existing non-null content.
UPDATE "GameSettings"
SET "SpeluitlegTitle" = COALESCE("SpeluitlegTitle", ''),
    "SpeluitlegBackstory" = COALESCE("SpeluitlegBackstory", ''),
    "SpeluitlegRules" = COALESCE("SpeluitlegRules", ''),
    "IntroTitle" = COALESCE("IntroTitle", ''),
    "IntroBody" = COALESCE("IntroBody", ''),
    "RulesTitle" = COALESCE("RulesTitle", ''),
    "RulesBody" = COALESCE("RulesBody", '')
WHERE "SpeluitlegTitle" IS NULL OR "SpeluitlegBackstory" IS NULL
   OR "SpeluitlegRules" IS NULL OR "IntroTitle" IS NULL
   OR "IntroBody" IS NULL OR "RulesTitle" IS NULL OR "RulesBody" IS NULL;

-- Defaults also protect future inserts that omit optional custom content.
ALTER TABLE "GameSettings"
    ALTER COLUMN "SpeluitlegTitle" SET DEFAULT '',
    ALTER COLUMN "SpeluitlegTitle" SET NOT NULL,
    ALTER COLUMN "SpeluitlegBackstory" SET DEFAULT '',
    ALTER COLUMN "SpeluitlegBackstory" SET NOT NULL,
    ALTER COLUMN "SpeluitlegRules" SET DEFAULT '',
    ALTER COLUMN "SpeluitlegRules" SET NOT NULL,
    ALTER COLUMN "IntroTitle" SET DEFAULT '',
    ALTER COLUMN "IntroTitle" SET NOT NULL,
    ALTER COLUMN "IntroBody" SET DEFAULT '',
    ALTER COLUMN "IntroBody" SET NOT NULL,
    ALTER COLUMN "RulesTitle" SET DEFAULT '',
    ALTER COLUMN "RulesTitle" SET NOT NULL,
    ALTER COLUMN "RulesBody" SET DEFAULT '',
    ALTER COLUMN "RulesBody" SET NOT NULL;
