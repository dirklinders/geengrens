namespace Muntonrecht.ApiService.Models;

/// <summary>
/// Built-in fallback texts for the speluitleg page.
/// Admins can override these via the GameSettings row (admin settings page).
/// </summary>
public static class GameDefaults
{
    public const string SpeluitlegTitle = "De Zaak-Muntonrecht";

    public const string SpeluitlegBackstory =
        """
        Zutphen, de nacht van zaterdag op zondag. Tegen beter zeggen in kwam Viktor Vermeer — zakenman, wereldreiziger en de man rond wie heel Muntonrecht draaide — terug naar de stad. Hij had afspraken, rekeningen en oude vetes. Voor middernacht was het voorbij: Viktor werd dood gevonden.

        Maar de zaak klopt van geen kant. Op de plek waar het lichaam lag is veel te weinig bloed gevonden. Rechercheur De Groot is er stellig van overtuigd: Viktor is niet dáár vermoord. Zijn lichaam is na de daad verplaatst. En dat betekent dat de dader tijd, ruimte én een reden had om de sporen te verwarren.

        De politie zit met de handen in het haar. Jullie niet. Jullie zijn de Zaak-Muntonrecht: één avond, één stad, en verdachten die allemaal een reden hadden om te willen dat Viktor Vermeer verdween. Wie deed het, met welk wapen — en waar vond de moord écht plaats?
        """;

    public const string SpeluitlegRules =
        """
        Eén team, één kans. Zo werken jullie onderzoek:

        1. Open de kaart. Elke locatie in de binnenstad huisvest één verdachte.

        2. Ga naar de locatie en scant daar de QR-code of NFC-tag. Daarmee ontgrendelen jullie het gesprek met de verdachte op die plek.

        3. Stel vragen en luister goed. Verdachten laten vanzelf aanwijzingen vallen over wie wat deed, welk voorwerp een rol speelde en waar het écht gebeurde — maar niet iedereen is eerlijk. Por ze, wieg ze, en vergelijk hun verhalen met elkaar.

        4. Zodra jullie álle locaties hebben ontgrendeld, mogen jullie één definitieve aanklacht indienen: wie was de dader, welk wapen werd gebruikt, en op welke plek vond de moord plaats?

        5. Een foutieve aanklacht betekent dat de dader vrijuit gaat. Denk goed na voordat jullie indienen.
        """;

    public const string IntroTitle = "Telegram uit het politiebureau";

    public const string IntroBody =
        """
        AAN: ALLE LEDEN VAN HET ONDERZOEKSTEAM
        VAN: RECHERCHEUR DE GROOT, ZUTPHEN
        ONDERWERP: ZAAK VERMEER — VERTROUWELIJK

        TEAM. GOED LUISTEREN.

        AFGELOPEN NACHT VONDEN WIJ VIKTOR VERMEER DOOD. DE PERS SPREEKT VAN EEN HARTAANVAL. WIJ SPREKEN VAN MOORD.

        HET BLOED OP DE PLEK KLOPT NIET MET HET LICHAAM. HIJ IS DAAR NIET VERMOORD. IEMAND HEEFT HEM NA DE DAAD VERPLAATST. EN DAT KAN NIET ZONDER TIJD, RUST EN EEN GOEDE REDEN.

        JULLIE KRIJGEN DE KAART VAN DE STAD. ELKE LOCATIE HUISVEST EEN VERDACHTE. ONTGRENDEL DE LOCATIES, LEES DE VERKLARINGEN, DOORZOEK DE FOTO'S EN HOUD HET LOGIGRAM BIJ.

        EEN KANS. EEN AANKLACHT. GEEF MIJ DE DADER.

        — DE GROOT
        EINDE BERICHT · STOP
        """;

    public const string RulesTitle = "Spelregels";

    public const string RulesBody =
        """
        Eén team, één kans. Zo werken jullie onderzoek:

        1. Open de kaart. Elke locatie in de binnenstad huisvest één verdachte.

        2. Ga naar de locatie en scan daar de QR-code of NFC-tag. Daarmee ontgrendelen jullie het dossier van die locatie: een politieverklaring of een zoekfoto met vondsten.

        3. Lees goed. In verklaringen zijn delen gezwart — wat eronder staat, blijft verborgen. Vergelijk de verhalen met elkaar: niet iedereen is eerlijk.

        4. Houd het logigram bij. Zet een kruis bij wat uitgesloten is en een vinkje bij wat vaststaat. De aanwijzingen helpen jullie op weg.

        5. Zodra jullie álle locaties hebben ontgrendeld, mogen jullie één definitieve aanklacht indienen: wie was de dader, welk wapen werd gebruikt, en op welke plek vond de moord plaats?

        6. Een foutieve aanklacht betekent dat de dader vrijuit gaat. Denk goed na voordat jullie indienen.
        """;
}
