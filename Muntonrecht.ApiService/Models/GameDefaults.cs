namespace Muntonrecht.ApiService.Models;

/// <summary>
/// Built-in fallback texts for the speluitleg page.
/// Admins can override these via the GameSettings row (admin settings page).
/// </summary>
public static class GameDefaults
{
    public const string SpeluitlegTitle = "De Zaak Thieme";

    public const string SpeluitlegBackstory =
        """
        In 2001 werd het lichaam van Willem Thieme in Zutphen uit het water gehaald. Er waren nauwelijks aanknopingspunten. Nu de onderzoekstermijn van 25 jaar voor deze cold case bijna verstreken is, doet de politie een laatste oproep aan het publiek: help de zaak op te lossen.

        Zeven mogelijke moordlocaties, zeven verdachten — van wie één nergens bij naam wordt genoemd — en zeven mogelijke moordwapens. Achterhaal wie zich in de nacht van 10 oktober 2001 waar bevond en welk mogelijk wapen diegene bij zich droeg. Misschien komt de moordenaar zo vanzelf aan het licht.

        De politie heeft alle informatie vrijgegeven die zij wettelijk mag delen. De dossiers liggen klaar. Het onderzoek is aan jullie. Veel succes.
        """;

    public const string SpeluitlegRules = RulesBody;

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
        Eén team, één kans. Zo werkt jullie onderzoek:

        1. Open de kaart. Elke locatie bevat informatie van de politie.

        2. Ga naar de locatie en scan daar de NFC-tag. Daarmee ontgrendelen jullie het dossier van die locatie.

        3. Lees goed. In verklaringen zijn delen zwart gelakt — wat eronder staat, blijft verborgen.

        4. Houd het logigram bij. Zet een kruis bij wat uitgesloten is en een vinkje bij wat vaststaat. De dossiers helpen jullie op weg.

        5. Zodra jullie álle locaties hebben ontgrendeld, mogen jullie één definitieve aanklacht indienen: wie was de dader, welk wapen werd gebruikt, en op welke plek vond de moord plaats?

        6. Een foutieve aanklacht betekent dat de dader vrijuit gaat. Denk goed na voordat jullie indienen.
        """;
}
