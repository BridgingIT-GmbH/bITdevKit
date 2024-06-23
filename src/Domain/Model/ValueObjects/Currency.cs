// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Model;
using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;

public class Currency : ValueObject
{
    private static readonly Dictionary<string, string> Currencies;

    static Currency()
    {
        // source: https://www.xe.com/symbols/
        Currencies = new Dictionary<string, string>()
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
                { "ZWD", "Z$" },
            };
    }

    private Currency()
    {
    }

    private Currency(string code)
    {
        this.Code = code;
    }

    public static Currency AlbaniaLek => Create("ALL");

    public static Currency AfghanistanAfghani => Create("AFN");

    public static Currency ArgentinaPeso => Create("ARS");

    public static Currency ArubaGuilder => Create("AWG");

    public static Currency AustraliaDollar => Create("AUD");

    public static Currency AzerbaijanManat => Create("AZN");

    public static Currency BahamasDollar => Create("BSD");

    public static Currency BarbadosDollar => Create("BBD");

    public static Currency BelarusRuble => Create("BYN");

    public static Currency BelizeDollar => Create("BZD");

    public static Currency BermudaDollar => Create("BMD");

    public static Currency BoliviaBolíviano => Create("BOB");

    public static Currency BosniaandHerzegovinaMark => Create("BAM");

    public static Currency BotswanaPula => Create("BWP");

    public static Currency BulgariaLev => Create("BGN");

    public static Currency BrazilReal => Create("BRL");

    public static Currency BruneiDarussalamDollar => Create("BND");

    public static Currency CambodiaRiel => Create("KHR");

    public static Currency CanadaDollar => Create("CAD");

    public static Currency CaymanIslandsDollar => Create("KYD");

    public static Currency ChilePeso => Create("CLP");

    public static Currency ChinaYuanRenminbi => Create("CNY");

    public static Currency ColombiaPeso => Create("COP");

    public static Currency CostaRicaColon => Create("CRC");

    public static Currency CroatiaKuna => Create("HRK");

    public static Currency CubaPeso => Create("CUP");

    public static Currency CzechRepublicKoruna => Create("CZK");

    public static Currency DenmarkKrone => Create("DKK");

    public static Currency DominicanRepublicPeso => Create("DOP");

    public static Currency EastCaribbeanDollar => Create("XCD");

    public static Currency EgyptPound => Create("EGP");

    public static Currency ElSalvadorColon => Create("SVC");

    public static Currency Euro => Create("EUR");

    public static Currency FalklandIslands => Create("FKP");

    public static Currency FijiDollar => Create("FJD");

    public static Currency GhanaCedi => Create("GHS");

    public static Currency GibraltarPound => Create("GIP");

    public static Currency GuatemalaQuetzal => Create("GTQ");

    public static Currency GuernseyPound => Create("GGP");

    public static Currency GuyanaDollar => Create("GYD");

    public static Currency HondurasLempira => Create("HNL");

    public static Currency HongKongDollar => Create("HKD");

    public static Currency HungaryForint => Create("HUF");

    public static Currency IcelandKrona => Create("ISK");

    public static Currency IndiaRupee => Create("INR");

    public static Currency IndonesiaRupiah => Create("IDR");

    public static Currency IranRial => Create("IRR");

    public static Currency IsleofManPound => Create("IMP");

    public static Currency IsraelShekel => Create("ILS");

    public static Currency JamaicaDollar => Create("JMD");

    public static Currency JapanYen => Create("JPY");

    public static Currency JerseyPound => Create("JEP");

    public static Currency KazakhstanTenge => Create("KZT");

    public static Currency KoreaNorth => Create("KPW");

    public static Currency KoreaSouth => Create("KRW");

    public static Currency KyrgyzstanSom => Create("KGS");

    public static Currency LaosKip => Create("LAK");

    public static Currency LebanonPound => Create("LBP");

    public static Currency LiberiaDollar => Create("LRD");

    public static Currency MacedoniaDenar => Create("MKD");

    public static Currency MalaysiaRinggit => Create("MYR");

    public static Currency MauritiusRupee => Create("MUR");

    public static Currency MexicoPeso => Create("MXN");

    public static Currency MongoliaTughrik => Create("MNT");

    public static Currency MozambiqueMetical => Create("MZN");

    public static Currency NamibiaDollar => Create("NAD");

    public static Currency NepalRupee => Create("NPR");

    public static Currency NetherlandsAntillesGuilder => Create("ANG");

    public static Currency NewZealandDollar => Create("NZD");

    public static Currency NicaraguaCordoba => Create("NIO");

    public static Currency NigeriaNaira => Create("NGN");

    public static Currency NorwayKrone => Create("NOK");

    public static Currency OmanRial => Create("OMR");

    public static Currency PakistanRupee => Create("PKR");

    public static Currency PanamaBalboa => Create("PAB");

    public static Currency ParaguayGuarani => Create("PYG");

    public static Currency PeruSol => Create("PEN");

    public static Currency PhilippinesPeso => Create("PHP");

    public static Currency PolandZloty => Create("PLN");

    public static Currency QatarRiyal => Create("QAR");

    public static Currency RomaniaLeu => Create("RON");

    public static Currency RussiaRuble => Create("RUB");

    public static Currency SaintHelenaPound => Create("SHP");

    public static Currency SaudiArabiaRiyal => Create("SAR");

    public static Currency SerbiaDinar => Create("RSD");

    public static Currency SeychellesRupee => Create("SCR");

    public static Currency SingaporeDollar => Create("SGD");

    public static Currency SolomonIslandsDollar => Create("SBD");

    public static Currency SomaliaShilling => Create("SOS");

    public static Currency SouthAfricaRand => Create("ZAR");

    public static Currency SriLankaRupee => Create("LKR");

    public static Currency SwedenKrona => Create("SEK");

    public static Currency SwitzerlandFranc => Create("CHF");

    public static Currency SurinameDollar => Create("SRD");

    public static Currency SyriaPound => Create("SYP");

    public static Currency TaiwanNewDollar => Create("TWD");

    public static Currency ThailandBaht => Create("THB");

    public static Currency TrinidadandTobagoDollar => Create("TTD");

    public static Currency TurkeyLira => Create("TRY");

    public static Currency TuvaluDollar => Create("TVD");

    public static Currency UkraineHryvnia => Create("UAH");

    public static Currency GBPound => Create("GBP");

    public static Currency USDollar => Create("USD");

    public static Currency UruguayPeso => Create("UYU");

    public static Currency UzbekistanSom => Create("UZS");

    public static Currency VenezuelaBolivar => Create("VEF");

    public static Currency VietNamDong => Create("VND");

    public static Currency YemenRial => Create("YER");

    public static Currency ZimbabweDollar => Create("ZWD");

    public string Code { get; protected set; }

    public string Symbol => Currencies.First(c => c.Key == this.Code).Value;

    public static implicit operator Currency(string value) => new(value);

    public static implicit operator string(Currency value) => value.Code;

    public static Currency Create(string code)
    {
        if (!Currencies.ContainsKey(code.SafeNull()))
        {
            throw new ArgumentException($"Invalid currency code: {code}", nameof(code));
        }

        return new Currency(code); //Currencies.First(c => c.Key == code).Value; //Currencies[code];
    }

    public override string ToString() => $"{this.Symbol}"; // https://social.technet.microsoft.com/wiki/contents/articles/27931.currency-formatting-in-c.aspx

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Code;
    }
}