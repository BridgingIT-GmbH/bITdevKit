// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;

/// <summary>
///     Represents a currency with various predefined currency instances.
/// </summary>
public class Currency : ValueObject
{
    private static readonly Dictionary<string, string> Currencies;

    static Currency()
    {
        // source: https://www.xe.com/symbols/
        Currencies = new Dictionary<string, string>
        {
            { "ALL", "Lek" },
            { "AFN", "؋" },
            { "ARS", "$" },
            { "AWG", "ƒ" },
            { "AUD", "$" },
            { "AZN", "₼" },
            { "BSD", "$" },
            { "BBD", "$" },
            { "BYN", "Br" },
            { "BZD", "BZ$" },
            { "BMD", "$" },
            { "BOB", "$b" },
            { "BAM", "KM" },
            { "BWP", "P" },
            { "BGN", "лв" },
            { "BRL", "R$" },
            { "BND", "$" },
            { "KHR", "៛" },
            { "CAD", "$" },
            { "KYD", "$" },
            { "CLP", "$" },
            { "CNY", "¥" },
            { "COP", "$" },
            { "CRC", "₡" },
            { "HRK", "kn" },
            { "CUP", "₱" },
            { "CZK", "Kč" },
            { "DKK", "kr" },
            { "DOP", "RD$" },
            { "XCD", "$" },
            { "EGP", "£" },
            { "SVC", "$" },
            { "EUR", "€" },
            { "FKP", "£" },
            { "FJD", "$" },
            { "GHS", "¢" },
            { "GIP", "£" },
            { "GTQ", "Q" },
            { "GGP", "£" },
            { "GYD", "$" },
            { "HNL", "L" },
            { "HKD", "$" },
            { "HUF", "Ft" },
            { "ISK", "kr" },
            { "INR", "₹" },
            { "IDR", "Rp" },
            { "IRR", "﷼" },
            { "IMP", "£" },
            { "ILS", "₪" },
            { "JMD", "J$" },
            { "JPY", "¥" },
            { "JEP", "£" },
            { "KZT", "лв" },
            { "KPW", "₩" },
            { "KRW", "₩" },
            { "KGS", "лв" },
            { "LAK", "₭" },
            { "LBP", "£" },
            { "LRD", "$" },
            { "MKD", "ден" },
            { "MYR", "RM" },
            { "MUR", "₨" },
            { "MXN", "$" },
            { "MNT", "₮" },
            { "MZN", "MT" },
            { "NAD", "$" },
            { "NPR", "₨" },
            { "ANG", "ƒ" },
            { "NZD", "$" },
            { "NIO", "C$" },
            { "NGN", "₦" },
            { "NOK", "kr" },
            { "OMR", "﷼" },
            { "PKR", "₨" },
            { "PAB", "B/." },
            { "PYG", "Gs" },
            { "PEN", "S/." },
            { "PHP", "₱" },
            { "PLN", "zł" },
            { "QAR", "﷼" },
            { "RON", "lei" },
            { "RUB", "₽" },
            { "SHP", "£" },
            { "SAR", "﷼" },
            { "RSD", "Дин." },
            { "SCR", "₨" },
            { "SGD", "$" },
            { "SBD", "$" },
            { "SOS", "S" },
            { "ZAR", "R" },
            { "LKR", "₨" },
            { "SEK", "kr" },
            { "CHF", "CHF" },
            { "SRD", "$" },
            { "SYP", "£" },
            { "TWD", "NT$" },
            { "THB", "฿" },
            { "TTD", "TT$" },
            { "TRY", "₺" },
            { "TVD", "$" },
            { "UAH", "₴" },
            { "GBP", "£" },
            { "USD", "$" },
            { "UYU", "$U" },
            { "UZS", "лв" },
            { "VEF", "Bs" },
            { "VND", "₫" },
            { "YER", "﷼" },
            { "ZWD", "Z$" }
        };
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Currency" /> class.
    ///     Represents a currency in the system.
    /// </summary>
    private Currency() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Currency" /> class.
    ///     The Currency class represents various world currencies.
    /// </summary>
    /// <remarks>
    ///     This class is equipped with properties representing different currencies,
    ///     each currency identified by its ISO code.
    /// </remarks>
    private Currency(string code)
    {
        this.Code = code;
    }

    /// <summary>
    ///     Represents the currency Albania Lek with the currency code "ALL".
    /// </summary>
    public static Currency AlbaniaLek => Create("ALL");

    /// <summary>
    ///     Gets the currency instance for the Afghanistan Afghani (AFN).
    /// </summary>
    public static Currency AfghanistanAfghani => Create("AFN");

    /// <summary>
    ///     Represents the Argentina Peso currency.
    /// </summary>
    public static Currency ArgentinaPeso => Create("ARS");

    /// <summary>
    ///     Represents the official currency of Aruba, denoted by the code "AWG".
    /// </summary>
    public static Currency ArubaGuilder => Create("AWG");

    /// <summary>
    ///     Represents the Australian Dollar (AUD) currency.
    /// </summary>
    public static Currency AustraliaDollar => Create("AUD");

    /// <summary>
    ///     Gets the currency instance representing the Azerbaijan Manat.
    /// </summary>
    /// <value>
    ///     The currency object for Azerbaijan Manat.
    /// </value>
    public static Currency AzerbaijanManat => Create("AZN");

    /// <summary>
    ///     Gets the Currency instance for the Bahamas Dollar (BSD).
    /// </summary>
    /// <value>
    ///     A Currency object representing the Bahamas Dollar.
    /// </value>
    public static Currency BahamasDollar => Create("BSD");

    /// <summary>
    ///     Represents the currency for Barbados, designated by the ISO 4217 code "BBD".
    /// </summary>
    public static Currency BarbadosDollar => Create("BBD");

    /// <summary>
    ///     Gets the instance of the currency used in Belarus, represented by the ISO code "BYN".
    /// </summary>
    public static Currency BelarusRuble => Create("BYN");

    /// <summary>
    ///     Represents the currency used in Belize.
    /// </summary>
    /// <remarks>
    ///     The code for Belize Dollar is BZD.
    /// </remarks>
    public static Currency BelizeDollar => Create("BZD");

    /// <summary>
    ///     Represents the currency for Bermuda Dollar.
    /// </summary>
    /// <remarks>
    ///     The ISO 4217 code for Bermuda Dollar is "BMD".
    /// </remarks>
    public static Currency BermudaDollar => Create("BMD");

    /// <summary>
    ///     Represents the currency of Bolivia, known as the Bolíviano, with the ISO code "BOB".
    /// </summary>
    public static Currency BoliviaBolíviano => Create("BOB");

    /// <summary>
    ///     Represents the currency for Bosnia and Herzegovina.
    /// </summary>
    public static Currency BosniaandHerzegovinaMark => Create("BAM");

    /// <summary>
    ///     Provides the currency instance for Botswana Pula (BWP).
    /// </summary>
    public static Currency BotswanaPula => Create("BWP");

    /// <summary>
    ///     Represents the currency for Bulgaria, known as the Lev (BGN).
    /// </summary>
    public static Currency BulgariaLev => Create("BGN");

    /// <summary>
    ///     Gets the instance of the Brazil Real currency.
    /// </summary>
    /// <returns>The Brazil Real currency instance.</returns>
    public static Currency BrazilReal => Create("BRL");

    /// <summary>
    ///     Currency: Brunei Darussalam Dollar (BND)
    /// </summary>
    public static Currency BruneiDarussalamDollar => Create("BND");

    /// <summary>
    ///     Represents the currency of Cambodia, the Riel (KHR).
    /// </summary>
    public static Currency CambodiaRiel => Create("KHR");

    /// <summary>
    ///     Provides a static instance of the Canada Dollar (CAD) currency.
    /// </summary>
    public static Currency CanadaDollar => Create("CAD");

    /// <summary>
    ///     Represents the currency for Cayman Islands Dollar.
    /// </summary>
    public static Currency CaymanIslandsDollar => Create("KYD");

    /// <summary>
    ///     Represents the currency of Chile, denominated as the Peso (CLP).
    /// </summary>
    public static Currency ChilePeso => Create("CLP");

    /// <summary>
    ///     Represents the currency China Yuan Renminbi (CNY).
    /// </summary>
    public static Currency ChinaYuanRenminbi => Create("CNY");

    /// <summary>
    ///     Represents the currency of Colombia, denoted by the code "COP".
    /// </summary>
    public static Currency ColombiaPeso => Create("COP");

    /// <summary>
    ///     Gets the Costa Rican Colón currency.
    /// </summary>
    public static Currency CostaRicaColon => Create("CRC");

    /// <summary>
    ///     Represents the currency "Croatia Kuna" with the currency code "HRK".
    /// </summary>
    public static Currency CroatiaKuna => Create("HRK");

    /// <summary>
    ///     Represents the Cuban Peso currency.
    /// </summary>
    public static Currency CubaPeso => Create("CUP");

    /// <summary>
    ///     Represents the Czech Republic Koruna currency.
    /// </summary>
    public static Currency CzechRepublicKoruna => Create("CZK");

    /// <summary>
    ///     Represents the currency for Denmark, the Krone, with the currency code "DKK".
    /// </summary>
    public static Currency DenmarkKrone => Create("DKK");

    /// <summary>
    ///     Represents the currency for the Dominican Republic.
    /// </summary>
    public static Currency DominicanRepublicPeso => Create("DOP");

    /// <summary>
    ///     Represents the East Caribbean Dollar currency.
    /// </summary>
    public static Currency EastCaribbeanDollar => Create("XCD");

    /// <summary>
    ///     Gets the Currency instance for the Egyptian Pound (EGP).
    /// </summary>
    public static Currency EgyptPound => Create("EGP");

    /// <summary>
    ///     Gets the currency instance for the El Salvador Colon (SVC).
    /// </summary>
    public static Currency ElSalvadorColon => Create("SVC");

    /// <summary>
    ///     Gets the Euro currency.
    /// </summary>
    /// <value>
    ///     A <see cref="Currency" /> that represents the Euro (EUR).
    /// </value>
    public static Currency Euro => Create("EUR");

    /// <summary>
    ///     Represents the currency used in the Falkland Islands.
    /// </summary>
    public static Currency FalklandIslands => Create("FKP");

    /// <summary>
    ///     Represents the currency for Fiji Dollar.
    /// </summary>
    public static Currency FijiDollar => Create("FJD");

    /// <summary>
    ///     Represents the Ghanaian Cedi currency.
    /// </summary>
    public static Currency GhanaCedi => Create("GHS");

    /// <summary>
    ///     Represents the currency Gibraltar Pound, identified by the ISO code "GIP".
    /// </summary>
    public static Currency GibraltarPound => Create("GIP");

    /// <summary>
    ///     Represents the currency for Guatemala, the Quetzal.
    /// </summary>
    public static Currency GuatemalaQuetzal => Create("GTQ");

    /// <summary>
    ///     Represents the currency of Guernsey.
    /// </summary>
    public static Currency GuernseyPound => Create("GGP");

    /// <summary>
    ///     Represents the currency for Guyana Dollar (GYD).
    /// </summary>
    public static Currency GuyanaDollar => Create("GYD");

    /// <summary>
    ///     Represents the official currency of Honduras, the Lempira.
    ///     Its ISO 4217 currency code is "HNL".
    /// </summary>
    public static Currency HondurasLempira => Create("HNL");

    /// <summary>
    ///     Represents the Hong Kong Dollar currency.
    /// </summary>
    public static Currency HongKongDollar => Create("HKD");

    /// <summary>
    ///     Represents the currency of Hungary, the Hungarian Forint (HUF).
    /// </summary>
    public static Currency HungaryForint => Create("HUF");

    /// <summary>
    ///     Represents the currency of Iceland.
    /// </summary>
    /// <remarks>
    ///     ISO 4217 code: ISK.
    /// </remarks>
    public static Currency IcelandKrona => Create("ISK");

    /// <summary>
    ///     Gets the currency instance for the Indian Rupee (INR).
    /// </summary>
    public static Currency IndiaRupee => Create("INR");

    /// <summary>
    ///     Represents the official currency of Indonesia.
    /// </summary>
    public static Currency IndonesiaRupiah => Create("IDR");

    /// <summary>
    ///     Represents the Iran Rial currency.
    /// </summary>
    public static Currency IranRial => Create("IRR");

    /// <summary>
    ///     Gets the currency instance for the Isle of Man Pound (IMP).
    /// </summary>
    public static Currency IsleofManPound => Create("IMP");

    /// <summary>
    ///     Represents the currency for Israel Shekel.
    /// </summary>
    public static Currency IsraelShekel => Create("ILS");

    /// <summary>
    ///     Represents the currency of Jamaica, identified by the ISO code "JMD".
    /// </summary>
    public static Currency JamaicaDollar => Create("JMD");

    /// <summary>
    ///     Represents the currency for Japanese Yen (JPY).
    /// </summary>
    public static Currency JapanYen => Create("JPY");

    /// <summary>
    ///     Represents the Jersey Pound currency.
    /// </summary>
    public static Currency JerseyPound => Create("JEP");

    /// <summary>
    ///     Represents the currency used in Kazakhstan, known as the Tenge.
    /// </summary>
    public static Currency KazakhstanTenge => Create("KZT");

    /// <summary>
    ///     Represents the currency used in North Korea.
    /// </summary>
    public static Currency KoreaNorth => Create("KPW");

    /// <summary>
    ///     Gets the South Korean Won (KRW) currency.
    /// </summary>
    public static Currency KoreaSouth => Create("KRW");

    /// <summary>
    ///     Represents the currency Kyrgyzstan Som with the ISO code "KGS".
    /// </summary>
    public static Currency KyrgyzstanSom => Create("KGS");

    /// <summary>
    ///     Gets the currency instance for the Laos Kip (LAK).
    /// </summary>
    public static Currency LaosKip => Create("LAK");

    /// <summary>
    ///     Represents the currency for Lebanon known as the Lebanon Pound (code: LBP).
    /// </summary>
    public static Currency LebanonPound => Create("LBP");

    /// <summary>
    ///     Represents the official currency of Liberia.
    /// </summary>
    public static Currency LiberiaDollar => Create("LRD");

    /// <summary>
    ///     Represents the currency of Macedonia, denoted by the code MKD.
    /// </summary>
    public static Currency MacedoniaDenar => Create("MKD");

    /// <summary>
    ///     Represents the currency of Malaysia, identified by the ISO code "MYR".
    /// </summary>
    public static Currency MalaysiaRinggit => Create("MYR");

    /// <summary>
    ///     Gets the currency instance for the Mauritius Rupee (MUR).
    /// </summary>
    public static Currency MauritiusRupee => Create("MUR");

    /// <summary>
    ///     Represents the currency type for the Mexican Peso.
    /// </summary>
    public static Currency MexicoPeso => Create("MXN");

    /// <summary>
    ///     Represents the currency of Mongolia, known as the Tughrik.
    /// </summary>
    /// <remarks>
    ///     This property returns a <see cref="Currency" /> object with the ISO 4217 currency code "MNT".
    /// </remarks>
    public static Currency MongoliaTughrik => Create("MNT");

    /// <summary>
    ///     Gets the Currency instance representing Mozambique Metical (MZN).
    /// </summary>
    public static Currency MozambiqueMetical => Create("MZN");

    /// <summary>
    ///     Represents the currency used in Namibia.
    /// </summary>
    public static Currency NamibiaDollar => Create("NAD");

    /// <summary>
    ///     Represents the currency of Nepal, specifically the Nepalese Rupee (NPR).
    /// </summary>
    public static Currency NepalRupee => Create("NPR");

    /// <summary>
    ///     Represents the currency of Netherlands Antilles, denoted by the code "ANG".
    /// </summary>
    public static Currency NetherlandsAntillesGuilder => Create("ANG");

    /// <summary>
    ///     Represents the New Zealand Dollar currency.
    ///     Currency code: NZD.
    /// </summary>
    public static Currency NewZealandDollar => Create("NZD");

    /// <summary>
    ///     Represents the currency code for the Nicaraguan Córdoba.
    /// </summary>
    public static Currency NicaraguaCordoba => Create("NIO");

    /// <summary>
    ///     Represents the Nigerian Naira currency.
    /// </summary>
    public static Currency NigeriaNaira => Create("NGN");

    /// <summary>
    ///     Represents the currency used in Norway, identified by the currency code 'NOK'.
    /// </summary>
    public static Currency NorwayKrone => Create("NOK");

    /// <summary>
    ///     Represents the currency for Oman Rial.
    /// </summary>
    public static Currency OmanRial => Create("OMR");

    /// <summary>
    ///     Represents the currency for Pakistan Rupee (PKR).
    /// </summary>
    public static Currency PakistanRupee => Create("PKR");

    /// <summary>
    ///     Represents the currency for Panama Balboa (PAB).
    /// </summary>
    public static Currency PanamaBalboa => Create("PAB");

    /// <summary>
    ///     Represents the currency of Paraguay, known as the Guarani.
    /// </summary>
    public static Currency ParaguayGuarani => Create("PYG");

    /// <summary>
    ///     Represents the official currency of Peru, known as the Sol.
    /// </summary>
    public static Currency PeruSol => Create("PEN");

    /// <summary>
    ///     Represents the currency of the Philippines Peso.
    /// </summary>
    public static Currency PhilippinesPeso => Create("PHP");

    /// <summary>
    ///     Represents the currency of Poland, the Zloty (PLN).
    /// </summary>
    public static Currency PolandZloty => Create("PLN");

    /// <summary>
    ///     Represents the currency used in Qatar (Qatari Riyal).
    /// </summary>
    public static Currency QatarRiyal => Create("QAR");

    /// <summary>
    ///     Gets the Romania Leu currency.
    /// </summary>
    public static Currency RomaniaLeu => Create("RON");

    /// <summary>
    ///     Gets the Russia Ruble (RUB) currency.
    /// </summary>
    /// <remarks>
    ///     This property represents the official currency of Russia and uses the ISO 4217 currency code "RUB".
    /// </remarks>
    public static Currency RussiaRuble => Create("RUB");

    /// <summary>
    ///     Represents the currency of Saint Helena Pound (SHP).
    /// </summary>
    public static Currency SaintHelenaPound => Create("SHP");

    /// <summary>
    ///     Represents the currency of Saudi Arabia.
    /// </summary>
    /// <value>
    ///     A <see cref="Currency" /> instance representing the Saudi Arabia Riyal (SAR).
    /// </value>
    public static Currency SaudiArabiaRiyal => Create("SAR");

    /// <summary>
    ///     Gets the currency instance for Serbia Dinar (RSD).
    /// </summary>
    public static Currency SerbiaDinar => Create("RSD");

    /// <summary>
    ///     Represents the currency for Seychelles Rupee.
    /// </summary>
    public static Currency SeychellesRupee => Create("SCR");

    /// <summary>
    ///     Represents the currency code for Singapore Dollar.
    /// </summary>
    public static Currency SingaporeDollar => Create("SGD");

    /// <summary>
    ///     Represents the currency in use in the Solomon Islands.
    /// </summary>
    public static Currency SolomonIslandsDollar => Create("SBD");

    /// <summary>
    ///     Represents the currency of Somalia, known as the Somalia Shilling.
    ///     The currency code for Somalia Shilling is "SOS".
    /// </summary>
    public static Currency SomaliaShilling => Create("SOS");

    /// <summary>
    ///     Represents the South African Rand (ZAR) currency.
    /// </summary>
    public static Currency SouthAfricaRand => Create("ZAR");

    /// <summary>
    ///     Gets the currency instance for the Sri Lankan Rupee (LKR).
    /// </summary>
    public static Currency SriLankaRupee => Create("LKR");

    /// <summary>
    ///     Represents the currency used in Sweden, known as the Swedish Krona (SEK).
    /// </summary>
    public static Currency SwedenKrona => Create("SEK");

    /// <summary>
    ///     Represents the currency of Switzerland Franc (CHF).
    /// </summary>
    public static Currency SwitzerlandFranc => Create("CHF");

    /// <summary>
    ///     Represents the currency of Suriname, identified by the currency code "SRD".
    /// </summary>
    public static Currency SurinameDollar => Create("SRD");

    /// <summary>
    ///     Gets the Syria Pound currency.
    /// </summary>
    public static Currency SyriaPound => Create("SYP");

    /// <summary>
    ///     Represents the currency for Taiwan New Dollar.
    /// </summary>
    public static Currency TaiwanNewDollar => Create("TWD");

    /// <summary>
    ///     Represents the currency of Thailand, known as the Baht.
    ///     The ISO 4217 currency code for Thailand Baht is THB.
    /// </summary>
    public static Currency ThailandBaht => Create("THB");

    /// <summary>
    ///     Represents the Trinidad and Tobago Dollar (TTD) currency.
    /// </summary>
    public static Currency TrinidadandTobagoDollar => Create("TTD");

    /// <summary>
    ///     Represents the currency of Turkey.
    ///     This property returns the Turkish Lira currency represented by the ISO 4217 currency code "TRY".
    /// </summary>
    public static Currency TurkeyLira => Create("TRY");

    /// <summary>
    ///     Represents the currency of Tuvalu, denoted by the code "TVD".
    /// </summary>
    public static Currency TuvaluDollar => Create("TVD");

    /// <summary>
    ///     Represents the currency for Ukraine known as the Hryvnia (UAH).
    /// </summary>
    public static Currency UkraineHryvnia => Create("UAH");

    /// <summary>
    ///     Represents the currency for United Kingdom - Pound Sterling (GBP).
    /// </summary>
    /// <returns>A Currency object representing the British Pound (GBP).</returns>
    public static Currency GbPound => Create("GBP");

    /// <summary>
    ///     Represents the currency: United States Dollar (USD).
    /// </summary>
    public static Currency UsDollar => Create("USD");

    /// <summary>
    ///     Represents the currency for Uruguay Peso.
    /// </summary>
    public static Currency UruguayPeso => Create("UYU");

    /// <summary>
    ///     Represents the currency of Uzbekistan, the Uzbekistan Som.
    /// </summary>
    public static Currency UzbekistanSom => Create("UZS");

    /// <summary>
    ///     Provides a <see cref="Currency" /> instance representing the Venezuelan Bolívar.
    /// </summary>
    public static Currency VenezuelaBolivar => Create("VEF");

    /// <summary>
    ///     Represents the currency Vietnamese Dong (VND).
    /// </summary>
    public static Currency VietNamDong => Create("VND");

    /// <summary>
    ///     Represents the currency for Yemen Rial (YER).
    /// </summary>
    public static Currency YemenRial => Create("YER");

    /// <summary>
    ///     Represents the currency of Zimbabwe, symbolized by the code "ZWD".
    /// </summary>
    public static Currency ZimbabweDollar => Create("ZWD");

    /// <summary>
    ///     Gets the ISO 4217 currency code representing the currency.
    /// </summary>
    public string Code { get; protected set; }

    /// <summary>
    ///     Gets the symbol associated with the currency code.
    /// </summary>
    /// <remarks>
    ///     The symbol is derived from a dictionary containing supported currency codes and their corresponding symbols.
    /// </remarks>
    public string Symbol => Currencies.First(c => c.Key == this.Code).Value;

    /// <summary>
    ///     Provides implementations for various operators.
    /// </summary>
    public static implicit operator Currency(string value)
    {
        return new Currency(value);
    }

    /// <summary>
    ///     Applies a specified mathematical operation to two given Complex numbers and returns the result.
    /// </summary>
    public static implicit operator string(Currency value)
    {
        return value.Code;
    }

    /// <summary>
    ///     Creates a new Currency instance based on the provided currency code.
    /// </summary>
    /// <param name="code">The currency code to create the Currency instance for.</param>
    public static Currency Create(string code)
    {
        if (!Currencies.ContainsKey(code.SafeNull()))
        {
            throw new ArgumentException($"Invalid currency code: {code}", nameof(code));
        }

        return new Currency(code); //Currencies.First(c => c.Key == code).Value; //Currencies[code];
    }

    /// <summary>
    ///     Returns the string representation of the Currency Code's symbol.
    /// </summary>
    /// <returns>The symbol corresponding to the Currency Code.</returns>
    public override string ToString()
    {
        return $"{this.Symbol}";
        // https://social.technet.microsoft.com/wiki/contents/articles/27931.currency-formatting-in-c.aspx
    }

    /// <summary>
    ///     Retrieves the atomic values that represent this instance.
    /// </summary>
    /// <returns>An enumeration of atomic values.</returns>
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Code;
    }
}