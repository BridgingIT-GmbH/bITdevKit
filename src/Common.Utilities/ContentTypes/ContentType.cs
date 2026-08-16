// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines known content types and their associated MIME metadata.
/// </summary>
public enum ContentType // https://mimetype.io/all-types
{
    /// <summary>
    /// Represents the <c>application/x-authorware-bin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    AAB,

    /// <summary>
    /// Represents the <c>audio/x-aac</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-aac", IsBinary = true)]
    AAC,

    /// <summary>
    /// Represents the <c>application/x-authorware-map</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-map", IsBinary = true)]
    AAM,

    /// <summary>
    /// Represents the <c>application/x-authorware-seg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-seg", IsBinary = true)]
    AAS,

    /// <summary>
    /// Represents the <c>application/x-abiword</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-abiword", IsBinary = true)]
    ABW,

    /// <summary>
    /// Represents the <c>application/pkix-attr-cert</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkix-attr-cert", IsBinary = true)]
    AC,

    /// <summary>
    /// Represents the <c>application/vnd.americandynamics.acc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.americandynamics.acc", IsBinary = true)]
    ACC,

    /// <summary>
    /// Represents the <c>application/x-ace-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ace-compressed", IsBinary = true)]
    ACE,

    /// <summary>
    /// Represents the <c>application/vnd.acucobol</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.acucobol", IsBinary = true)]
    ACU,

    /// <summary>
    /// Represents the <c>application/vnd.acucorp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.acucorp", IsBinary = true)]
    ACUTC,

    /// <summary>
    /// Represents the <c>audio/adpcm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/adpcm", IsBinary = true)]
    ADP,

    /// <summary>
    /// Represents the <c>application/vnd.audiograph</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.audiograph", IsBinary = true)]
    AEP,

    /// <summary>
    /// Represents the <c>application/x-font-type1</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    AFM,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.modcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    AFP,

    /// <summary>
    /// Represents the <c>application/vnd.ahead.space</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ahead.space", IsBinary = true)]
    AHEAD,

    /// <summary>
    /// Represents the <c>application/postscript</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    AI,

    /// <summary>
    /// Represents the <c>audio/x-aiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIF,

    /// <summary>
    /// Represents the <c>audio/x-aiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIFC,

    /// <summary>
    /// Represents the <c>audio/x-aiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIFF,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.air-application-installer-package+zip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.air-application-installer-package+zip", IsBinary = true)]
    AIR,

    /// <summary>
    /// Represents the <c>application/vnd.dvb.ait</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dvb.ait", IsBinary = true)]
    AIT,

    /// <summary>
    /// Represents the <c>application/vnd.amiga.ami</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.amiga.ami", IsBinary = true)]
    AMI,

    /// <summary>
    /// Represents the <c>application/vnd.android.package-archive</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.android.package-archive", IsBinary = true)]
    APK,

    /// <summary>
    /// Represents the <c>text/cache-manifest</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/cache-manifest", IsText = true)]
    APPCACHE,

    /// <summary>
    /// Represents the <c>application/x-ms-application</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ms-application", IsBinary = true)]
    APPLICATION,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-approach</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-approach", IsBinary = true)]
    APR,

    /// <summary>
    /// Represents the <c>application/x-freearc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-freearc", IsBinary = true)]
    ARC,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    ASC,

    /// <summary>
    /// Represents the <c>video/x-ms-asf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-asf", IsBinary = true)]
    ASF,

    /// <summary>
    /// Represents the <c>text/x-asm</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-asm", IsText = true)]
    ASM,

    /// <summary>
    /// Represents the <c>application/vnd.accpac.simply.aso</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.accpac.simply.aso", IsBinary = true)]
    ASO,

    /// <summary>
    /// Represents the <c>video/x-ms-asf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-asf", IsBinary = true)]
    ASX,

    /// <summary>
    /// Represents the <c>application/vnd.acucorp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.acucorp", IsBinary = true)]
    ATC,

    /// <summary>
    /// Represents the <c>application/atom+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/atom+xml", IsText = true)]
    ATOM,

    /// <summary>
    /// Represents the <c>application/atomcat+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/atomcat+xml", IsText = true)]
    ATOMCAT,

    /// <summary>
    /// Represents the <c>application/atomsvc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/atomsvc+xml", IsText = true)]
    ATOMSVC,

    /// <summary>
    /// Represents the <c>application/vnd.antix.game-component</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.antix.game-component", IsBinary = true)]
    ATX,

    /// <summary>
    /// Represents the <c>audio/basic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/basic", IsBinary = true)]
    AU,

    /// <summary>
    /// Represents the <c>video/x-msvideo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-msvideo", IsBinary = true)]
    AVI,

    /// <summary>
    /// Represents the <c>application/applixware</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/applixware", IsBinary = true)]
    AW,

    /// <summary>
    /// Represents the <c>application/vnd.airzip.filesecure.azf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.airzip.filesecure.azf", IsBinary = true)]
    AZF,

    /// <summary>
    /// Represents the <c>application/vnd.airzip.filesecure.azs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.airzip.filesecure.azs", IsBinary = true)]
    AZS,

    /// <summary>
    /// Represents the <c>application/vnd.amazon.ebook</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.amazon.ebook", IsBinary = true)]
    AZW,

    /// <summary>
    /// Represents the <c>application/x-msdownload</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    BAT,

    /// <summary>
    /// Represents the <c>application/x-bcpio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-bcpio", IsBinary = true)]
    BCPIO,

    /// <summary>
    /// Represents the <c>application/x-font-bdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-bdf", IsBinary = true)]
    BDF,

    /// <summary>
    /// Represents the <c>application/vnd.syncml.dm+wbxml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.syncml.dm+wbxml", IsText = true)]
    BDM,

    /// <summary>
    /// Represents the <c>application/vnd.realvnc.bed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.realvnc.bed", IsBinary = true)]
    BED,

    /// <summary>
    /// Represents the <c>application/vnd.fujitsu.oasysprs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasysprs", IsBinary = true)]
    BH2,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    BIN,

    /// <summary>
    /// Represents the <c>application/x-blorb</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-blorb", IsBinary = true)]
    BLB,

    /// <summary>
    /// Represents the <c>application/x-blorb</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-blorb", IsBinary = true)]
    BLORB,

    /// <summary>
    /// Represents the <c>application/vnd.bmi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.bmi", IsBinary = true)]
    BMI,

    /// <summary>
    /// Represents the <c>image/bmp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/bmp", IsBinary = true)]
    BMP,

    /// <summary>
    /// Represents the <c>application/vnd.framemaker</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    BOOK,

    /// <summary>
    /// Represents the <c>application/vnd.previewsystems.box</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.previewsystems.box", IsBinary = true)]
    BOX,

    /// <summary>
    /// Represents the <c>application/x-bzip2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-bzip2", IsBinary = true)]
    BOZ,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    BPK,

    /// <summary>
    /// Represents the <c>image/prs.btif</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/prs.btif", IsBinary = true)]
    BTIF,

    /// <summary>
    /// Represents the <c>application/x-bzip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-bzip", IsBinary = true)]
    BZ,

    /// <summary>
    /// Represents the <c>application/x-bzip2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-bzip2", IsBinary = true)]
    BZ2,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    C,

    /// <summary>
    /// Represents the <c>application/vnd.cluetrust.cartomobile-config</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cluetrust.cartomobile-config", IsBinary = true)]
    C11AMC,

    /// <summary>
    /// Represents the <c>application/vnd.cluetrust.cartomobile-config-pkg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cluetrust.cartomobile-config-pkg", IsBinary = true)]
    C11AMZ,

    /// <summary>
    /// Represents the <c>application/vnd.clonk.c4group</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4D,

    /// <summary>
    /// Represents the <c>application/vnd.clonk.c4group</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4F,

    /// <summary>
    /// Represents the <c>application/vnd.clonk.c4group</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4G,

    /// <summary>
    /// Represents the <c>application/vnd.clonk.c4group</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4P,

    /// <summary>
    /// Represents the <c>application/vnd.clonk.c4group</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4U,

    /// <summary>
    /// Represents the <c>application/vnd.ms-cab-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-cab-compressed", IsBinary = true)]
    CAB,

    /// <summary>
    /// Represents the <c>audio/x-caf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-caf", IsBinary = true)]
    CAF,

    /// <summary>
    /// Represents the <c>application/vnd.tcpdump.pcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    CAP,

    /// <summary>
    /// Represents the <c>application/vnd.curl.car</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.curl.car", IsBinary = true)]
    CAR,

    /// <summary>
    /// Represents the <c>application/vnd.ms-pki.seccat</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-pki.seccat", IsBinary = true)]
    CAT,

    /// <summary>
    /// Represents the <c>application/x-cbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CB7,

    /// <summary>
    /// Represents the <c>application/x-cbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBA,

    /// <summary>
    /// Represents the <c>application/x-cbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBR,

    /// <summary>
    /// Represents the <c>application/x-cbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBT,

    /// <summary>
    /// Represents the <c>application/x-cbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBZ,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CC,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CCT,

    /// <summary>
    /// Represents the <c>application/ccxml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ccxml+xml", IsText = true)]
    CCXML,

    /// <summary>
    /// Represents the <c>application/vnd.contact.cmsg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.contact.cmsg", IsBinary = true)]
    CDBCMSG,

    /// <summary>
    /// Represents the <c>application/x-netcdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-netcdf", IsBinary = true)]
    CDF,

    /// <summary>
    /// Represents the <c>application/vnd.mediastation.cdkey</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mediastation.cdkey", IsBinary = true)]
    CDKEY,

    /// <summary>
    /// Represents the <c>application/cdmi-capability</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cdmi-capability", IsBinary = true)]
    CDMIA,

    /// <summary>
    /// Represents the <c>application/cdmi-container</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cdmi-container", IsBinary = true)]
    CDMIC,

    /// <summary>
    /// Represents the <c>application/cdmi-domain</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cdmi-domain", IsBinary = true)]
    CDMID,

    /// <summary>
    /// Represents the <c>application/cdmi-object</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cdmi-object", IsBinary = true)]
    CDMIO,

    /// <summary>
    /// Represents the <c>application/cdmi-queue</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cdmi-queue", IsBinary = true)]
    CDMIQ,

    /// <summary>
    /// Represents the <c>chemical/x-cdx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-cdx", IsBinary = true)]
    CDX,

    /// <summary>
    /// Represents the <c>application/vnd.chemdraw+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.chemdraw+xml", IsText = true)]
    CDXML,

    /// <summary>
    /// Represents the <c>application/vnd.cinderella</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cinderella", IsBinary = true)]
    CDY,

    /// <summary>
    /// Represents the <c>application/pkix-cert</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkix-cert", IsBinary = true)]
    CER,

    /// <summary>
    /// Represents the <c>application/x-cfs-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cfs-compressed", IsBinary = true)]
    CFS,

    /// <summary>
    /// Represents the <c>image/cgm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/cgm", IsBinary = true)]
    CGM,

    /// <summary>
    /// Represents the <c>application/x-chat</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-chat", IsBinary = true)]
    CHAT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-htmlhelp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-htmlhelp", IsBinary = true)]
    CHM,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kchart</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kchart", IsBinary = true)]
    CHRT,

    /// <summary>
    /// Represents the <c>chemical/x-cif</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-cif", IsBinary = true)]
    CIF,

    /// <summary>
    /// Represents the <c>application/vnd.anser-web-certificate-issue-initiation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.anser-web-certificate-issue-initiation", IsBinary = true)]
    CII,

    /// <summary>
    /// Represents the <c>application/vnd.ms-artgalry</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-artgalry", IsBinary = true)]
    CIL,

    /// <summary>
    /// Represents the <c>application/vnd.claymore</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.claymore", IsBinary = true)]
    CLA,

    /// <summary>
    /// Represents the <c>application/java-vm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/java-vm", IsBinary = true)]
    CLASS,

    /// <summary>
    /// Represents the <c>application/vnd.crick.clicker.keyboard</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.keyboard", IsBinary = true)]
    CLKK,

    /// <summary>
    /// Represents the <c>application/vnd.crick.clicker.palette</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.palette", IsBinary = true)]
    CLKP,

    /// <summary>
    /// Represents the <c>application/vnd.crick.clicker.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.template", IsBinary = true)]
    CLKT,

    /// <summary>
    /// Represents the <c>application/vnd.crick.clicker.wordbank</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.wordbank", IsBinary = true)]
    CLKW,

    /// <summary>
    /// Represents the <c>application/vnd.crick.clicker</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker", IsBinary = true)]
    CLKX,

    /// <summary>
    /// Represents the <c>application/x-msclip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msclip", IsBinary = true)]
    CLP,

    /// <summary>
    /// Represents the <c>application/vnd.cosmocaller</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cosmocaller", IsBinary = true)]
    CMC,

    /// <summary>
    /// Represents the <c>chemical/x-cmdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-cmdf", IsBinary = true)]
    CMDF,

    /// <summary>
    /// Represents the <c>chemical/x-cml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-cml", IsBinary = true)]
    CML,

    /// <summary>
    /// Represents the <c>application/vnd.yellowriver-custom-menu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yellowriver-custom-menu", IsBinary = true)]
    CMP,

    /// <summary>
    /// Represents the <c>image/x-cmx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-cmx", IsBinary = true)]
    CMX,

    /// <summary>
    /// Represents the <c>application/vnd.rim.cod</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.rim.cod", IsBinary = true)]
    COD,

    /// <summary>
    /// Represents the <c>application/x-msdownload</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    COM,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    CONF,

    /// <summary>
    /// Represents the <c>application/x-cpio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cpio", IsBinary = true)]
    CPIO,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CPP,

    /// <summary>
    /// Represents the <c>application/mac-compactpro</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mac-compactpro", IsBinary = true)]
    CPT,

    /// <summary>
    /// Represents the <c>application/x-mscardfile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mscardfile", IsBinary = true)]
    CRD,

    /// <summary>
    /// Represents the <c>application/pkix-crl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkix-crl", IsBinary = true)]
    CRL,

    /// <summary>
    /// Represents the <c>application/x-x509-ca-cert</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-x509-ca-cert", IsBinary = true)]
    CRT,

    /// <summary>
    /// Represents the <c>application/vnd.rig.cryptonote</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.rig.cryptonote", IsBinary = true)]
    CRYPTONOTE,

    /// <summary>
    /// Represents the <c>application/x-csh</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-csh", IsBinary = true)]
    CSH,

    /// <summary>
    /// Represents the <c>chemical/x-csml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-csml", IsBinary = true)]
    CSML,

    /// <summary>
    /// Represents the <c>application/vnd.commonspace</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.commonspace", IsBinary = true)]
    CSP,

    /// <summary>
    /// Represents the <c>text/css</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/css", IsText = true, FileExtension = "css")]
    CSS,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CST,

    /// <summary>
    /// Represents the <c>text/csv</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/csv", IsText = true, FileExtension = "csv")]
    CSV,

    /// <summary>
    /// Represents the <c>application/cu-seeme</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/cu-seeme", IsBinary = true)]
    CU,

    /// <summary>
    /// Represents the <c>text/vnd.curl</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.curl", IsText = true)]
    CURL,

    /// <summary>
    /// Represents the <c>application/prs.cww</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/prs.cww", IsBinary = true)]
    CWW,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CXT,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CXX,

    /// <summary>
    /// Represents the <c>model/vnd.collada+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.collada+xml", IsText = true)]
    DAE,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.daf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.daf", IsBinary = true)]
    DAF,

    /// <summary>
    /// Represents the <c>application/vnd.dart</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dart", IsBinary = true)]
    DART,

    /// <summary>
    /// Represents the <c>application/vnd.fdsn.seed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.seed", IsBinary = true)]
    DATALESS,

    /// <summary>
    /// Represents the <c>application/davmount+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/davmount+xml", IsText = true)]
    DAVMOUNT,

    /// <summary>
    /// Represents the <c>application/docbook+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/docbook+xml", IsText = true)]
    DBK,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DCR,

    /// <summary>
    /// Represents the <c>text/vnd.curl.dcurl</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.curl.dcurl", IsText = true)]
    DCURL,

    /// <summary>
    /// Represents the <c>application/vnd.oma.dd2+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oma.dd2+xml", IsText = true)]
    DD2,

    /// <summary>
    /// Represents the <c>application/vnd.fujixerox.ddd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.ddd", IsBinary = true)]
    DDD,

    /// <summary>
    /// Represents the <c>application/x-debian-package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-debian-package", IsBinary = true)]
    DEB,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    DEF,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DEPLOY,

    /// <summary>
    /// Represents the <c>application/x-x509-ca-cert</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-x509-ca-cert", IsBinary = true)]
    DER,

    /// <summary>
    /// Represents the <c>application/vnd.dreamfactory</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dreamfactory", IsBinary = true)]
    DFAC,

    /// <summary>
    /// Represents the <c>application/x-dgc-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-dgc-compressed", IsBinary = true)]
    DGC,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    DIC,

    /// <summary>
    /// Represents the <c>video/x-dv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-dv", IsBinary = true)]
    DIF,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DIR,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.dis</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.dis", IsBinary = true)]
    DIS,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DIST,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DISTZ,

    /// <summary>
    /// Represents the <c>image/vnd.djvu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.djvu", IsBinary = true)]
    DJV,

    /// <summary>
    /// Represents the <c>image/vnd.djvu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.djvu", IsBinary = true)]
    DJVU,

    /// <summary>
    /// Represents the <c>application/x-msdownload</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    DLL,

    /// <summary>
    /// Represents the <c>application/x-apple-diskimage</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-apple-diskimage", IsBinary = true)]
    DMG,

    /// <summary>
    /// Represents the <c>application/vnd.tcpdump.pcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    DMP,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DMS,

    /// <summary>
    /// Represents the <c>application/vnd.dna</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dna", IsBinary = true)]
    DNA,

    /// <summary>
    /// Represents the <c>application/msword</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/msword", IsBinary = true)]
    DOC,

    /// <summary>
    /// Represents the <c>application/vnd.ms-word.document.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-word.document.macroenabled.12", IsBinary = true)]
    DOCM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        IsBinary = true)]
    DOCX,

    /// <summary>
    /// Represents the <c>application/msword</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/msword", IsBinary = true)]
    DOT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-word.template.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-word.template.macroenabled.12", IsBinary = true)]
    DOTM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.wordprocessingml.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
        IsBinary = true)]
    DOTX,

    /// <summary>
    /// Represents the <c>application/vnd.osgi.dp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.osgi.dp", IsBinary = true)]
    DP,

    /// <summary>
    /// Represents the <c>application/vnd.dpgraph</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dpgraph", IsBinary = true)]
    DPG,

    /// <summary>
    /// Represents the <c>audio/vnd.dra</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.dra", IsBinary = true)]
    DRA,

    /// <summary>
    /// Represents the <c>text/prs.lines.tag</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/prs.lines.tag", IsText = true)]
    DSC,

    /// <summary>
    /// Represents the <c>application/dssc+der</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/dssc+der", IsBinary = true)]
    DSSC,

    /// <summary>
    /// Represents the <c>application/x-dtbook+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-dtbook+xml", IsText = true)]
    DTB,

    /// <summary>
    /// Represents the <c>application/xml-dtd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xml-dtd", IsBinary = true)]
    DTD,

    /// <summary>
    /// Represents the <c>audio/vnd.dts</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.dts", IsBinary = true)]
    DTS,

    /// <summary>
    /// Represents the <c>audio/vnd.dts.hd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.dts.hd", IsBinary = true)]
    DTSHD,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DUMP,

    /// <summary>
    /// Represents the <c>video/x-dv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-dv", IsBinary = true)]
    DV,

    /// <summary>
    /// Represents the <c>video/vnd.dvb.file</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dvb.file", IsBinary = true)]
    DVB,

    /// <summary>
    /// Represents the <c>application/x-dvi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-dvi", IsBinary = true)]
    DVI,

    /// <summary>
    /// Represents the <c>model/vnd.dwf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.dwf", IsBinary = true)]
    DWF,

    /// <summary>
    /// Represents the <c>image/vnd.dwg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dwg", IsBinary = true)]
    DWG,

    /// <summary>
    /// Represents the <c>image/vnd.dxf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dxf", IsBinary = true)]
    DXF,

    /// <summary>
    /// Represents the <c>application/vnd.spotfire.dxp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.spotfire.dxp", IsBinary = true)]
    DXP,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DXR,

    /// <summary>
    /// Represents the <c>audio/vnd.nuera.ecelp4800</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp4800", IsBinary = true)]
    ECELP4800,

    /// <summary>
    /// Represents the <c>audio/vnd.nuera.ecelp7470</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp7470", IsBinary = true)]
    ECELP7470,

    /// <summary>
    /// Represents the <c>audio/vnd.nuera.ecelp9600</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp9600", IsBinary = true)]
    ECELP9600,

    /// <summary>
    /// Represents the <c>application/ecmascript</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ecmascript", IsBinary = true)]
    ECMA,

    /// <summary>
    /// Represents the <c>application/vnd.novadigm.edm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.edm", IsBinary = true)]
    EDM,

    /// <summary>
    /// Represents the <c>application/vnd.novadigm.edx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.edx", IsBinary = true)]
    EDX,

    /// <summary>
    /// Represents the <c>application/vnd.picsel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.picsel", IsBinary = true)]
    EFIF,

    /// <summary>
    /// Represents the <c>application/vnd.pg.osasli</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pg.osasli", IsBinary = true)]
    EI6,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    ELC,

    /// <summary>
    /// Represents the <c>application/x-msmetafile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    EMF,

    /// <summary>
    /// Represents the <c>message/rfc822</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "message/rfc822", IsBinary = true)]
    EML,

    /// <summary>
    /// Represents the <c>application/emma+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/emma+xml", IsText = true)]
    EMMA,

    /// <summary>
    /// Represents the <c>application/x-msmetafile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    EMZ,

    /// <summary>
    /// Represents the <c>audio/vnd.digital-winds</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.digital-winds", IsBinary = true)]
    EOL,

    /// <summary>
    /// Represents the <c>application/vnd.ms-fontobject</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-fontobject", IsBinary = true)]
    EOT,

    /// <summary>
    /// Represents the <c>application/postscript</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    EPS,

    /// <summary>
    /// Represents the <c>application/epub+zip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/epub+zip", IsBinary = true)]
    EPUB,

    /// <summary>
    /// Represents the <c>application/vnd.eszigno3+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.eszigno3+xml", IsText = true)]
    ES3,

    /// <summary>
    /// Represents the <c>application/vnd.osgi.subsystem</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.osgi.subsystem", IsBinary = true)]
    ESA,

    /// <summary>
    /// Represents the <c>application/vnd.epson.esf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.epson.esf", IsBinary = true)]
    ESF,

    /// <summary>
    /// Represents the <c>application/vnd.eszigno3+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.eszigno3+xml", IsText = true)]
    ET3,

    /// <summary>
    /// Represents the <c>text/x-setext</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-setext", IsText = true)]
    ETX,

    /// <summary>
    /// Represents the <c>application/x-eva</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-eva", IsBinary = true)]
    EVA,

    /// <summary>
    /// Represents the <c>application/x-envoy</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-envoy", IsBinary = true)]
    EVY,

    /// <summary>
    /// Represents the <c>application/x-msdownload</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    EXE,

    /// <summary>
    /// Represents the <c>application/exi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/exi", IsBinary = true)]
    EXI,

    /// <summary>
    /// Represents the <c>application/vnd.novadigm.ext</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.ext", IsBinary = true)]
    EXT,

    /// <summary>
    /// Represents the <c>MIME type (lowercased)</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "MIME type (lowercased)", IsBinary = true)]
    EXTENSIONS,

    /// <summary>
    /// Represents the <c>application/andrew-inset</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/andrew-inset", IsBinary = true)]
    EZ,

    /// <summary>
    /// Represents the <c>application/vnd.ezpix-album</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ezpix-album", IsBinary = true)]
    EZ2,

    /// <summary>
    /// Represents the <c>application/vnd.ezpix-package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ezpix-package", IsBinary = true)]
    EZ3,

    /// <summary>
    /// Represents the <c>text/x-fortran</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F,

    /// <summary>
    /// Represents the <c>video/x-f4v</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-f4v", IsBinary = true)]
    F4V,

    /// <summary>
    /// Represents the <c>text/x-fortran</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F77,

    /// <summary>
    /// Represents the <c>text/x-fortran</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F90,

    /// <summary>
    /// Represents the <c>image/vnd.fastbidsheet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.fastbidsheet", IsBinary = true)]
    FBS,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.formscentral.fcdt</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.formscentral.fcdt", IsBinary = true)]
    FCDT,

    /// <summary>
    /// Represents the <c>application/vnd.isac.fcs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.isac.fcs", IsBinary = true)]
    FCS,

    /// <summary>
    /// Represents the <c>application/vnd.fdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fdf", IsBinary = true)]
    FDF,

    /// <summary>
    /// Represents the <c>application/vnd.denovo.fcselayout-link</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.denovo.fcselayout-link", IsBinary = true)]
    FE_LAUNCH,

    /// <summary>
    /// Represents the <c>application/vnd.fujitsu.oasysgp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasysgp", IsBinary = true)]
    FG5,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    FGD,

    /// <summary>
    /// Represents the <c>image/x-freehand</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH,

    /// <summary>
    /// Represents the <c>image/x-freehand</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH4,

    /// <summary>
    /// Represents the <c>image/x-freehand</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH5,

    /// <summary>
    /// Represents the <c>image/x-freehand</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH7,

    /// <summary>
    /// Represents the <c>image/x-freehand</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FHC,

    /// <summary>
    /// Represents the <c>application/x-xfig</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-xfig", IsBinary = true)]
    FIG,

    /// <summary>
    /// Represents the <c>audio/x-flac</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-flac", IsBinary = true)]
    FLAC,

    /// <summary>
    /// Represents the <c>video/x-fli</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-fli", IsBinary = true)]
    FLI,

    /// <summary>
    /// Represents the <c>application/vnd.micrografx.flo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.micrografx.flo", IsBinary = true)]
    FLO,

    /// <summary>
    /// Represents the <c>video/x-flv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-flv", IsBinary = true)]
    FLV,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kivio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kivio", IsBinary = true)]
    FLW,

    /// <summary>
    /// Represents the <c>text/vnd.fmi.flexstor</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.fmi.flexstor", IsText = true)]
    FLX,

    /// <summary>
    /// Represents the <c>text/vnd.fly</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.fly", IsText = true)]
    FLY,

    /// <summary>
    /// Represents the <c>application/vnd.framemaker</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    FM,

    /// <summary>
    /// Represents the <c>application/vnd.frogans.fnc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.frogans.fnc", IsBinary = true)]
    FNC,

    /// <summary>
    /// Represents the <c>text/x-fortran</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    FOR,

    /// <summary>
    /// Represents the <c>image/vnd.fpx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.fpx", IsBinary = true)]
    FPX,

    /// <summary>
    /// Represents the <c>application/vnd.framemaker</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    FRAME,

    /// <summary>
    /// Represents the <c>application/vnd.fsc.weblaunch</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fsc.weblaunch", IsBinary = true)]
    FSC,

    /// <summary>
    /// Represents the <c>image/vnd.fst</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.fst", IsBinary = true)]
    FST,

    /// <summary>
    /// Represents the <c>application/x-www-form-urlencoded</c> non-binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-www-form-urlencoded", IsBinary = false)]
    FORM,

    /// <summary>
    /// Represents the <c>multipart/form-data</c> non-binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "multipart/form-data", IsBinary = false)]
    MFORM,

    /// <summary>
    /// Represents the <c>application/vnd.fluxtime.clip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fluxtime.clip", IsBinary = true)]
    FTC,

    /// <summary>
    /// Represents the <c>application/vnd.anser-web-funds-transfer-initiation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.anser-web-funds-transfer-initiation", IsBinary = true)]
    FTI,

    /// <summary>
    /// Represents the <c>video/vnd.fvt</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.fvt", IsBinary = true)]
    FVT,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.fxp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.fxp", IsBinary = true)]
    FXP,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.fxp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.fxp", IsBinary = true)]
    FXPL,

    /// <summary>
    /// Represents the <c>application/vnd.fuzzysheet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fuzzysheet", IsBinary = true)]
    FZS,

    /// <summary>
    /// Represents the <c>application/vnd.geoplan</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geoplan", IsBinary = true)]
    G2W,

    /// <summary>
    /// Represents the <c>image/g3fax</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/g3fax", IsBinary = true)]
    G3,

    /// <summary>
    /// Represents the <c>application/vnd.geospace</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geospace", IsBinary = true)]
    G3W,

    /// <summary>
    /// Represents the <c>application/vnd.groove-account</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-account", IsBinary = true)]
    GAC,

    /// <summary>
    /// Represents the <c>application/x-tads</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tads", IsBinary = true)]
    GAM,

    /// <summary>
    /// Represents the <c>application/rpki-ghostbusters</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rpki-ghostbusters", IsBinary = true)]
    GBR,

    /// <summary>
    /// Represents the <c>application/x-gca-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-gca-compressed", IsBinary = true)]
    GCA,

    /// <summary>
    /// Represents the <c>model/vnd.gdl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.gdl", IsBinary = true)]
    GDL,

    /// <summary>
    /// Represents the <c>application/vnd.dynageo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dynageo", IsBinary = true)]
    GEO,

    /// <summary>
    /// Represents the <c>application/vnd.geometry-explorer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geometry-explorer", IsBinary = true)]
    GEX,

    /// <summary>
    /// Represents the <c>application/vnd.geogebra.file</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geogebra.file", IsBinary = true)]
    GGB,

    /// <summary>
    /// Represents the <c>application/vnd.geogebra.tool</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geogebra.tool", IsBinary = true)]
    GGT,

    /// <summary>
    /// Represents the <c>application/vnd.groove-help</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-help", IsBinary = true)]
    GHF,

    /// <summary>
    /// Represents the <c>image/gif</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/gif", IsBinary = true)]
    GIF,

    /// <summary>
    /// Represents the <c>application/vnd.groove-identity-message</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-identity-message", IsBinary = true)]
    GIM,

    /// <summary>
    /// Represents the <c>application/gml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/gml+xml", IsText = true)]
    GML,

    /// <summary>
    /// Represents the <c>application/vnd.gmx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.gmx", IsBinary = true)]
    GMX,

    /// <summary>
    /// Represents the <c>application/x-gnumeric</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-gnumeric", IsBinary = true)]
    GNUMERIC,

    /// <summary>
    /// Represents the <c>application/vnd.flographit</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.flographit", IsBinary = true)]
    GPH,

    /// <summary>
    /// Represents the <c>application/gpx+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/gpx+xml", IsText = true)]
    GPX,

    /// <summary>
    /// Represents the <c>application/vnd.grafeq</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.grafeq", IsBinary = true)]
    GQF,

    /// <summary>
    /// Represents the <c>application/vnd.grafeq</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.grafeq", IsBinary = true)]
    GQS,

    /// <summary>
    /// Represents the <c>application/srgs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/srgs", IsBinary = true)]
    GRAM,

    /// <summary>
    /// Represents the <c>application/x-gramps-xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-gramps-xml", IsText = true)]
    GRAMPS,

    /// <summary>
    /// Represents the <c>application/vnd.geometry-explorer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geometry-explorer", IsBinary = true)]
    GRE,

    /// <summary>
    /// Represents the <c>application/vnd.groove-injector</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-injector", IsBinary = true)]
    GRV,

    /// <summary>
    /// Represents the <c>application/srgs+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/srgs+xml", IsText = true)]
    GRXML,

    /// <summary>
    /// Represents the <c>application/x-font-ghostscript</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-ghostscript", IsBinary = true)]
    GSF,

    /// <summary>
    /// Represents the <c>application/x-gtar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-gtar", IsBinary = true)]
    GTAR,

    /// <summary>
    /// Represents the <c>application/vnd.groove-tool-message</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-tool-message", IsBinary = true)]
    GTM,

    /// <summary>
    /// Represents the <c>model/vnd.gtw</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.gtw", IsBinary = true)]
    GTW,

    /// <summary>
    /// Represents the <c>text/vnd.graphviz</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.graphviz", IsText = true)]
    GV,

    /// <summary>
    /// Represents the <c>application/gxf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/gxf", IsBinary = true)]
    GXF,

    /// <summary>
    /// Represents the <c>application/vnd.geonext</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.geonext", IsBinary = true)]
    GXT,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    H,

    /// <summary>
    /// Represents the <c>video/h261</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/h261", IsBinary = true)]
    H261,

    /// <summary>
    /// Represents the <c>video/h263</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/h263", IsBinary = true)]
    H263,

    /// <summary>
    /// Represents the <c>video/h264</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/h264", IsBinary = true)]
    H264,

    /// <summary>
    /// Represents the <c>application/vnd.hal+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hal+xml", IsText = true)]
    HAL,

    /// <summary>
    /// Represents the <c>application/vnd.hbci</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hbci", IsBinary = true)]
    HBCI,

    /// <summary>
    /// Represents the <c>application/x-hdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-hdf", IsBinary = true)]
    HDF,

    /// <summary>
    /// Represents the <c>text/x-c</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    HH,

    /// <summary>
    /// Represents the <c>application/winhlp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/winhlp", IsBinary = true)]
    HLP,

    /// <summary>
    /// Represents the <c>application/vnd.hp-hpgl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-hpgl", IsBinary = true)]
    HPGL,

    /// <summary>
    /// Represents the <c>application/vnd.hp-hpid</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-hpid", IsBinary = true)]
    HPID,

    /// <summary>
    /// Represents the <c>application/vnd.hp-hps</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-hps", IsBinary = true)]
    HPS,

    /// <summary>
    /// Represents the <c>application/mac-binhex40</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mac-binhex40", IsBinary = true)]
    HQX,

    /// <summary>
    /// Represents the <c>application/vnd.kenameaapp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kenameaapp", IsBinary = true)]
    HTKE,

    /// <summary>
    /// Represents the <c>text/html</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/html", IsText = true, FileExtension = "htm")]
    HTM,

    /// <summary>
    /// Represents the <c>text/html</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/html", IsText = true, FileExtension = "html")]
    HTML,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.hv-dic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-dic", IsBinary = true)]
    HVD,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.hv-voice</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-voice", IsBinary = true)]
    HVP,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.hv-script</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-script", IsBinary = true)]
    HVS,

    /// <summary>
    /// Represents the <c>application/vnd.intergeo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.intergeo", IsBinary = true)]
    I2G,

    /// <summary>
    /// Represents the <c>x-conference/x-cooltalk</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "x-conference/x-cooltalk", IsBinary = true)]
    IC,

    /// <summary>
    /// Represents the <c>application/vnd.iccprofile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.iccprofile", IsBinary = true)]
    ICC,

    /// <summary>
    /// Represents the <c>x-conference/x-cooltalk</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "x-conference/x-cooltalk", IsBinary = true)]
    ICE,

    /// <summary>
    /// Represents the <c>application/vnd.iccprofile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.iccprofile", IsBinary = true)]
    ICM,

    /// <summary>
    /// Represents the <c>image/x-icon</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-icon", IsBinary = true)]
    ICO,

    /// <summary>
    /// Represents the <c>text/calendar</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/calendar", IsText = true)]
    ICS,

    /// <summary>
    /// Represents the <c>image/ief</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/ief", IsBinary = true)]
    IEF,

    /// <summary>
    /// Represents the <c>text/calendar</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/calendar", IsText = true)]
    IFB,

    /// <summary>
    /// Represents the <c>application/vnd.shana.informed.formdata</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.formdata", IsBinary = true)]
    IFM,

    /// <summary>
    /// Represents the <c>model/iges</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/iges", IsBinary = true)]
    IGES,

    /// <summary>
    /// Represents the <c>application/vnd.igloader</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.igloader", IsBinary = true)]
    IGL,

    /// <summary>
    /// Represents the <c>application/vnd.insors.igm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.insors.igm", IsBinary = true)]
    IGM,

    /// <summary>
    /// Represents the <c>model/iges</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/iges", IsBinary = true)]
    IGS,

    /// <summary>
    /// Represents the <c>application/vnd.micrografx.igx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.micrografx.igx", IsBinary = true)]
    IGX,

    /// <summary>
    /// Represents the <c>application/vnd.shana.informed.interchange</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.interchange", IsBinary = true)]
    IIF,

    /// <summary>
    /// Represents the <c>application/vnd.accpac.simply.imp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.accpac.simply.imp", IsBinary = true)]
    IMP,

    /// <summary>
    /// Represents the <c>application/vnd.ms-ims</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-ims", IsBinary = true)]
    IMS,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    IN,

    /// <summary>
    /// Represents the <c>application/inkml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/inkml+xml", IsText = true)]
    INK,

    /// <summary>
    /// Represents the <c>application/inkml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/inkml+xml", IsText = true)]
    INKML,

    /// <summary>
    /// Represents the <c>application/x-install-instructions</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-install-instructions", IsBinary = true)]
    INSTALL,

    /// <summary>
    /// Represents the <c>application/vnd.astraea-software.iota</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.astraea-software.iota", IsBinary = true)]
    IOTA,

    /// <summary>
    /// Represents the <c>application/ipfix</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ipfix", IsBinary = true)]
    IPFIX,

    /// <summary>
    /// Represents the <c>application/vnd.shana.informed.package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.package", IsBinary = true)]
    IPK,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.rights-management</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.rights-management", IsBinary = true)]
    IRM,

    /// <summary>
    /// Represents the <c>application/vnd.irepository.package+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.irepository.package+xml", IsText = true)]
    IRP,

    /// <summary>
    /// Represents the <c>application/x-iso9660-image</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-iso9660-image", IsBinary = true)]
    ISO,

    /// <summary>
    /// Represents the <c>application/vnd.shana.informed.formtemplate</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.formtemplate", IsBinary = true)]
    ITP,

    /// <summary>
    /// Represents the <c>application/vnd.immervision-ivp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.immervision-ivp", IsBinary = true)]
    IVP,

    /// <summary>
    /// Represents the <c>application/vnd.immervision-ivu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.immervision-ivu", IsBinary = true)]
    IVU,

    /// <summary>
    /// Represents the <c>text/vnd.sun.j2me.app-descriptor</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.sun.j2me.app-descriptor", IsText = true)]
    JAD,

    /// <summary>
    /// Represents the <c>application/vnd.jam</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.jam", IsBinary = true)]
    JAM,

    /// <summary>
    /// Represents the <c>application/java-archive</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/java-archive", IsBinary = true)]
    JAR,

    /// <summary>
    /// Represents the <c>text/x-java-source</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-java-source", IsText = true)]
    JAVA,

    /// <summary>
    /// Represents the <c>application/vnd.jisp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.jisp", IsBinary = true)]
    JISP,

    /// <summary>
    /// Represents the <c>application/vnd.hp-jlyt</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-jlyt", IsBinary = true)]
    JLT,

    /// <summary>
    /// Represents the <c>application/x-java-jnlp-file</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-java-jnlp-file", IsBinary = true)]
    JNLP,

    /// <summary>
    /// Represents the <c>application/vnd.joost.joda-archive</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.joost.joda-archive", IsBinary = true)]
    JODA,

    /// <summary>
    /// Represents the <c>image/jp2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/jp2", IsBinary = true)]
    JP2,

    /// <summary>
    /// Represents the <c>image/jpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true)]
    JPE,

    /// <summary>
    /// Represents the <c>image/jpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true)]
    JPEG,

    /// <summary>
    /// Represents the <c>image/jpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true, FileExtension = "jpg")]
    JPG,

    /// <summary>
    /// Represents the <c>video/jpm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/jpm", IsBinary = true)]
    JPGM,

    /// <summary>
    /// Represents the <c>video/jpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/jpeg", IsBinary = true)]
    JPGV,

    /// <summary>
    /// Represents the <c>video/jpm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/jpm", IsBinary = true)]
    JPM,

    /// <summary>
    /// Represents the <c>application/javascript</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/javascript", IsText = true, FileExtension = "js")]
    JS,

    /// <summary>
    /// Represents the <c>application/json</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/json", IsText = true, FileExtension = "json")]
    JSON,

    /// <summary>
    /// Represents the <c>application/problem+json</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/problem+json", IsText = true)]
    JSONPROBLEM,

    /// <summary>
    /// Represents the <c>application/jsonml+json</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/jsonml+json", IsText = true)]
    JSONML,

    /// <summary>
    /// Represents the <c>audio/midi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    KAR,

    /// <summary>
    /// Represents the <c>application/vnd.kde.karbon</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.karbon", IsBinary = true)]
    KARBON,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kformula</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kformula", IsBinary = true)]
    KFO,

    /// <summary>
    /// Represents the <c>application/vnd.kidspiration</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kidspiration", IsBinary = true)]
    KIA,

    /// <summary>
    /// Represents the <c>application/vnd.google-earth.kml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.google-earth.kml+xml", IsText = true)]
    KML,

    /// <summary>
    /// Represents the <c>application/vnd.google-earth.kmz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.google-earth.kmz", IsBinary = true)]
    KMZ,

    /// <summary>
    /// Represents the <c>application/vnd.kinar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kinar", IsBinary = true)]
    KNE,

    /// <summary>
    /// Represents the <c>application/vnd.kinar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kinar", IsBinary = true)]
    KNP,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kontour</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kontour", IsBinary = true)]
    KON,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kpresenter</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kpresenter", IsBinary = true)]
    KPR,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kpresenter</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kpresenter", IsBinary = true)]
    KPT,

    /// <summary>
    /// Represents the <c>application/vnd.ds-keypoint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ds-keypoint", IsBinary = true)]
    KPXX,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kspread</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kspread", IsBinary = true)]
    KSP,

    /// <summary>
    /// Represents the <c>application/vnd.kahootz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kahootz", IsBinary = true)]
    KTR,

    /// <summary>
    /// Represents the <c>image/ktx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/ktx", IsBinary = true)]
    KTX,

    /// <summary>
    /// Represents the <c>application/vnd.kahootz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kahootz", IsBinary = true)]
    KTZ,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kword</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kword", IsBinary = true)]
    KWD,

    /// <summary>
    /// Represents the <c>application/vnd.kde.kword</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kde.kword", IsBinary = true)]
    KWT,

    /// <summary>
    /// Represents the <c>application/vnd.las.las+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.las.las+xml", IsText = true)]
    LASXML,

    /// <summary>
    /// Represents the <c>application/x-latex</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-latex", IsBinary = true)]
    LATEX,

    /// <summary>
    /// Represents the <c>application/vnd.llamagraphics.life-balance.desktop</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.llamagraphics.life-balance.desktop", IsBinary = true)]
    LBD,

    /// <summary>
    /// Represents the <c>application/vnd.llamagraphics.life-balance.exchange+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.llamagraphics.life-balance.exchange+xml", IsText = true)]
    LBE,

    /// <summary>
    /// Represents the <c>application/vnd.hhe.lesson-player</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hhe.lesson-player", IsBinary = true)]
    LES,

    /// <summary>
    /// Represents the <c>application/x-lzh-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-lzh-compressed", IsBinary = true)]
    LHA,

    /// <summary>
    /// Represents the <c>application/vnd.route66.link66+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.route66.link66+xml", IsText = true)]
    LINK66,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    LIST,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.modcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    LIST3820,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.modcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    LISTAFP,

    /// <summary>
    /// Represents the <c>application/x-ms-shortcut</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ms-shortcut", IsBinary = true)]
    LNK,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    LOG,

    /// <summary>
    /// Represents the <c>application/lost+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/lost+xml", IsText = true)]
    LOSTXML,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    LRF,

    /// <summary>
    /// Represents the <c>application/vnd.ms-lrm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-lrm", IsBinary = true)]
    LRM,

    /// <summary>
    /// Represents the <c>application/vnd.frogans.ltf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.frogans.ltf", IsBinary = true)]
    LTF,

    /// <summary>
    /// Represents the <c>audio/vnd.lucent.voice</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.lucent.voice", IsBinary = true)]
    LVP,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-wordpro</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-wordpro", IsBinary = true)]
    LWP,

    /// <summary>
    /// Represents the <c>application/x-lzh-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-lzh-compressed", IsBinary = true)]
    LZH,

    /// <summary>
    /// Represents the <c>application/x-msmediaview</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    M13,

    /// <summary>
    /// Represents the <c>application/x-msmediaview</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    M14,

    /// <summary>
    /// Represents the <c>video/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    M1V,

    /// <summary>
    /// Represents the <c>application/mp21</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mp21", IsBinary = true)]
    M21,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    M2A,

    /// <summary>
    /// Represents the <c>video/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    M2V,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    M3A,

    /// <summary>
    /// Represents the <c>audio/x-mpegurl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-mpegurl", IsBinary = true)]
    M3U,

    /// <summary>
    /// Represents the <c>application/vnd.apple.mpegurl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.apple.mpegurl", IsBinary = true)]
    M3U8,

    /// <summary>
    /// Represents the <c>audio/mp4a-latm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4A,

    /// <summary>
    /// Represents the <c>audio/mp4a-latm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4B,

    /// <summary>
    /// Represents the <c>audio/mp4a-latm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4P,

    /// <summary>
    /// Represents the <c>video/vnd.mpegurl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.mpegurl", IsBinary = true)]
    M4U,

    /// <summary>
    /// Represents the <c>video/x-m4v</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-m4v", IsBinary = true)]
    M4V,

    /// <summary>
    /// Represents the <c>application/mathematica</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    MA,

    /// <summary>
    /// Represents the <c>image/x-macpaint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    MAC,

    /// <summary>
    /// Represents the <c>application/mads+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mads+xml", IsText = true)]
    MADS,

    /// <summary>
    /// Represents the <c>application/vnd.ecowin.chart</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ecowin.chart", IsBinary = true)]
    MAG,

    /// <summary>
    /// Represents the <c>application/vnd.framemaker</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    MAKER,

    /// <summary>
    /// Represents the <c>application/x-troff-man</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff-man", IsBinary = true)]
    MAN,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    MAR,

    /// <summary>
    /// Represents the <c>application/mathml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mathml+xml", IsText = true)]
    MATHML,

    /// <summary>
    /// Represents the <c>application/mathematica</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    MB,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.mbk</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.mbk", IsBinary = true)]
    MBK,

    /// <summary>
    /// Represents the <c>application/mbox</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mbox", IsBinary = true)]
    MBOX,

    /// <summary>
    /// Represents the <c>application/vnd.medcalcdata</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.medcalcdata", IsBinary = true)]
    MC1,

    /// <summary>
    /// Represents the <c>application/vnd.mcd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mcd", IsBinary = true)]
    MCD,

    /// <summary>
    /// Represents the <c>text/vnd.curl.mcurl</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.curl.mcurl", IsText = true)]
    MCURL,

    /// <summary>
    /// Represents the <c>text/markdown</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/markdown", IsText = true)]
    MD,

    /// <summary>
    /// Represents the <c>application/x-msaccess</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msaccess", IsBinary = true)]
    MDB,

    /// <summary>
    /// Represents the <c>image/vnd.ms-modi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.ms-modi", IsBinary = true)]
    MDI,

    /// <summary>
    /// Represents the <c>application/x-troff-me</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff-me", IsBinary = true)]
    ME,

    /// <summary>
    /// Represents the <c>model/mesh</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    MESH,

    /// <summary>
    /// Represents the <c>application/metalink4+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/metalink4+xml", IsText = true)]
    META4,

    /// <summary>
    /// Represents the <c>application/metalink+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/metalink+xml", IsText = true)]
    METALINK,

    /// <summary>
    /// Represents the <c>application/mets+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mets+xml", IsText = true)]
    METS,

    /// <summary>
    /// Represents the <c>application/vnd.mfmp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mfmp", IsBinary = true)]
    MFM,

    /// <summary>
    /// Represents the <c>application/rpki-manifest</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rpki-manifest", IsBinary = true)]
    MFT,

    /// <summary>
    /// Represents the <c>application/vnd.osgeo.mapguide.package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.osgeo.mapguide.package", IsBinary = true)]
    MGP,

    /// <summary>
    /// Represents the <c>application/vnd.proteus.magazine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.proteus.magazine", IsBinary = true)]
    MGZ,

    /// <summary>
    /// Represents the <c>audio/midi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    MID,

    /// <summary>
    /// Represents the <c>audio/midi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    MIDI,

    /// <summary>
    /// Represents the <c>application/x-mie</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mie", IsBinary = true)]
    MIE,

    /// <summary>
    /// Represents the <c>application/vnd.mif</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mif", IsBinary = true)]
    MIF,

    /// <summary>
    /// Represents the <c>message/rfc822</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "message/rfc822", IsBinary = true)]
    MIME,

    /// <summary>
    /// Represents the <c>video/mj2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mj2", IsBinary = true)]
    MJ2,

    /// <summary>
    /// Represents the <c>video/mj2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mj2", IsBinary = true)]
    MJP2,

    /// <summary>
    /// Represents the <c>video/x-matroska</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MK3D,

    /// <summary>
    /// Represents the <c>audio/x-matroska</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-matroska", IsBinary = true)]
    MKA,

    /// <summary>
    /// Represents the <c>video/x-matroska</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MKS,

    /// <summary>
    /// Represents the <c>video/x-matroska</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MKV,

    /// <summary>
    /// Represents the <c>application/vnd.dolby.mlp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dolby.mlp", IsBinary = true)]
    MLP,

    /// <summary>
    /// Represents the <c>application/vnd.chipnuts.karaoke-mmd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.chipnuts.karaoke-mmd", IsBinary = true)]
    MMD,

    /// <summary>
    /// Represents the <c>application/vnd.smaf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.smaf", IsBinary = true)]
    MMF,

    /// <summary>
    /// Represents the <c>image/vnd.fujixerox.edmics-mmr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.fujixerox.edmics-mmr", IsBinary = true)]
    MMR,

    /// <summary>
    /// Represents the <c>video/x-mng</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-mng", IsBinary = true)]
    MNG,

    /// <summary>
    /// Represents the <c>application/x-msmoney</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmoney", IsBinary = true)]
    MNY,

    /// <summary>
    /// Represents the <c>application/x-mobipocket-ebook</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mobipocket-ebook", IsBinary = true)]
    MOBI,

    /// <summary>
    /// Represents the <c>application/mods+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mods+xml", IsText = true)]
    MODS,

    /// <summary>
    /// Represents the <c>video/quicktime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/quicktime", IsBinary = true)]
    MOV,

    /// <summary>
    /// Represents the <c>video/x-sgi-movie</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-sgi-movie", IsBinary = true)]
    MOVIE,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP2,

    /// <summary>
    /// Represents the <c>application/mp21</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mp21", IsBinary = true)]
    MP21,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP2A,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP3,

    /// <summary>
    /// Represents the <c>video/mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MP4,

    /// <summary>
    /// Represents the <c>audio/mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mp4", IsBinary = true)]
    MP4A,

    /// <summary>
    /// Represents the <c>application/mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mp4", IsBinary = true)]
    MP4S,

    /// <summary>
    /// Represents the <c>video/mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MP4V,

    /// <summary>
    /// Represents the <c>application/vnd.mophun.certificate</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mophun.certificate", IsBinary = true)]
    MPC,

    /// <summary>
    /// Represents the <c>video/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPE,

    /// <summary>
    /// Represents the <c>video/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPEG,

    /// <summary>
    /// Represents the <c>video/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPG,

    /// <summary>
    /// Represents the <c>video/mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MPG4,

    /// <summary>
    /// Represents the <c>audio/mpeg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MPGA,

    /// <summary>
    /// Represents the <c>application/vnd.apple.installer+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.apple.installer+xml", IsText = true)]
    MPKG,

    /// <summary>
    /// Represents the <c>application/vnd.blueice.multipass</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.blueice.multipass", IsBinary = true)]
    MPM,

    /// <summary>
    /// Represents the <c>application/vnd.mophun.application</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mophun.application", IsBinary = true)]
    MPN,

    /// <summary>
    /// Represents the <c>application/vnd.ms-project</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-project", IsBinary = true)]
    MPP,

    /// <summary>
    /// Represents the <c>application/vnd.ms-project</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-project", IsBinary = true)]
    MPT,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.minipay</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.minipay", IsBinary = true)]
    MPY,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.mqy</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.mqy", IsBinary = true)]
    MQY,

    /// <summary>
    /// Represents the <c>application/marc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/marc", IsBinary = true)]
    MRC,

    /// <summary>
    /// Represents the <c>application/marcxml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/marcxml+xml", IsText = true)]
    MRCX,

    /// <summary>
    /// Represents the <c>application/x-troff-ms</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff-ms", IsBinary = true)]
    MS,

    /// <summary>
    /// Represents the <c>application/mediaservercontrol+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mediaservercontrol+xml", IsText = true)]
    MSCML,

    /// <summary>
    /// Represents the <c>application/vnd.fdsn.mseed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.mseed", IsBinary = true)]
    MSEED,

    /// <summary>
    /// Represents the <c>application/vnd.mseq</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mseq", IsBinary = true)]
    MSEQ,

    /// <summary>
    /// Represents the <c>application/vnd.epson.msf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.epson.msf", IsBinary = true)]
    MSF,

    /// <summary>
    /// Represents the <c>model/mesh</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    MSH,

    /// <summary>
    /// Represents the <c>application/x-msdownload</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    MSI,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.msl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.msl", IsBinary = true)]
    MSL,

    /// <summary>
    /// Represents the <c>application/vnd.muvee.style</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.muvee.style", IsBinary = true)]
    MSTY,

    /// <summary>
    /// Represents the <c>model/vnd.mts</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.mts", IsBinary = true)]
    MTS,

    /// <summary>
    /// Represents the <c>application/vnd.musician</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.musician", IsBinary = true)]
    MUS,

    /// <summary>
    /// Represents the <c>application/vnd.recordare.musicxml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.recordare.musicxml+xml", IsText = true)]
    MUSICXML,

    /// <summary>
    /// Represents the <c>application/x-msmediaview</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    MVB,

    /// <summary>
    /// Represents the <c>application/vnd.mfer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mfer", IsBinary = true)]
    MWF,

    /// <summary>
    /// Represents the <c>application/mxf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mxf", IsBinary = true)]
    MXF,

    /// <summary>
    /// Represents the <c>application/vnd.recordare.musicxml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.recordare.musicxml", IsText = true)]
    MXL,

    /// <summary>
    /// Represents the <c>application/xv+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    MXML,

    /// <summary>
    /// Represents the <c>application/vnd.triscape.mxs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.triscape.mxs", IsBinary = true)]
    MXS,

    /// <summary>
    /// Represents the <c>video/vnd.mpegurl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.mpegurl", IsBinary = true)]
    MXU,

    /// <summary>
    /// Represents the <c>text/n3</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/n3", IsText = true)]
    N3,

    /// <summary>
    /// Represents the <c>application/mathematica</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    NB,

    /// <summary>
    /// Represents the <c>application/vnd.wolfram.player</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wolfram.player", IsBinary = true)]
    NBP,

    /// <summary>
    /// Represents the <c>application/x-netcdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-netcdf", IsBinary = true)]
    NC,

    /// <summary>
    /// Represents the <c>application/x-dtbncx+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-dtbncx+xml", IsText = true)]
    NCX,

    /// <summary>
    /// Represents the <c>text/x-nfo</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-nfo", IsText = true)]
    NFO,

    /// <summary>
    /// Represents the <c>application/vnd.nokia.n-gage.data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.nokia.n-gage.data", IsBinary = true)]
    NGDAT,

    /// <summary>
    /// Represents the <c>application/vnd.nitf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.nitf", IsBinary = true)]
    NITF,

    /// <summary>
    /// Represents the <c>application/vnd.neurolanguage.nlu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.neurolanguage.nlu", IsBinary = true)]
    NLU,

    /// <summary>
    /// Represents the <c>application/vnd.enliven</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.enliven", IsBinary = true)]
    NML,

    /// <summary>
    /// Represents the <c>application/vnd.noblenet-directory</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-directory", IsBinary = true)]
    NND,

    /// <summary>
    /// Represents the <c>application/vnd.noblenet-sealer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-sealer", IsBinary = true)]
    NNS,

    /// <summary>
    /// Represents the <c>application/vnd.noblenet-web</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-web", IsBinary = true)]
    NNW,

    /// <summary>
    /// Represents the <c>image/vnd.net-fpx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.net-fpx", IsBinary = true)]
    NPX,

    /// <summary>
    /// Represents the <c>application/x-conference</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-conference", IsBinary = true)]
    NSC,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-notes</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-notes", IsBinary = true)]
    NSF,

    /// <summary>
    /// Represents the <c>application/vnd.nitf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.nitf", IsBinary = true)]
    NTF,

    /// <summary>
    /// Represents the <c>application/x-nzb</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-nzb", IsBinary = true)]
    NZB,

    /// <summary>
    /// Represents the <c>application/vnd.fujitsu.oasys2</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys2", IsBinary = true)]
    OA2,

    /// <summary>
    /// Represents the <c>application/vnd.fujitsu.oasys3</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys3", IsBinary = true)]
    OA3,

    /// <summary>
    /// Represents the <c>application/vnd.fujitsu.oasys</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys", IsBinary = true)]
    OAS,

    /// <summary>
    /// Represents the <c>application/x-msbinder</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msbinder", IsBinary = true)]
    OBD,

    /// <summary>
    /// Represents the <c>application/x-tgif</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tgif", IsBinary = true)]
    OBJ,

    /// <summary>
    /// Represents the <c>application/oda</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/oda", IsBinary = true)]
    ODA,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.database</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.database", IsBinary = true)]
    ODB,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.chart</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.chart", IsBinary = true)]
    ODC,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.formula</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.formula", IsBinary = true)]
    ODF,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.formula-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.formula-template", IsBinary = true)]
    ODFT,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.graphics</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.graphics", IsBinary = true)]
    ODG,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.image</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.image", IsBinary = true)]
    ODI,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.text-master</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-master", IsBinary = true)]
    ODM,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.presentation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.presentation", IsBinary = true)]
    ODP,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.spreadsheet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.spreadsheet", IsBinary = true)]
    ODS,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.text</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text", IsBinary = true)]
    ODT,

    /// <summary>
    /// Represents the <c>audio/ogg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/ogg", IsBinary = true)]
    OGA,

    /// <summary>
    /// Represents the <c>video/ogg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/ogg", IsBinary = true)]
    OGG,

    /// <summary>
    /// Represents the <c>video/ogg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/ogg", IsBinary = true)]
    OGV,

    /// <summary>
    /// Represents the <c>application/ogg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ogg", IsBinary = true)]
    OGX,

    /// <summary>
    /// Represents the <c>application/omdoc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/omdoc+xml", IsText = true)]
    OMDOC,

    /// <summary>
    /// Represents the <c>application/onenote</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONEPKG,

    /// <summary>
    /// Represents the <c>application/onenote</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETMP,

    /// <summary>
    /// Represents the <c>application/onenote</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETOC,

    /// <summary>
    /// Represents the <c>application/onenote</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETOC2,

    /// <summary>
    /// Represents the <c>application/oebps-package+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/oebps-package+xml", IsText = true)]
    OPF,

    /// <summary>
    /// Represents the <c>text/x-opml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-opml", IsText = true)]
    OPML,

    /// <summary>
    /// Represents the <c>application/vnd.palm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.palm", IsBinary = true)]
    OPRC,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-organizer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-organizer", IsBinary = true)]
    ORG,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.openscoreformat</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.openscoreformat", IsBinary = true)]
    OSF,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.openscoreformat.osfpvg+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.openscoreformat.osfpvg+xml", IsText = true)]
    OSFPVG,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.chart-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.chart-template", IsBinary = true)]
    OTC,

    /// <summary>
    /// Represents the <c>application/x-font-otf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-otf", IsBinary = true)]
    OTF,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.graphics-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.graphics-template", IsBinary = true)]
    OTG,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.text-web</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-web", IsBinary = true)]
    OTH,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.image-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.image-template", IsBinary = true)]
    OTI,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.presentation-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.presentation-template", IsBinary = true)]
    OTP,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.spreadsheet-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.spreadsheet-template", IsBinary = true)]
    OTS,

    /// <summary>
    /// Represents the <c>application/vnd.oasis.opendocument.text-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-template", IsBinary = true)]
    OTT,

    /// <summary>
    /// Represents the <c>application/oxps</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/oxps", IsBinary = true)]
    OXPS,

    /// <summary>
    /// Represents the <c>application/vnd.openofficeorg.extension</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openofficeorg.extension", IsBinary = true)]
    OXT,

    /// <summary>
    /// Represents the <c>text/x-pascal</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-pascal", IsText = true)]
    P,

    /// <summary>
    /// Represents the <c>application/pkcs10</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkcs10", IsBinary = true)]
    P10,

    /// <summary>
    /// Represents the <c>application/x-pkcs12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-pkcs12", IsBinary = true)]
    P12,

    /// <summary>
    /// Represents the <c>application/x-pkcs7-certificates</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certificates", IsBinary = true)]
    P7B,

    /// <summary>
    /// Represents the <c>application/pkcs7-mime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkcs7-mime", IsBinary = true)]
    P7C,

    /// <summary>
    /// Represents the <c>application/pkcs7-mime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkcs7-mime", IsBinary = true)]
    P7M,

    /// <summary>
    /// Represents the <c>application/x-pkcs7-certreqresp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certreqresp", IsBinary = true)]
    P7R,

    /// <summary>
    /// Represents the <c>application/pkcs7-signature</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkcs7-signature", IsBinary = true)]
    P7S,

    /// <summary>
    /// Represents the <c>application/pkcs8</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkcs8", IsBinary = true)]
    P8,

    /// <summary>
    /// Represents the <c>text/x-pascal</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-pascal", IsText = true)]
    PAS,

    /// <summary>
    /// Represents the <c>application/vnd.pawaafile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pawaafile", IsBinary = true)]
    PAW,

    /// <summary>
    /// Represents the <c>application/vnd.powerbuilder6</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.powerbuilder6", IsBinary = true)]
    PBD,

    /// <summary>
    /// Represents the <c>image/x-portable-bitmap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-portable-bitmap", IsBinary = true)]
    PBM,

    /// <summary>
    /// Represents the <c>application/vnd.tcpdump.pcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    PCAP,

    /// <summary>
    /// Represents the <c>application/x-font-pcf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-pcf", IsBinary = true)]
    PCF,

    /// <summary>
    /// Represents the <c>application/vnd.hp-pcl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-pcl", IsBinary = true)]
    PCL,

    /// <summary>
    /// Represents the <c>application/vnd.hp-pclxl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.hp-pclxl", IsBinary = true)]
    PCLXL,

    /// <summary>
    /// Represents the <c>image/x-pict</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-pict", IsBinary = true)]
    PCT,

    /// <summary>
    /// Represents the <c>application/vnd.curl.pcurl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.curl.pcurl", IsBinary = true)]
    PCURL,

    /// <summary>
    /// Represents the <c>image/x-pcx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-pcx", IsBinary = true)]
    PCX,

    /// <summary>
    /// Represents the <c>applicaton/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "applicaton/octet-stream", IsBinary = true)]
    PDB,

    /// <summary>
    /// Represents the <c>application/pdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pdf", IsBinary = true, FileExtension = "pdf")]
    PDF,

    /// <summary>
    /// Represents the <c>application/x-font-type1</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFA,

    /// <summary>
    /// Represents the <c>application/x-font-type1</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFB,

    /// <summary>
    /// Represents the <c>application/x-font-type1</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFM,

    /// <summary>
    /// Represents the <c>application/font-tdpfr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/font-tdpfr", IsBinary = true)]
    PFR,

    /// <summary>
    /// Represents the <c>application/x-pkcs12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-pkcs12", IsBinary = true)]
    PFX,

    /// <summary>
    /// Represents the <c>image/x-portable-graymap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-portable-graymap", IsBinary = true)]
    PGM,

    /// <summary>
    /// Represents the <c>application/x-chess-pgn</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-chess-pgn", IsBinary = true)]
    PGN,

    /// <summary>
    /// Represents the <c>application/pgp-encrypted</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pgp-encrypted", IsBinary = true)]
    PGP,

    /// <summary>
    /// Represents the <c>image/x-pict</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-pict", IsBinary = true)]
    PIC,

    /// <summary>
    /// Represents the <c>image/pict</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/pict", IsBinary = true)]
    PICT,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    PKG,

    /// <summary>
    /// Represents the <c>application/pkixcmp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkixcmp", IsBinary = true)]
    PKI,

    /// <summary>
    /// Represents the <c>application/pkix-pkipath</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pkix-pkipath", IsBinary = true)]
    PKIPATH,

    /// <summary>
    /// Represents the <c>application/vnd.3gpp.pic-bw-large</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-large", IsBinary = true)]
    PLB,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.plc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.plc", IsBinary = true)]
    PLC,

    /// <summary>
    /// Represents the <c>application/vnd.pocketlearn</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pocketlearn", IsBinary = true)]
    PLF,

    /// <summary>
    /// Represents the <c>application/pls+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pls+xml", IsText = true)]
    PLS,

    /// <summary>
    /// Represents the <c>application/vnd.ctc-posml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ctc-posml", IsBinary = true)]
    PML,

    /// <summary>
    /// Represents the <c>image/png</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/png", IsBinary = true, FileExtension = "png")]
    PNG,

    /// <summary>
    /// Represents the <c>image/x-portable-anymap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-portable-anymap", IsBinary = true)]
    PNM,

    /// <summary>
    /// Represents the <c>image/x-macpaint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    PNT,

    /// <summary>
    /// Represents the <c>image/x-macpaint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    PNTG,

    /// <summary>
    /// Represents the <c>application/vnd.macports.portpkg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.macports.portpkg", IsBinary = true)]
    PORTPKG,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    POT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint.template.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.template.macroenabled.12", IsBinary = true)]
    POTM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.presentationml.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.template",
        IsBinary = true)]
    POTX,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint.addin.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.addin.macroenabled.12", IsBinary = true)]
    PPAM,

    /// <summary>
    /// Represents the <c>application/vnd.cups-ppd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cups-ppd", IsBinary = true)]
    PPD,

    /// <summary>
    /// Represents the <c>image/x-portable-pixmap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-portable-pixmap", IsBinary = true)]
    PPM,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    PPS,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint.slideshow.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.slideshow.macroenabled.12", IsBinary = true)]
    PPSM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.presentationml.slideshow</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
        IsBinary = true)]
    PPSX,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    PPT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint.presentation.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.presentation.macroenabled.12", IsBinary = true)]
    PPTM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.presentationml.presentation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        IsBinary = true)]
    PPTX,

    /// <summary>
    /// Represents the <c>application/vnd.palm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.palm", IsBinary = true)]
    PQA,

    /// <summary>
    /// Represents the <c>application/x-mobipocket-ebook</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mobipocket-ebook", IsBinary = true)]
    PRC,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-freelance</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-freelance", IsBinary = true)]
    PRE,

    /// <summary>
    /// Represents the <c>application/pics-rules</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pics-rules", IsBinary = true)]
    PRF,

    /// <summary>
    /// Represents the <c>application/postscript</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    PS,

    /// <summary>
    /// Represents the <c>application/vnd.3gpp.pic-bw-small</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-small", IsBinary = true)]
    PSB,

    /// <summary>
    /// Represents the <c>image/vnd.adobe.photoshop</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.adobe.photoshop", IsBinary = true)]
    PSD,

    /// <summary>
    /// Represents the <c>application/x-font-linux-psf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-linux-psf", IsBinary = true)]
    PSF,

    /// <summary>
    /// Represents the <c>application/pskc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pskc+xml", IsText = true)]
    PSKCXML,

    /// <summary>
    /// Represents the <c>application/vnd.pvi.ptid1</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pvi.ptid1", IsBinary = true)]
    PTID,

    /// <summary>
    /// Represents the <c>application/x-mspublisher</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mspublisher", IsBinary = true)]
    PUB,

    /// <summary>
    /// Represents the <c>application/vnd.3gpp.pic-bw-var</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-var", IsBinary = true)]
    PVB,

    /// <summary>
    /// Represents the <c>application/vnd.3m.post-it-notes</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.3m.post-it-notes", IsBinary = true)]
    PWN,

    /// <summary>
    /// Represents the <c>audio/vnd.ms-playready.media.pya</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.ms-playready.media.pya", IsBinary = true)]
    PYA,

    /// <summary>
    /// Represents the <c>video/vnd.ms-playready.media.pyv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.ms-playready.media.pyv", IsBinary = true)]
    PYV,

    /// <summary>
    /// Represents the <c>application/vnd.epson.quickanime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.epson.quickanime", IsBinary = true)]
    QAM,

    /// <summary>
    /// Represents the <c>application/vnd.intu.qbo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.intu.qbo", IsBinary = true)]
    QBO,

    /// <summary>
    /// Represents the <c>application/vnd.intu.qfx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.intu.qfx", IsBinary = true)]
    QFX,

    /// <summary>
    /// Represents the <c>application/vnd.publishare-delta-tree</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.publishare-delta-tree", IsBinary = true)]
    QPS,

    /// <summary>
    /// Represents the <c>video/quicktime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/quicktime", IsBinary = true)]
    QT,

    /// <summary>
    /// Represents the <c>image/x-quicktime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-quicktime", IsBinary = true)]
    QTI,

    /// <summary>
    /// Represents the <c>image/x-quicktime</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-quicktime", IsBinary = true)]
    QTIF,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QWD,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QWT,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXB,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXD,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXL,

    /// <summary>
    /// Represents the <c>application/vnd.quark.quarkxpress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXT,

    /// <summary>
    /// Represents the <c>audio/x-pn-realaudio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio", IsBinary = true)]
    RA,

    /// <summary>
    /// Represents the <c>audio/x-pn-realaudio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio", IsBinary = true)]
    RAM,

    /// <summary>
    /// Represents the <c>application/x-rar-compressed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-rar-compressed", IsBinary = true)]
    RAR,

    /// <summary>
    /// Represents the <c>image/x-cmu-raster</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-cmu-raster", IsBinary = true)]
    RAS,

    /// <summary>
    /// Represents the <c>application/vnd.ipunplugged.rcprofile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ipunplugged.rcprofile", IsBinary = true)]
    RCPROFILE,

    /// <summary>
    /// Represents the <c>application/rdf+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rdf+xml", IsText = true)]
    RDF,

    /// <summary>
    /// Represents the <c>application/vnd.data-vision.rdz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.data-vision.rdz", IsBinary = true)]
    RDZ,

    /// <summary>
    /// Represents the <c>application/vnd.businessobjects</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.businessobjects", IsBinary = true)]
    REP,

    /// <summary>
    /// Represents the <c>application/x-dtbresource+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-dtbresource+xml", IsText = true)]
    RES,

    /// <summary>
    /// Represents the <c>image/x-rgb</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-rgb", IsBinary = true)]
    RGB,

    /// <summary>
    /// Represents the <c>application/reginfo+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/reginfo+xml", IsText = true)]
    RIF,

    /// <summary>
    /// Represents the <c>audio/vnd.rip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.rip", IsBinary = true)]
    RIP,

    /// <summary>
    /// Represents the <c>application/x-research-info-systems</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-research-info-systems", IsBinary = true)]
    RIS,

    /// <summary>
    /// Represents the <c>application/resource-lists+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/resource-lists+xml", IsText = true)]
    RL,

    /// <summary>
    /// Represents the <c>image/vnd.fujixerox.edmics-rlc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.fujixerox.edmics-rlc", IsBinary = true)]
    RLC,

    /// <summary>
    /// Represents the <c>application/resource-lists-diff+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/resource-lists-diff+xml", IsText = true)]
    RLD,

    /// <summary>
    /// Represents the <c>application/vnd.rn-realmedia</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.rn-realmedia", IsBinary = true)]
    RM,

    /// <summary>
    /// Represents the <c>audio/midi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    RMI,

    /// <summary>
    /// Represents the <c>audio/x-pn-realaudio-plugin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio-plugin", IsBinary = true)]
    RMP,

    /// <summary>
    /// Represents the <c>application/vnd.jcp.javame.midlet-rms</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.jcp.javame.midlet-rms", IsBinary = true)]
    RMS,

    /// <summary>
    /// Represents the <c>application/vnd.rn-realmedia-vbr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.rn-realmedia-vbr", IsBinary = true)]
    RMVB,

    /// <summary>
    /// Represents the <c>application/relax-ng-compact-syntax</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/relax-ng-compact-syntax", IsBinary = true)]
    RNC,

    /// <summary>
    /// Represents the <c>application/rpki-roa</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rpki-roa", IsBinary = true)]
    ROA,

    /// <summary>
    /// Represents the <c>application/x-troff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    ROFF,

    /// <summary>
    /// Represents the <c>application/vnd.cloanto.rp9</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.cloanto.rp9", IsBinary = true)]
    RP9,

    /// <summary>
    /// Represents the <c>application/vnd.nokia.radio-presets</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.nokia.radio-presets", IsBinary = true)]
    RPSS,

    /// <summary>
    /// Represents the <c>application/vnd.nokia.radio-preset</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.nokia.radio-preset", IsBinary = true)]
    RPST,

    /// <summary>
    /// Represents the <c>application/sparql-query</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/sparql-query", IsBinary = true)]
    RQ,

    /// <summary>
    /// Represents the <c>application/rls-services+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rls-services+xml", IsText = true)]
    RS,

    /// <summary>
    /// Represents the <c>application/rsd+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rsd+xml", IsText = true)]
    RSD,

    /// <summary>
    /// Represents the <c>application/rss+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rss+xml", IsText = true)]
    RSS,

    /// <summary>
    /// Represents the <c>application/rtf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/rtf", IsBinary = true)]
    RTF,

    /// <summary>
    /// Represents the <c>text/richtext</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/richtext", IsText = true)]
    RTX,

    /// <summary>
    /// Represents the <c>text/x-asm</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-asm", IsText = true)]
    S,

    /// <summary>
    /// Represents the <c>audio/s3m</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/s3m", IsBinary = true)]
    S3M,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.smaf-audio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.smaf-audio", IsBinary = true)]
    SAF,

    /// <summary>
    /// Represents the <c>application/sbml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/sbml+xml", IsText = true)]
    SBML,

    /// <summary>
    /// Represents the <c>application/vnd.ibm.secure-container</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ibm.secure-container", IsBinary = true)]
    SC,

    /// <summary>
    /// Represents the <c>application/x-msschedule</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msschedule", IsBinary = true)]
    SCD,

    /// <summary>
    /// Represents the <c>application/vnd.lotus-screencam</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.lotus-screencam", IsBinary = true)]
    SCM,

    /// <summary>
    /// Represents the <c>application/scvp-cv-request</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/scvp-cv-request", IsBinary = true)]
    SCQ,

    /// <summary>
    /// Represents the <c>application/scvp-cv-response</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/scvp-cv-response", IsBinary = true)]
    SCS,

    /// <summary>
    /// Represents the <c>text/vnd.curl.scurl</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.curl.scurl", IsText = true)]
    SCURL,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.draw</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.draw", IsBinary = true)]
    SDA,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.calc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.calc", IsBinary = true)]
    SDC,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.impress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.impress", IsBinary = true)]
    SDD,

    /// <summary>
    /// Represents the <c>application/vnd.solent.sdkm+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.solent.sdkm+xml", IsText = true)]
    SDKD,

    /// <summary>
    /// Represents the <c>application/vnd.solent.sdkm+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.solent.sdkm+xml", IsText = true)]
    SDKM,

    /// <summary>
    /// Represents the <c>application/sdp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/sdp", IsBinary = true)]
    SDP,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.writer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer", IsBinary = true)]
    SDW,

    /// <summary>
    /// Represents the <c>application/vnd.seemail</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.seemail", IsBinary = true)]
    SEE,

    /// <summary>
    /// Represents the <c>application/vnd.fdsn.seed</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.seed", IsBinary = true)]
    SEED,

    /// <summary>
    /// Represents the <c>application/vnd.sema</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sema", IsBinary = true)]
    SEMA,

    /// <summary>
    /// Represents the <c>application/vnd.semd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.semd", IsBinary = true)]
    SEMD,

    /// <summary>
    /// Represents the <c>application/vnd.semf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.semf", IsBinary = true)]
    SEMF,

    /// <summary>
    /// Represents the <c>application/java-serialized-object</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/java-serialized-object", IsBinary = true)]
    SER,

    /// <summary>
    /// Represents the <c>application/set-payment-initiation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/set-payment-initiation", IsBinary = true)]
    SETPAY,

    /// <summary>
    /// Represents the <c>application/set-registration-initiation</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/set-registration-initiation", IsBinary = true)]
    SETREG,

    /// <summary>
    /// Represents the <c>application/vnd.spotfire.sfs</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.spotfire.sfs", IsBinary = true)]
    SFS,

    /// <summary>
    /// Represents the <c>text/x-sfv</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-sfv", IsText = true)]
    SFV,

    /// <summary>
    /// Represents the <c>image/sgi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/sgi", IsBinary = true)]
    SGI,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.writer-global</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer-global", IsBinary = true)]
    SGL,

    /// <summary>
    /// Represents the <c>text/sgml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/sgml", IsText = true)]
    SGM,

    /// <summary>
    /// Represents the <c>text/sgml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/sgml", IsText = true)]
    SGML,

    /// <summary>
    /// Represents the <c>application/x-sh</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-sh", IsBinary = true)]
    SH,

    /// <summary>
    /// Represents the <c>application/x-shar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-shar", IsBinary = true)]
    SHAR,

    /// <summary>
    /// Represents the <c>application/shf+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/shf+xml", IsText = true)]
    SHF,

    /// <summary>
    /// Represents the <c>image/x-mrsid-image</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-mrsid-image", IsBinary = true)]
    SID,

    /// <summary>
    /// Represents the <c>application/pgp-signature</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/pgp-signature", IsBinary = true)]
    SIG,

    /// <summary>
    /// Represents the <c>audio/silk</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/silk", IsBinary = true)]
    SIL,

    /// <summary>
    /// Represents the <c>model/mesh</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    SILO,

    /// <summary>
    /// Represents the <c>application/vnd.symbian.install</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.symbian.install", IsBinary = true)]
    SIS,

    /// <summary>
    /// Represents the <c>application/vnd.symbian.install</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.symbian.install", IsBinary = true)]
    SISX,

    /// <summary>
    /// Represents the <c>application/x-stuffit</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-stuffit", IsBinary = true)]
    SIT,

    /// <summary>
    /// Represents the <c>application/x-stuffitx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-stuffitx", IsBinary = true)]
    SITX,

    /// <summary>
    /// Represents the <c>application/x-koan</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKD,

    /// <summary>
    /// Represents the <c>application/x-koan</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKM,

    /// <summary>
    /// Represents the <c>application/x-koan</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKP,

    /// <summary>
    /// Represents the <c>application/x-koan</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-powerpoint.slide.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.slide.macroenabled.12", IsBinary = true)]
    SLDM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.presentationml.slide</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.slide",
        IsBinary = true)]
    SLDX,

    /// <summary>
    /// Represents the <c>application/vnd.epson.salt</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.epson.salt", IsBinary = true)]
    SLT,

    /// <summary>
    /// Represents the <c>application/vnd.stepmania.stepchart</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stepmania.stepchart", IsBinary = true)]
    SM,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.math</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.math", IsBinary = true)]
    SMF,

    /// <summary>
    /// Represents the <c>application/smil+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/smil+xml", IsText = true)]
    SMI,

    /// <summary>
    /// Represents the <c>application/smil+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/smil+xml", IsText = true)]
    SMIL,

    /// <summary>
    /// Represents the <c>video/x-smv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-smv", IsBinary = true)]
    SMV,

    /// <summary>
    /// Represents the <c>application/vnd.stepmania.package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stepmania.package", IsBinary = true)]
    SMZIP,

    /// <summary>
    /// Represents the <c>audio/basic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/basic", IsBinary = true)]
    SND,

    /// <summary>
    /// Represents the <c>application/x-font-snf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-snf", IsBinary = true)]
    SNF,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    SO,

    /// <summary>
    /// Represents the <c>application/x-pkcs7-certificates</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certificates", IsBinary = true)]
    SPC,

    /// <summary>
    /// Represents the <c>application/vnd.yamaha.smaf-phrase</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.smaf-phrase", IsBinary = true)]
    SPF,

    /// <summary>
    /// Represents the <c>application/x-futuresplash</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-futuresplash", IsBinary = true)]
    SPL,

    /// <summary>
    /// Represents the <c>text/vnd.in3d.spot</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.in3d.spot", IsText = true)]
    SPOT,

    /// <summary>
    /// Represents the <c>application/scvp-vp-response</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/scvp-vp-response", IsBinary = true)]
    SPP,

    /// <summary>
    /// Represents the <c>application/scvp-vp-request</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/scvp-vp-request", IsBinary = true)]
    SPQ,

    /// <summary>
    /// Represents the <c>audio/ogg</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/ogg", IsBinary = true)]
    SPX,

    /// <summary>
    /// Represents the <c>application/x-sql</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-sql", IsBinary = true)]
    SQL,

    /// <summary>
    /// Represents the <c>application/x-wais-source</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-wais-source", IsBinary = true)]
    SRC,

    /// <summary>
    /// Represents the <c>application/x-subrip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-subrip", IsBinary = true)]
    SRT,

    /// <summary>
    /// Represents the <c>application/sru+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/sru+xml", IsText = true)]
    SRU,

    /// <summary>
    /// Represents the <c>application/sparql-results+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/sparql-results+xml", IsText = true)]
    SRX,

    /// <summary>
    /// Represents the <c>application/ssdl+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ssdl+xml", IsText = true)]
    SSDL,

    /// <summary>
    /// Represents the <c>application/vnd.kodak-descriptor</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.kodak-descriptor", IsBinary = true)]
    SSE,

    /// <summary>
    /// Represents the <c>application/vnd.epson.ssf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.epson.ssf", IsBinary = true)]
    SSF,

    /// <summary>
    /// Represents the <c>application/ssml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/ssml+xml", IsText = true)]
    SSML,

    /// <summary>
    /// Represents the <c>application/vnd.sailingtracker.track</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sailingtracker.track", IsBinary = true)]
    ST,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.calc.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.calc.template", IsBinary = true)]
    STC,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.draw.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.draw.template", IsBinary = true)]
    STD,

    /// <summary>
    /// Represents the <c>application/vnd.wt.stf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wt.stf", IsBinary = true)]
    STF,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.impress.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.impress.template", IsBinary = true)]
    STI,

    /// <summary>
    /// Represents the <c>application/hyperstudio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/hyperstudio", IsBinary = true)]
    STK,

    /// <summary>
    /// Represents the <c>application/vnd.ms-pki.stl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-pki.stl", IsBinary = true)]
    STL,

    /// <summary>
    /// Represents the <c>application/vnd.pg.format</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pg.format", IsBinary = true)]
    STR,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.writer.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer.template", IsBinary = true)]
    STW,

    /// <summary>
    /// Represents the <c>text/vnd.dvb.subtitle</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.dvb.subtitle", IsText = true)]
    SUB,

    /// <summary>
    /// Represents the <c>application/vnd.sus-calendar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sus-calendar", IsBinary = true)]
    SUS,

    /// <summary>
    /// Represents the <c>application/vnd.sus-calendar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sus-calendar", IsBinary = true)]
    SUSP,

    /// <summary>
    /// Represents the <c>application/x-sv4cpio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-sv4cpio", IsBinary = true)]
    SV4CPIO,

    /// <summary>
    /// Represents the <c>application/x-sv4crc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-sv4crc", IsBinary = true)]
    SV4CRC,

    /// <summary>
    /// Represents the <c>application/vnd.dvb.service</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dvb.service", IsBinary = true)]
    SVC,

    /// <summary>
    /// Represents the <c>application/vnd.svd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.svd", IsBinary = true)]
    SVD,

    /// <summary>
    /// Represents the <c>image/svg+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/svg+xml", IsText = true)]
    SVG,

    /// <summary>
    /// Represents the <c>image/svg+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/svg+xml", IsText = true)]
    SVGZ,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    SWA,

    /// <summary>
    /// Represents the <c>application/x-shockwave-flash</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-shockwave-flash", IsBinary = true)]
    SWF,

    /// <summary>
    /// Represents the <c>application/vnd.aristanetworks.swi</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.aristanetworks.swi", IsBinary = true)]
    SWI,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.calc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.calc", IsBinary = true)]
    SXC,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.draw</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.draw", IsBinary = true)]
    SXD,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.writer.global</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer.global", IsBinary = true)]
    SXG,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.impress</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.impress", IsBinary = true)]
    SXI,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.math</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.math", IsBinary = true)]
    SXM,

    /// <summary>
    /// Represents the <c>application/vnd.sun.xml.writer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer", IsBinary = true)]
    SXW,

    /// <summary>
    /// Represents the <c>application/x-troff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    T,

    /// <summary>
    /// Represents the <c>application/x-t3vm-image</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-t3vm-image", IsBinary = true)]
    T3,

    /// <summary>
    /// Represents the <c>application/vnd.mynfc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mynfc", IsBinary = true)]
    TAGLET,

    /// <summary>
    /// Represents the <c>application/vnd.tao.intent-module-archive</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.tao.intent-module-archive", IsBinary = true)]
    TAO,

    /// <summary>
    /// Represents the <c>application/x-tar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tar", IsBinary = true)]
    TAR,

    /// <summary>
    /// Represents the <c>application/vnd.3gpp2.tcap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.3gpp2.tcap", IsBinary = true)]
    TCAP,

    /// <summary>
    /// Represents the <c>application/x-tcl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tcl", IsBinary = true)]
    TCL,

    /// <summary>
    /// Represents the <c>application/vnd.smart.teacher</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.smart.teacher", IsBinary = true)]
    TEACHER,

    /// <summary>
    /// Represents the <c>application/tei+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/tei+xml", IsText = true)]
    TEI,

    /// <summary>
    /// Represents the <c>application/tei+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/tei+xml", IsText = true)]
    TEICORPUS,

    /// <summary>
    /// Represents the <c>application/x-tex</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tex", IsBinary = true)]
    TEX,

    /// <summary>
    /// Represents the <c>application/x-texinfo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-texinfo", IsBinary = true)]
    TEXI,

    /// <summary>
    /// Represents the <c>application/x-texinfo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-texinfo", IsBinary = true)]
    TEXINFO,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    TEXT,

    /// <summary>
    /// Represents the <c>application/thraud+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/thraud+xml", IsText = true)]
    TFI,

    /// <summary>
    /// Represents the <c>application/x-tex-tfm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-tex-tfm", IsBinary = true)]
    TFM,

    /// <summary>
    /// Represents the <c>image/x-tga</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-tga", IsBinary = true)]
    TGA,

    /// <summary>
    /// Represents the <c>application/vnd.ms-officetheme</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-officetheme", IsBinary = true)]
    THMX,

    /// <summary>
    /// Represents the <c>image/tiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/tiff", IsBinary = true)]
    TIF,

    /// <summary>
    /// Represents the <c>image/tiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/tiff", IsBinary = true)]
    TIFF,

    /// <summary>
    /// Represents the <c>application/vnd.tmobile-livetv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.tmobile-livetv", IsBinary = true)]
    TMO,

    /// <summary>
    /// Represents the <c>application/x-bittorrent</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-bittorrent", IsBinary = true)]
    TORRENT,

    /// <summary>
    /// Represents the <c>application/vnd.groove-tool-template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-tool-template", IsBinary = true)]
    TPL,

    /// <summary>
    /// Represents the <c>application/vnd.trid.tpt</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.trid.tpt", IsBinary = true)]
    TPT,

    /// <summary>
    /// Represents the <c>application/x-troff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    TR,

    /// <summary>
    /// Represents the <c>application/vnd.trueapp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.trueapp", IsBinary = true)]
    TRA,

    /// <summary>
    /// Represents the <c>application/x-msterminal</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msterminal", IsBinary = true)]
    TRM,

    /// <summary>
    /// Represents the <c>application/timestamped-data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/timestamped-data", IsBinary = true)]
    TSD,

    /// <summary>
    /// Represents the <c>text/tab-separated-values</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/tab-separated-values", IsText = true)]
    TSV,

    /// <summary>
    /// Represents the <c>application/x-font-ttf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-ttf", IsBinary = true)]
    TTC,

    /// <summary>
    /// Represents the <c>application/x-font-ttf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-font-ttf", IsBinary = true)]
    TTF,

    /// <summary>
    /// Represents the <c>text/turtle</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/turtle", IsText = true)]
    TTL,

    /// <summary>
    /// Represents the <c>application/vnd.simtech-mindmapper</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.simtech-mindmapper", IsBinary = true)]
    TWD,

    /// <summary>
    /// Represents the <c>application/vnd.simtech-mindmapper</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.simtech-mindmapper", IsBinary = true)]
    TWDS,

    /// <summary>
    /// Represents the <c>application/vnd.genomatix.tuxedo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.genomatix.tuxedo", IsBinary = true)]
    TXD,

    /// <summary>
    /// Represents the <c>application/vnd.mobius.txf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mobius.txf", IsBinary = true)]
    TXF,

    /// <summary>
    /// Represents the <c>text/plain</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    TXT,

    /// <summary>
    /// Represents the <c>application/x-authorware-bin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    U32,

    /// <summary>
    /// Represents the <c>application/x-debian-package</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-debian-package", IsBinary = true)]
    UDEB,

    /// <summary>
    /// Represents the <c>application/vnd.ufdl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ufdl", IsBinary = true)]
    UFD,

    /// <summary>
    /// Represents the <c>application/vnd.ufdl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ufdl", IsBinary = true)]
    UFDL,

    /// <summary>
    /// Represents the <c>application/x-glulx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-glulx", IsBinary = true)]
    ULX,

    /// <summary>
    /// Represents the <c>application/vnd.umajin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.umajin", IsBinary = true)]
    UMJ,

    /// <summary>
    /// Represents the <c>application/vnd.unity</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.unity", IsBinary = true)]
    UNITYWEB,

    /// <summary>
    /// Represents the <c>application/vnd.uoml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.uoml+xml", IsText = true)]
    UOML,

    /// <summary>
    /// Represents the <c>text/uri-list</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URI,

    /// <summary>
    /// Represents the <c>text/uri-list</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URIS,

    /// <summary>
    /// Represents the <c>text/uri-list</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URLS,

    /// <summary>
    /// Represents the <c>application/x-ustar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ustar", IsBinary = true)]
    USTAR,

    /// <summary>
    /// Represents the <c>application/vnd.uiq.theme</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.uiq.theme", IsBinary = true)]
    UTZ,

    /// <summary>
    /// Represents the <c>text/x-uuencode</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-uuencode", IsText = true)]
    UU,

    /// <summary>
    /// Represents the <c>audio/vnd.dece.audio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.dece.audio", IsBinary = true)]
    UVA,

    /// <summary>
    /// Represents the <c>application/vnd.dece.data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVD,

    /// <summary>
    /// Represents the <c>application/vnd.dece.data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVF,

    /// <summary>
    /// Represents the <c>image/vnd.dece.graphic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVG,

    /// <summary>
    /// Represents the <c>video/vnd.dece.hd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.hd", IsBinary = true)]
    UVH,

    /// <summary>
    /// Represents the <c>image/vnd.dece.graphic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVI,

    /// <summary>
    /// Represents the <c>video/vnd.dece.mobile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.mobile", IsBinary = true)]
    UVM,

    /// <summary>
    /// Represents the <c>video/vnd.dece.pd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.pd", IsBinary = true)]
    UVP,

    /// <summary>
    /// Represents the <c>video/vnd.dece.sd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.sd", IsBinary = true)]
    UVS,

    /// <summary>
    /// Represents the <c>application/vnd.dece.ttml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.ttml+xml", IsText = true)]
    UVT,

    /// <summary>
    /// Represents the <c>video/vnd.uvvu.mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.uvvu.mp4", IsBinary = true)]
    UVU,

    /// <summary>
    /// Represents the <c>video/vnd.dece.video</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.video", IsBinary = true)]
    UVV,

    /// <summary>
    /// Represents the <c>audio/vnd.dece.audio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/vnd.dece.audio", IsBinary = true)]
    UVVA,

    /// <summary>
    /// Represents the <c>application/vnd.dece.data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVVD,

    /// <summary>
    /// Represents the <c>application/vnd.dece.data</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVVF,

    /// <summary>
    /// Represents the <c>image/vnd.dece.graphic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVVG,

    /// <summary>
    /// Represents the <c>video/vnd.dece.hd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.hd", IsBinary = true)]
    UVVH,

    /// <summary>
    /// Represents the <c>image/vnd.dece.graphic</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVVI,

    /// <summary>
    /// Represents the <c>video/vnd.dece.mobile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.mobile", IsBinary = true)]
    UVVM,

    /// <summary>
    /// Represents the <c>video/vnd.dece.pd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.pd", IsBinary = true)]
    UVVP,

    /// <summary>
    /// Represents the <c>video/vnd.dece.sd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.sd", IsBinary = true)]
    UVVS,

    /// <summary>
    /// Represents the <c>application/vnd.dece.ttml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.ttml+xml", IsText = true)]
    UVVT,

    /// <summary>
    /// Represents the <c>video/vnd.uvvu.mp4</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.uvvu.mp4", IsBinary = true)]
    UVVU,

    /// <summary>
    /// Represents the <c>video/vnd.dece.video</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.dece.video", IsBinary = true)]
    UVVV,

    /// <summary>
    /// Represents the <c>application/vnd.dece.unspecified</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.unspecified", IsBinary = true)]
    UVVX,

    /// <summary>
    /// Represents the <c>application/vnd.dece.zip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.zip", IsBinary = true)]
    UVVZ,

    /// <summary>
    /// Represents the <c>application/vnd.dece.unspecified</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.unspecified", IsBinary = true)]
    UVX,

    /// <summary>
    /// Represents the <c>application/vnd.dece.zip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.dece.zip", IsBinary = true)]
    UVZ,

    /// <summary>
    /// Represents the <c>text/vcard</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vcard", IsText = true)]
    VCARD,

    /// <summary>
    /// Represents the <c>application/x-cdlink</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-cdlink", IsBinary = true)]
    VCD,

    /// <summary>
    /// Represents the <c>text/x-vcard</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-vcard", IsText = true)]
    VCF,

    /// <summary>
    /// Represents the <c>application/vnd.groove-vcard</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.groove-vcard", IsBinary = true)]
    VCG,

    /// <summary>
    /// Represents the <c>text/x-vcalendar</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/x-vcalendar", IsText = true)]
    VCS,

    /// <summary>
    /// Represents the <c>application/vnd.vcx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.vcx", IsBinary = true)]
    VCX,

    /// <summary>
    /// Represents the <c>application/vnd.visionary</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.visionary", IsBinary = true)]
    VIS,

    /// <summary>
    /// Represents the <c>video/vnd.vivo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/vnd.vivo", IsBinary = true)]
    VIV,

    /// <summary>
    /// Represents the <c>video/x-ms-vob</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-vob", IsBinary = true)]
    VOB,

    /// <summary>
    /// Represents the <c>application/vnd.stardivision.writer</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer", IsBinary = true)]
    VOR,

    /// <summary>
    /// Represents the <c>application/x-authorware-bin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    VOX,

    /// <summary>
    /// Represents the <c>model/vrml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vrml", IsBinary = true)]
    VRML,

    /// <summary>
    /// Represents the <c>application/vnd.visio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSD,

    /// <summary>
    /// Represents the <c>application/vnd.vsf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.vsf", IsBinary = true)]
    VSF,

    /// <summary>
    /// Represents the <c>application/vnd.visio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSS,

    /// <summary>
    /// Represents the <c>application/vnd.visio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VST,

    /// <summary>
    /// Represents the <c>application/vnd.visio</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSW,

    /// <summary>
    /// Represents the <c>model/vnd.vtu</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vnd.vtu", IsBinary = true)]
    VTU,

    /// <summary>
    /// Represents the <c>application/voicexml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/voicexml+xml", IsText = true)]
    VXML,

    /// <summary>
    /// Represents the <c>application/x-director</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    W3D,

    /// <summary>
    /// Represents the <c>application/x-doom</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-doom", IsBinary = true)]
    WAD,

    /// <summary>
    /// Represents the <c>audio/x-wav</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-wav", IsBinary = true)]
    WAV,

    /// <summary>
    /// Represents the <c>audio/x-ms-wax</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-ms-wax", IsBinary = true)]
    WAX,

    /// <summary>
    /// Represents the <c>image/vnd.wap.wbmp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.wap.wbmp", IsBinary = true)]
    WBMP,

    /// <summary>
    /// Represents the <c>application/vnd.wap.wbxml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wap.wbxml", IsText = true)]
    WBMXL,

    /// <summary>
    /// Represents the <c>application/vnd.criticaltools.wbs+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.criticaltools.wbs+xml", IsText = true)]
    WBS,

    /// <summary>
    /// Represents the <c>application/vnd.wap.wbxml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wap.wbxml", IsText = true)]
    WBXML,

    /// <summary>
    /// Represents the <c>application/vnd.ms-works</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WCM,

    /// <summary>
    /// Represents the <c>application/vnd.ms-works</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WDB,

    /// <summary>
    /// Represents the <c>image/vnd.ms-photo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.ms-photo", IsBinary = true)]
    WDP,

    /// <summary>
    /// Represents the <c>audio/webm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/webm", IsBinary = true)]
    WEBA,

    /// <summary>
    /// Represents the <c>video/webm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/webm", IsBinary = true)]
    WEBM,

    /// <summary>
    /// Represents the <c>image/webp</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/webp", IsBinary = true)]
    WEBP,

    /// <summary>
    /// Represents the <c>application/vnd.pmi.widget</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.pmi.widget", IsBinary = true)]
    WG,

    /// <summary>
    /// Represents the <c>application/widget</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/widget", IsBinary = true)]
    WGT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-works</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WKS,

    /// <summary>
    /// Represents the <c>video/x-ms-wm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-wm", IsBinary = true)]
    WM,

    /// <summary>
    /// Represents the <c>audio/x-ms-wma</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/x-ms-wma", IsBinary = true)]
    WMA,

    /// <summary>
    /// Represents the <c>application/x-ms-wmd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ms-wmd", IsBinary = true)]
    WMD,

    /// <summary>
    /// Represents the <c>application/x-msmetafile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    WMF,

    /// <summary>
    /// Represents the <c>text/vnd.wap.wml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.wap.wml", IsText = true)]
    WML,

    /// <summary>
    /// Represents the <c>application/vnd.wap.wmlc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wap.wmlc", IsBinary = true)]
    WMLC,

    /// <summary>
    /// Represents the <c>text/vnd.wap.wmlscript</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/vnd.wap.wmlscript", IsText = true)]
    WMLS,

    /// <summary>
    /// Represents the <c>application/vnd.wap.wmlscriptc</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wap.wmlscriptc", IsBinary = true)]
    WMLSC,

    /// <summary>
    /// Represents the <c>video/x-ms-wmv</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-wmv", IsBinary = true)]
    WMV,

    /// <summary>
    /// Represents the <c>video/x-ms-wmx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-wmx", IsBinary = true)]
    WMX,

    /// <summary>
    /// Represents the <c>application/x-msmetafile</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    WMZ,

    /// <summary>
    /// Represents the <c>application/font-woff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/font-woff", IsBinary = true)]
    WOFF,

    /// <summary>
    /// Represents the <c>application/vnd.wordperfect</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wordperfect", IsBinary = true)]
    WPD,

    /// <summary>
    /// Represents the <c>application/vnd.ms-wpl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-wpl", IsBinary = true)]
    WPL,

    /// <summary>
    /// Represents the <c>application/vnd.ms-works</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WPS,

    /// <summary>
    /// Represents the <c>application/vnd.wqd</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.wqd", IsBinary = true)]
    WQD,

    /// <summary>
    /// Represents the <c>application/x-mswrite</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-mswrite", IsBinary = true)]
    WRI,

    /// <summary>
    /// Represents the <c>model/vrml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/vrml", IsBinary = true)]
    WRL,

    /// <summary>
    /// Represents the <c>application/wsdl+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/wsdl+xml", IsText = true)]
    WSDL,

    /// <summary>
    /// Represents the <c>application/wspolicy+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/wspolicy+xml", IsText = true)]
    WSPOLICY,

    /// <summary>
    /// Represents the <c>application/vnd.webturbo</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.webturbo", IsBinary = true)]
    WTB,

    /// <summary>
    /// Represents the <c>video/x-ms-wvx</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "video/x-ms-wvx", IsBinary = true)]
    WVX,

    /// <summary>
    /// Represents the <c>application/x-authorware-bin</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    X32,

    /// <summary>
    /// Represents the <c>model/x3d+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+xml", IsText = true)]
    X3D,

    /// <summary>
    /// Represents the <c>model/x3d+binary</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+binary", IsBinary = true)]
    X3DB,

    /// <summary>
    /// Represents the <c>model/x3d+binary</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+binary", IsBinary = true)]
    X3DBZ,

    /// <summary>
    /// Represents the <c>model/x3d+vrml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+vrml", IsBinary = true)]
    X3DV,

    /// <summary>
    /// Represents the <c>model/x3d+vrml</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+vrml", IsBinary = true)]
    X3DVZ,

    /// <summary>
    /// Represents the <c>model/x3d+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "model/x3d+xml", IsText = true)]
    X3DZ,

    /// <summary>
    /// Represents the <c>application/xaml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xaml+xml", IsText = true)]
    XAML,

    /// <summary>
    /// Represents the <c>application/x-silverlight-app</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-silverlight-app", IsBinary = true)]
    XAP,

    /// <summary>
    /// Represents the <c>application/vnd.xara</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.xara", IsBinary = true)]
    XAR,

    /// <summary>
    /// Represents the <c>application/x-ms-xbap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-ms-xbap", IsBinary = true)]
    XBAP,

    /// <summary>
    /// Represents the <c>application/vnd.fujixerox.docuworks.binder</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.docuworks.binder", IsBinary = true)]
    XBD,

    /// <summary>
    /// Represents the <c>image/x-xbitmap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-xbitmap", IsBinary = true)]
    XBM,

    /// <summary>
    /// Represents the <c>application/xcap-diff+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xcap-diff+xml", IsText = true)]
    XDF,

    /// <summary>
    /// Represents the <c>application/vnd.syncml.dm+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.syncml.dm+xml", IsText = true)]
    XDM,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.xdp+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.xdp+xml", IsText = true)]
    XDP,

    /// <summary>
    /// Represents the <c>application/dssc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/dssc+xml", IsText = true)]
    XDSSC,

    /// <summary>
    /// Represents the <c>application/vnd.fujixerox.docuworks</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.docuworks", IsBinary = true)]
    XDW,

    /// <summary>
    /// Represents the <c>application/xenc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xenc+xml", IsText = true)]
    XENC,

    /// <summary>
    /// Represents the <c>application/patch-ops-error+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/patch-ops-error+xml", IsText = true)]
    XER,

    /// <summary>
    /// Represents the <c>application/vnd.adobe.xfdf</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.adobe.xfdf", IsBinary = true)]
    XFDF,

    /// <summary>
    /// Represents the <c>application/vnd.xfdl</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.xfdl", IsBinary = true)]
    XFDL,

    /// <summary>
    /// Represents the <c>application/xhtml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xhtml+xml", IsText = true)]
    XHT,

    /// <summary>
    /// Represents the <c>application/xhtml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xhtml+xml", IsText = true)]
    XHTML,

    /// <summary>
    /// Represents the <c>application/xv+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XHVML,

    /// <summary>
    /// Represents the <c>image/vnd.xiff</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/vnd.xiff", IsBinary = true)]
    XIF,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLA,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel.addin.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.addin.macroenabled.12", IsBinary = true)]
    XLAM,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLC,

    /// <summary>
    /// Represents the <c>application/x-xliff+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-xliff+xml", IsText = true)]
    XLF,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLM,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLS,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel.sheet.binary.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.sheet.binary.macroenabled.12", IsBinary = true)]
    XLSB,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel.sheet.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.sheet.macroenabled.12", IsBinary = true)]
    XLSM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        IsBinary = true)]
    XLSX,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLT,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel.template.macroenabled.12</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.template.macroenabled.12", IsBinary = true)]
    XLTM,

    /// <summary>
    /// Represents the <c>application/vnd.openxmlformats-officedocument.spreadsheetml.template</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
        IsBinary = true)]
    XLTX,

    /// <summary>
    /// Represents the <c>application/vnd.ms-excel</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLW,

    /// <summary>
    /// Represents the <c>audio/xm</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "audio/xm", IsBinary = true)]
    XM,

    /// <summary>
    /// Represents the <c>application/xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xml", IsText = true)]
    XML,

    /// <summary>
    /// Represents the <c>application/vnd.olpc-sugar</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.olpc-sugar", IsBinary = true)]
    XO,

    /// <summary>
    /// Represents the <c>application/xop+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xop+xml", IsText = true)]
    XOP,

    /// <summary>
    /// Represents the <c>application/x-xpinstall</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-xpinstall", IsBinary = true)]
    XPI,

    /// <summary>
    /// Represents the <c>application/xproc+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xproc+xml", IsText = true)]
    XPL,

    /// <summary>
    /// Represents the <c>image/x-xpixmap</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-xpixmap", IsBinary = true)]
    XPM,

    /// <summary>
    /// Represents the <c>application/vnd.is-xpr</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.is-xpr", IsBinary = true)]
    XPR,

    /// <summary>
    /// Represents the <c>application/vnd.ms-xpsdocument</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.ms-xpsdocument", IsBinary = true)]
    XPS,

    /// <summary>
    /// Represents the <c>application/vnd.intercon.formnet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.intercon.formnet", IsBinary = true)]
    XPW,

    /// <summary>
    /// Represents the <c>application/vnd.intercon.formnet</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.intercon.formnet", IsBinary = true)]
    XPX,

    /// <summary>
    /// Represents the <c>application/xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xml", IsText = true)]
    XSL,

    /// <summary>
    /// Represents the <c>application/xslt+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xslt+xml", IsText = true)]
    XSLT,

    /// <summary>
    /// Represents the <c>application/vnd.syncml+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.syncml+xml", IsText = true)]
    XSM,

    /// <summary>
    /// Represents the <c>application/xspf+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xspf+xml", IsText = true)]
    XSPF,

    /// <summary>
    /// Represents the <c>application/vnd.mozilla.xul+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.mozilla.xul+xml", IsText = true)]
    XUL,

    /// <summary>
    /// Represents the <c>application/xv+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XVM,

    /// <summary>
    /// Represents the <c>application/xv+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XVML,

    /// <summary>
    /// Represents the <c>image/x-xwindowdump</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "image/x-xwindowdump", IsBinary = true)]
    XWD,

    /// <summary>
    /// Represents the <c>chemical/x-xyz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "chemical/x-xyz", IsBinary = true)]
    XYZ,

    /// <summary>
    /// Represents the <c>application/x-xz</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-xz", IsBinary = true)]
    XZ,

    /// <summary>
    /// Represents the <c>text/yaml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "text/yaml", IsText = true)]
    YAML,

    /// <summary>
    /// Represents the <c>application/yang</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/yang", IsBinary = true)]
    YANG,

    /// <summary>
    /// Represents the <c>application/yin+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/yin+xml", IsText = true)]
    YIN,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z1,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z2,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z3,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z4,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z5,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z6,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z7,

    /// <summary>
    /// Represents the <c>application/x-zmachine</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z8,

    /// <summary>
    /// Represents the <c>application/vnd.zzazz.deck+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.zzazz.deck+xml", IsText = true)]
    ZAZ,

    /// <summary>
    /// Represents the <c>application/zip</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/zip", IsBinary = true)]
    ZIP,

    /// <summary>
    /// Represents the <c>application/vnd.zul</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.zul", IsBinary = true)]
    ZIR,

    /// <summary>
    /// Represents the <c>application/vnd.zul</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.zul", IsBinary = true)]
    ZIRZ,

    /// <summary>
    /// Represents the <c>application/vnd.handheld-entertainment+xml</c> text content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/vnd.handheld-entertainment+xml", IsText = true)]
    ZMM,

    /// <summary>
    /// Represents the <c>application/octet-stream</c> binary content type.
    /// </summary>
    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DEFAULT
}
