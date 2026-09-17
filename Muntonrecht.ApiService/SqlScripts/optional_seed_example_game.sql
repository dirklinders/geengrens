-- ============================================================================
-- optional_seed_example_game.sql — OPTIONELE DEMO-INHOUD
-- ============================================================================
-- *** NIET AUTOMATISCH UITGEVOERD ***
--
-- Dit script staat NIET in Muntonrecht.ApiService.csproj en wordt daardoor
-- nooit naar de build-output SqlScripts-map gekopieerd waar RunMigrations()
-- bij het opstarten alle *.sql-bestanden uitvoert. Het wordt dus NOOIT
-- automatisch toegepast — alleen als je het zelf uitvoert, bijvoorbeeld:
--
--   psql "<connectionstring>" \
--     -f muntonrecht/Muntonrecht.ApiService/SqlScripts/optional_seed_example_game.sql
--
-- Vereiste: de API is minstens één keer gestart (auto_*.sql en
-- manual_001..012 zijn dan al toegepast).
--
-- Inhoud (voorbeeld-detectivespel, neutrale namen):
--   1. Logigram: 3 standaardcategorieën (suspect/weapon/location) met elk
--      7 voorbeeld-items en 5 voorbeeld-aanwijzingen. De aanwijzingen zijn
--      consistent met het voorbeeld-oplossingsdrieluik
--      "De huiskeeper · Touw · De Wijnkelder" en maken het uniek afleidbaar.
--      De ECHTE oplossing leeft in GameSettings ("MurdererCharacterId" /
--      "MurderWeaponId" / "MurderLocationId"); onderaan staat een uitge-
--      commentarieerde UPDATE die laat zien hoe je die op dit voorbeeld zet.
--   2. Locaties: 2 voorbeeldkaart-locaties met ingevulde
--      ContentType/ContentJson — één "interview" (politieverklaring met
--      [[...]]-redacties) en één "search_picture" (notitie + afbeelding met
--      3 hotspots, afbeelding verwijst naar bestaande /images/pages/hotel.png).
--
-- Idempotent: veilig om twee keer uit te voeren. Elke INSERT is afgeschermd
-- met NOT EXISTS-checks; voorbeeld-items worden alleen toegevoegd aan een
-- categorie die nog leeg is, voorbeeld-aanwijzingen alleen als de
-- aanwijzingentabel leeg is en voorbeeldlocaties alleen als die naam nog
-- niet bestaat. Bestaande (spel)data wordt nooit overschreven of verwijderd.
-- ============================================================================

-- ────────────────────────────────────────────────────────────
-- 1. Logigram-categorieën (alleen toevoegen als de key ontbreekt)
-- ────────────────────────────────────────────────────────────
DO $do$
BEGIN
    IF to_regclass('"LogigramCategorys"') IS NULL THEN
        RAISE NOTICE 'LogigramCategorys ontbreekt — start eerst de API zodat auto_*.sql is toegepast. Logigram-sectie overgeslagen.';
        RETURN;
    END IF;

    INSERT INTO "LogigramCategorys" ("Key", "Name", "SortOrder")
    SELECT 'suspect', 'Verdachten', 0
    WHERE NOT EXISTS (SELECT 1 FROM "LogigramCategorys" WHERE "Key" = 'suspect');

    INSERT INTO "LogigramCategorys" ("Key", "Name", "SortOrder")
    SELECT 'weapon', 'Wapens', 1
    WHERE NOT EXISTS (SELECT 1 FROM "LogigramCategorys" WHERE "Key" = 'weapon');

    INSERT INTO "LogigramCategorys" ("Key", "Name", "SortOrder")
    SELECT 'location', 'Locaties', 2
    WHERE NOT EXISTS (SELECT 1 FROM "LogigramCategorys" WHERE "Key" = 'location');
END
$do$;

-- ────────────────────────────────────────────────────────────
-- 2. Logigram-items: 7 voorbeeld-items per categorie, alleen als die
--    categorie nog GEEN items heeft (bestaande puzzels blijven intact)
-- ────────────────────────────────────────────────────────────
DO $do$
BEGIN
    IF to_regclass('"LogigramEntrys"') IS NULL OR to_regclass('"LogigramCategorys"') IS NULL THEN
        RAISE NOTICE 'Logigram-tabellen ontbreken — items overgeslagen.';
        RETURN;
    END IF;

    -- Verdachten (key = suspect)
    INSERT INTO "LogigramEntrys" ("LogigramCategoryId", "Name", "ImageUrl", "SortOrder")
    SELECT c."Id", v."Name", NULL, v."SortOrder"
    FROM "LogigramCategorys" c
    CROSS JOIN (VALUES
        ('De barman', 0),
        ('De kok', 1),
        ('De burgemeester', 2),
        ('De journaliste', 3),
        ('De huiskeeper', 4),
        ('De tuinman', 5),
        ('De boekhouder', 6)
    ) AS v("Name", "SortOrder")
    WHERE c."Key" = 'suspect'
      AND NOT EXISTS (
          SELECT 1 FROM "LogigramEntrys" e WHERE e."LogigramCategoryId" = c."Id");

    -- Wapens (key = weapon)
    INSERT INTO "LogigramEntrys" ("LogigramCategoryId", "Name", "ImageUrl", "SortOrder")
    SELECT c."Id", v."Name", NULL, v."SortOrder"
    FROM "LogigramCategorys" c
    CROSS JOIN (VALUES
        ('Kandelaar', 0),
        ('Mes', 1),
        ('Vergif', 2),
        ('Touw', 3),
        ('Pistool', 4),
        ('Bronzen beeld', 5),
        ('Briefopener', 6)
    ) AS v("Name", "SortOrder")
    WHERE c."Key" = 'weapon'
      AND NOT EXISTS (
          SELECT 1 FROM "LogigramEntrys" e WHERE e."LogigramCategoryId" = c."Id");

    -- Locaties (key = location)
    INSERT INTO "LogigramEntrys" ("LogigramCategoryId", "Name", "ImageUrl", "SortOrder")
    SELECT c."Id", v."Name", NULL, v."SortOrder"
    FROM "LogigramCategorys" c
    CROSS JOIN (VALUES
        ('Bibliotheek', 0),
        ('Keuken', 1),
        ('Kas', 2),
        ('Wijnkelder', 3),
        ('Werkkamer', 4),
        ('Serre', 5),
        ('Zolder', 6)
    ) AS v("Name", "SortOrder")
    WHERE c."Key" = 'location'
      AND NOT EXISTS (
          SELECT 1 FROM "LogigramEntrys" e WHERE e."LogigramCategoryId" = c."Id");
END
$do$;

-- ────────────────────────────────────────────────────────────
-- 3. Voorbeeld-aanwijzingen (5), alleen als de aanwijzingentabel leeg is.
--
--    Voorbeeld-oplossing: DE HUISKEEPER · TOUW · WIJNKELDER
--    Redeneerketen (strikt afleidbaar):
--      Aanwijzing 1 elimineert barman, kok, burgemeester (alibi in de
--        feestzaal) → journaliste, huiskeeper, tuinman, boekhouder over.
--      Aanwijzing 2 elimineert journaliste (trein 20:30) en tuinman
--        (kas, drie getuigen) → huiskeeper, boekhouder over.
--      Aanwijzing 3 elimineert de boekhouder (ondertekening bij de
--        notaris om 21:00, het overlijdstip) → dader: DE HUISKEEPER.
--      Aanwijzing 4 elimineert mes, briefopener (geen snijwonden),
--        vergif (bloedonderzoek), pistool (geen kogel), kandelaar en
--        beeld (geen blauwe plekken) → wapen: TOUW (gewurgd).
--      Aanwijzing 5 elimineert bibliotheek, keuken, kas, werkkamer,
--        serre en zolder (op slot wegens renovatie) → plek: WIJNKELDER.
-- ────────────────────────────────────────────────────────────
DO $do$
BEGIN
    IF to_regclass('"LogigramClues"') IS NULL THEN
        RAISE NOTICE 'LogigramClues ontbreekt — aanwijzingen overgeslagen.';
        RETURN;
    END IF;

    INSERT INTO "LogigramClues" ("Text", "SortOrder")
    SELECT v."Text", v."SortOrder"
    FROM (VALUES
        ('Op de avond van de moord waren de barman, de kok en de burgemeester tot middernacht samen in de feestzaal voor een proeverij van streekgerechten — drie buren bevestigen dit. Geen van de drie is de dader.', 0),
        ('De journaliste vertrok om 20:30 met de trein naar Amsterdam; het kaartje ligt in de administratie van het station. De tuinman zette tot middernacht met drie buren bloembollen in de kas. Zij zijn onschuldig.', 1),
        ('De lijkschouwer stelt het tijdstip van overlijden vast op exact 21:00. Om die tijd ondertekende de boekhouder de nalatenschap bij de notaris in Zutphen — een uur reizen van het landhuis. De boekhouder is onschuldig.', 2),
        ('Het lijkschouwersrapport: geen snijwonden (geen mes, geen briefopener), geen sporen van vergif in het bloed, geen kogelwond en geen blauwe plekken zoals een kandelaar of een beeld die achterlaten. Het slachtoffer is gewurgd.', 3),
        ('Wegens de renovatie waren die avond zes van de zeven kamers van binnen op slot: de bibliotheek, de keuken, de kas, de werkkamer, de serre en de zolder. Alleen de wijnkelder was die avond toegankelijk.', 4)
    ) AS v("Text", "SortOrder")
    WHERE NOT EXISTS (SELECT 1 FROM "LogigramClues");
END
$do$;

-- ────────────────────────────────────────────────────────────
-- 4. Voorbeeldlocaties (kaart + onderzoeksdossier), alleen toegevoegd als
--    er nog GEEN locatie met die naam bestaat.
--
--    NB: Locations.CharacterId verwijst naar Characters (FK). Beide
--    voorbeeldlocaties worden daarom alleen ingevoegd als er minstens één
--    character bestaat; ze krijgen dan de eerst aanwezige verdachte. Wijs
--    via /admin/locations zelf de gewenste verdachte per locatie toe.
-- ────────────────────────────────────────────────────────────
DO $do$
BEGIN
    IF to_regclass('"Locations"') IS NULL THEN
        RAISE NOTICE 'Locations ontbreekt — start eerst de API. Locaties overgeslagen.';
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "Characters") THEN
        RAISE NOTICE 'Geen characters gevonden: voorbeeldlocaties hebben een verdachte nodig (Locations.CharacterId). Maak eerst verdachten aan in /admin/characters en voer dit script daarna opnieuw uit.';
        RETURN;
    END IF;

    -- 4a. Interview-locatie: politieverklaring (transcript) met [[...]]-redacties.
    --     Readable hint-phrases koppelen bewust aan de voorbeeldpuzzel
    --     (wijnkelder, renovatie, 21:00). ContentJson-contract:
    --     { "header", "meta", "lines": [{ "speaker", "text" }] }
    INSERT INTO "Locations" ("Name", "Description", "ContentType", "ContentJson", "Latitude", "Longitude", "CharacterId")
    SELECT
        'De Wijnkelder',
        'De koele kelder onder het landhuis, waar voor het feest de drank werd bewaard.',
        'interview',
        '{"header":"Politie-interview · Verklaring #3","meta":"Afdeling Zutphen · 25-04-2026 · 23:40","lines":[{"speaker":"Rechercheur De Groot","text":"Waar was u tussen [[20:00]] en [[22:00]]?"},{"speaker":"De tuinman","text":"In de kas, met drie buren. Wij zetten [[bloembollen]]. U kunt hen bellen."},{"speaker":"Rechercheur De Groot","text":"Heeft u iets gehoord?"},{"speaker":"De tuinman","text":"Rond [[21:00]] een deur die dichtklapte. En iets [[zwaars]] dat door de gang werd gesleept."},{"speaker":"Rechercheur De Groot","text":"Weet u welke deuren op slot waren wegens de renovatie?"},{"speaker":"De tuinman","text":"Allemaal, denk ik. Behalve de [[wijnkelder]]. Daar stond het bier koud voor het feest."},{"speaker":"Rechercheur De Groot","text":"Heeft u de huiskeeper die avond gezien?"},{"speaker":"De tuinman","text":"Even, tegen [[negen uur]]. Zij droeg [[een emmer]] richting het achterhuis."}]}',
        52.1392,
        6.2014,
        (SELECT MIN("Id") FROM "Characters")
    WHERE NOT EXISTS (SELECT 1 FROM "Locations" WHERE "Name" = 'De Wijnkelder');

    -- 4b. Zoekfoto-locatie: notitie + afbeelding met 3 hotspots
    --     (x/y zijn percentages van de afbeelding; hotel.png bestaat in
    --     Muntonrecht-FE/public/images/pages/). ContentJson-contract:
    --     { "note", "image", "hotspots": [{ "id", "x", "y", "label", "detail" }] }
    INSERT INTO "Locations" ("Name", "Description", "ContentType", "ContentJson", "Latitude", "Longitude", "CharacterId")
    SELECT
        'De Werkkamer',
        'De studeerkamer van het slachtoffer, gedeeltelijk leeggeruimd door de renovatie.',
        'search_picture',
        '{"note":"Politievondst: de werkkamer van het slachtoffer doorzocht op 25-04-2026 om 22:15.","image":"/images/pages/hotel.png","hotspots":[{"id":"hs1","x":30,"y":55,"label":"Sleutelbos","detail":"Een sleutelbos met [[zes]] sleutels — voor elke kamer van de renovatie één. De sleutel van de wijnkelder ontbreekt."},{"id":"hs2","x":62,"y":38,"label":"Notitieboekje","detail":"Notitie van het slachtoffer: afspraak om [[21:00]] met de huiskeeper, over [[de voorraad]] in de kelder."},{"id":"hs3","x":78,"y":70,"label":"Omgevallen stoel","detail":"Een stoel ligt om; op de rugleuning zitten [[vezels]] van touw."}]}',
        52.1415,
        6.1989,
        (SELECT MIN("Id") FROM "Characters")
    WHERE NOT EXISTS (SELECT 1 FROM "Locations" WHERE "Name" = 'De Werkkamer');
END
$do$;

-- ────────────────────────────────────────────────────────────
-- 5. OPTIONEEL: de echte oplossing koppelen aan het voorbeeld-logigram.
--
--    Het voorbeeld-puzzeldrieluik is: De huiskeeper · Touw · De Wijnkelder.
--    De oplossing leeft in GameSettings en verwijst naar de ID's van
--    Characters/Weapons/Locations (NIET naar logigram-items — de koppeling
--    gaat via gelijke namen, zoals ook in /admin/logigram wordt gemeld).
--    Maak eerst entiteiten met precies deze namen aan (via /admin/characters,
--    /admin/weapons en /admin/locations), en verwijder daarna de commentaar-
--    streepjes hieronder. De EXISTS-guards voorkomen dat de oplossing op 0
--    wordt gezet wanneer een van de entiteiten nog ontbreekt.
--
-- UPDATE "GameSettings"
-- SET "MurdererCharacterId" = (SELECT "Id" FROM "Characters" WHERE "Name" = 'De huiskeeper'),
--     "MurderWeaponId"      = (SELECT "Id" FROM "Weapons"    WHERE "Name" = 'Touw'),
--     "MurderLocationId"    = (SELECT "Id" FROM "Locations"  WHERE "Name" = 'De Wijnkelder')
-- WHERE EXISTS (SELECT 1 FROM "Characters" WHERE "Name" = 'De huiskeeper')
--   AND EXISTS (SELECT 1 FROM "Weapons"    WHERE "Name" = 'Touw')
--   AND EXISTS (SELECT 1 FROM "Locations"  WHERE "Name" = 'De Wijnkelder');
-- ────────────────────────────────────────────────────────────
