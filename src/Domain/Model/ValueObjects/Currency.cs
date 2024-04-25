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
    private static readonly IDictionary<string, Currency> Currencies;

    static Currency()
    {
        Currencies = new Dictionary<string, Currency>()
            {
                { AlbaniaLek.Code, AlbaniaLek },
                { AfghanistanAfghani.Code, AfghanistanAfghani },
                { ArgentinaPeso.Code, ArgentinaPeso },
                { ArubaGuilder.Code, ArubaGuilder },
                { AustraliaDollar.Code, AustraliaDollar },
                { AzerbaijanManat.Code, AzerbaijanManat },
                { BahamasDollar.Code, BahamasDollar },
                { BarbadosDollar.Code, BarbadosDollar },
                { BelarusRuble.Code, BelarusRuble },
                { BelizeDollar.Code, BelizeDollar },
                { BermudaDollar.Code, BermudaDollar },
                { BoliviaBolíviano.Code, BoliviaBolíviano },
                { BosniaandHerzegovinaMark.Code, BosniaandHerzegovinaMark },
                { BotswanaPula.Code, BotswanaPula },
                { BulgariaLev.Code, BulgariaLev },
                { BrazilReal.Code, BrazilReal },
                { BruneiDarussalamDollar.Code, BruneiDarussalamDollar },
                { CambodiaRiel.Code, CambodiaRiel },
                { CanadaDollar.Code, CanadaDollar },
                { CaymanIslandsDollar.Code, CaymanIslandsDollar },
                { ChilePeso.Code, ChilePeso },
                { ChinaYuanRenminbi.Code, ChinaYuanRenminbi },
                { ColombiaPeso.Code, ColombiaPeso },
                { CostaRicaColon.Code, CostaRicaColon },
                { CroatiaKuna.Code, CroatiaKuna },
                { CubaPeso.Code, CubaPeso },
                { CzechRepublicKoruna.Code, CzechRepublicKoruna },
                { DenmarkKrone.Code, DenmarkKrone },
                { DominicanRepublicPeso.Code, DominicanRepublicPeso },
                { EastCaribbeanDollar.Code, EastCaribbeanDollar },
                { EgyptPound.Code, EgyptPound },
                { ElSalvadorColon.Code, ElSalvadorColon },
                { Euro.Code, Euro },
                { FalklandIslands.Code, FalklandIslands },
                { FijiDollar.Code, FijiDollar },
                { GhanaCedi.Code, GhanaCedi },
                { GibraltarPound.Code, GibraltarPound },
                { GuatemalaQuetzal.Code, GuatemalaQuetzal },
                { GuernseyPound.Code, GuernseyPound },
                { GuyanaDollar.Code, GuyanaDollar },
                { HondurasLempira.Code, HondurasLempira },
                { HongKongDollar.Code, HongKongDollar },
                { HungaryForint.Code, HungaryForint },
                { IcelandKrona.Code, IcelandKrona },
                { IndiaRupee.Code, IndiaRupee },
                { IndonesiaRupiah.Code, IndonesiaRupiah },
                { IranRial.Code, IranRial },
                { IsleofManPound.Code, IsleofManPound },
                { IsraelShekel.Code, IsraelShekel },
                { JamaicaDollar.Code, JamaicaDollar },
                { JapanYen.Code, JapanYen },
                { JerseyPound.Code, JerseyPound },
                { KazakhstanTenge.Code, KazakhstanTenge },
                { KoreaNorth.Code, KoreaNorth },
                { KoreaSouth.Code, KoreaSouth },
                { KyrgyzstanSom.Code, KyrgyzstanSom },
                { LaosKip.Code, LaosKip },
                { LebanonPound.Code, LebanonPound },
                { LiberiaDollar.Code, LiberiaDollar },
                { MacedoniaDenar.Code, MacedoniaDenar },
                { MalaysiaRinggit.Code, MalaysiaRinggit },
                { MauritiusRupee.Code, MauritiusRupee },
                { MexicoPeso.Code, MexicoPeso },
                { MongoliaTughrik.Code, MongoliaTughrik },
                { MozambiqueMetical.Code, MozambiqueMetical },
                { NamibiaDollar.Code, NamibiaDollar },
                { NepalRupee.Code, NepalRupee },
                { NetherlandsAntillesGuilder.Code, NetherlandsAntillesGuilder },
                { NewZealandDollar.Code, NewZealandDollar },
                { NicaraguaCordoba.Code, NicaraguaCordoba },
                { NigeriaNaira.Code, NigeriaNaira },
                { NorwayKrone.Code, NorwayKrone },
                { OmanRial.Code, OmanRial },
                { PakistanRupee.Code, PakistanRupee },
                { PanamaBalboa.Code, PanamaBalboa },
                { ParaguayGuarani.Code, ParaguayGuarani },
                { PeruSol.Code, PeruSol },
                { PhilippinesPeso.Code, PhilippinesPeso },
                { PolandZloty.Code, PolandZloty },
                { QatarRiyal.Code, QatarRiyal },
                { RomaniaLeu.Code, RomaniaLeu },
                { RussiaRuble.Code, RussiaRuble },
                { SaintHelenaPound.Code, SaintHelenaPound },
                { SaudiArabiaRiyal.Code, SaudiArabiaRiyal },
                { SerbiaDinar.Code, SerbiaDinar },
                { SeychellesRupee.Code, SeychellesRupee },
                { SingaporeDollar.Code, SingaporeDollar },
                { SolomonIslandsDollar.Code, SolomonIslandsDollar },
                { SomaliaShilling.Code, SomaliaShilling },
                { SouthAfricaRand.Code, SouthAfricaRand },
                { SriLankaRupee.Code, SriLankaRupee },
                { SwedenKrona.Code, SwedenKrona },
                { SwitzerlandFranc.Code, SwitzerlandFranc },
                { SurinameDollar.Code, SurinameDollar },
                { SyriaPound.Code, SyriaPound },
                { TaiwanNewDollar.Code, TaiwanNewDollar },
                { ThailandBaht.Code, ThailandBaht },
                { TrinidadandTobagoDollar.Code, TrinidadandTobagoDollar },
                { TurkeyLira.Code, TurkeyLira },
                { TuvaluDollar.Code, TuvaluDollar },
                { UkraineHryvnia.Code, UkraineHryvnia },
                { GBPound.Code, GBPound },
                { USDollar.Code, USDollar },
                { UruguayPeso.Code, UruguayPeso },
                { UzbekistanSom.Code, UzbekistanSom },
                { VenezuelaBolivar.Code, VenezuelaBolivar },
                { VietNamDong.Code, VietNamDong },
                { YemenRial.Code, YemenRial },
                { ZimbabweDollar.Code, ZimbabweDollar }
            };
    }

    private Currency()
    {
    }

    private Currency(string code, string symbol)
    {
        this.Code = code;
        this.Symbol = symbol;
    }

    public static Currency AlbaniaLek => new("ALL", "Lek"); // source: https://www.xe.com/symbols/
    public static Currency AfghanistanAfghani => new("AFN", "؋");
    public static Currency ArgentinaPeso => new("ARS", "$");
    public static Currency ArubaGuilder => new("AWG", "ƒ");
    public static Currency AustraliaDollar => new("AUD", "$");
    public static Currency AzerbaijanManat => new("AZN", "₼");
    public static Currency BahamasDollar => new("BSD", "$");
    public static Currency BarbadosDollar => new("BBD", "$");
    public static Currency BelarusRuble => new("BYN", "Br");
    public static Currency BelizeDollar => new("BZD", "BZ$");
    public static Currency BermudaDollar => new("BMD", "$");
    public static Currency BoliviaBolíviano => new("BOB", "$b");
    public static Currency BosniaandHerzegovinaMark => new("BAM", "KM");
    public static Currency BotswanaPula => new("BWP", "P");
    public static Currency BulgariaLev => new("BGN", "лв");
    public static Currency BrazilReal => new("BRL", "R$");
    public static Currency BruneiDarussalamDollar => new("BND", "$");
    public static Currency CambodiaRiel => new("KHR", "៛");
    public static Currency CanadaDollar => new("CAD", "$");
    public static Currency CaymanIslandsDollar => new("KYD", "$");
    public static Currency ChilePeso => new("CLP", "$");
    public static Currency ChinaYuanRenminbi => new("CNY", "¥");
    public static Currency ColombiaPeso => new("COP", "$");
    public static Currency CostaRicaColon => new("CRC", "₡");
    public static Currency CroatiaKuna => new("HRK", "kn");
    public static Currency CubaPeso => new("CUP", "₱");
    public static Currency CzechRepublicKoruna => new("CZK", "Kč");
    public static Currency DenmarkKrone => new("DKK", "kr");
    public static Currency DominicanRepublicPeso => new("DOP", "RD$");
    public static Currency EastCaribbeanDollar => new("XCD", "$");
    public static Currency EgyptPound => new("EGP", "£");
    public static Currency ElSalvadorColon => new("SVC", "$");
    public static Currency Euro => new("EUR", "€");
    public static Currency FalklandIslands => new("FKP", "£");
    public static Currency FijiDollar => new("FJD", "$");
    public static Currency GhanaCedi => new("GHS", "¢");
    public static Currency GibraltarPound => new("GIP", "£");
    public static Currency GuatemalaQuetzal => new("GTQ", "Q");
    public static Currency GuernseyPound => new("GGP", "£");
    public static Currency GuyanaDollar => new("GYD", "$");
    public static Currency HondurasLempira => new("HNL", "L");
    public static Currency HongKongDollar => new("HKD", "$");
    public static Currency HungaryForint => new("HUF", "Ft");
    public static Currency IcelandKrona => new("ISK", "kr");
    public static Currency IndiaRupee => new("INR", "8377");
    public static Currency IndonesiaRupiah => new("IDR", "Rp");
    public static Currency IranRial => new("IRR", "﷼");
    public static Currency IsleofManPound => new("IMP", "£");
    public static Currency IsraelShekel => new("ILS", "₪");
    public static Currency JamaicaDollar => new("JMD", "J$");
    public static Currency JapanYen => new("JPY", "¥");
    public static Currency JerseyPound => new("JEP", "£");
    public static Currency KazakhstanTenge => new("KZT", "лв");
    public static Currency KoreaNorth => new("KPW", "₩");
    public static Currency KoreaSouth => new("KRW", "₩");
    public static Currency KyrgyzstanSom => new("KGS", "лв");
    public static Currency LaosKip => new("LAK", "₭");
    public static Currency LebanonPound => new("LBP", "£");
    public static Currency LiberiaDollar => new("LRD", "$");
    public static Currency MacedoniaDenar => new("MKD", "ден");
    public static Currency MalaysiaRinggit => new("MYR", "RM");
    public static Currency MauritiusRupee => new("MUR", "₨");
    public static Currency MexicoPeso => new("MXN", "$");
    public static Currency MongoliaTughrik => new("MNT", "₮");
    public static Currency MozambiqueMetical => new("MZN", "MT");
    public static Currency NamibiaDollar => new("NAD", "$");
    public static Currency NepalRupee => new("NPR", "₨");
    public static Currency NetherlandsAntillesGuilder => new("ANG", "ƒ");
    public static Currency NewZealandDollar => new("NZD", "$");
    public static Currency NicaraguaCordoba => new("NIO", "C$");
    public static Currency NigeriaNaira => new("NGN", "₦");
    public static Currency NorwayKrone => new("NOK", "kr");
    public static Currency OmanRial => new("OMR", "﷼");
    public static Currency PakistanRupee => new("PKR", "₨");
    public static Currency PanamaBalboa => new("PAB", "B/.");
    public static Currency ParaguayGuarani => new("PYG", "Gs");
    public static Currency PeruSol => new("PEN", "S/.");
    public static Currency PhilippinesPeso => new("PHP", "₱");
    public static Currency PolandZloty => new("PLN", "zł");
    public static Currency QatarRiyal => new("QAR", "﷼");
    public static Currency RomaniaLeu => new("RON", "lei");
    public static Currency RussiaRuble => new("RUB", "₽");
    public static Currency SaintHelenaPound => new("SHP", "£");
    public static Currency SaudiArabiaRiyal => new("SAR", "﷼");
    public static Currency SerbiaDinar => new("RSD", "Дин.");
    public static Currency SeychellesRupee => new("SCR", "₨");
    public static Currency SingaporeDollar => new("SGD", "$");
    public static Currency SolomonIslandsDollar => new("SBD", "$");
    public static Currency SomaliaShilling => new("SOS", "S");
    public static Currency SouthAfricaRand => new("ZAR", "R");
    public static Currency SriLankaRupee => new("LKR", "₨");
    public static Currency SwedenKrona => new("SEK", "kr");
    public static Currency SwitzerlandFranc => new("CHF", "CHF");
    public static Currency SurinameDollar => new("SRD", "$");
    public static Currency SyriaPound => new("SYP", "£");
    public static Currency TaiwanNewDollar => new("TWD", "NT$");
    public static Currency ThailandBaht => new("THB", "฿");
    public static Currency TrinidadandTobagoDollar => new("TTD", "TT$");
    public static Currency TurkeyLira => new("TRY", "8378");
    public static Currency TuvaluDollar => new("TVD", "$");
    public static Currency UkraineHryvnia => new("UAH", "₴");
    public static Currency GBPound => new("GBP", "£");
    public static Currency USDollar => new("USD", "$");
    public static Currency UruguayPeso => new("UYU", "$U");
    public static Currency UzbekistanSom => new("UZS", "лв");
    public static Currency VenezuelaBolivar => new("VEF", "Bs");
    public static Currency VietNamDong => new("VND", "₫");
    public static Currency YemenRial => new("YER", "﷼");
    public static Currency ZimbabweDollar => new("ZWD", "Z$");

    public string Code { get; }

    public string Symbol { get; }

    public static implicit operator Currency(string value) => new(value.Split(' ').FirstOrDefault(), value.Split(' ').LastOrDefault());

    public static implicit operator string(Currency value) => value.Code;

    public static Currency ForCode(string code)
    {
        if (!Currencies.ContainsKey(code.SafeNull()))
        {
            throw new ArgumentException($"Invalid currency code: {code}", nameof(code));
        }

        return Currencies[code];
    }

    public override string ToString() => $"{this.Symbol}"; // https://social.technet.microsoft.com/wiki/contents/articles/27931.currency-formatting-in-c.aspx

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return this.Code;
    }
}