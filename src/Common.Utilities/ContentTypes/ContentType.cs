// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public enum ContentType // https://mimetype.io/all-types
{
    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    AAB,

    [ContentTypeMetadate(MimeType = "audio/x-aac", IsBinary = true)]
    AAC,

    [ContentTypeMetadate(MimeType = "application/x-authorware-map", IsBinary = true)]
    AAM,

    [ContentTypeMetadate(MimeType = "application/x-authorware-seg", IsBinary = true)]
    AAS,

    [ContentTypeMetadate(MimeType = "application/x-abiword", IsBinary = true)]
    ABW,

    [ContentTypeMetadate(MimeType = "application/pkix-attr-cert", IsBinary = true)]
    AC,

    [ContentTypeMetadate(MimeType = "application/vnd.americandynamics.acc", IsBinary = true)]
    ACC,

    [ContentTypeMetadate(MimeType = "application/x-ace-compressed", IsBinary = true)]
    ACE,

    [ContentTypeMetadate(MimeType = "application/vnd.acucobol", IsBinary = true)]
    ACU,

    [ContentTypeMetadate(MimeType = "application/vnd.acucorp", IsBinary = true)]
    ACUTC,

    [ContentTypeMetadate(MimeType = "audio/adpcm", IsBinary = true)]
    ADP,

    [ContentTypeMetadate(MimeType = "application/vnd.audiograph", IsBinary = true)]
    AEP,

    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    AFM,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    AFP,

    [ContentTypeMetadate(MimeType = "application/vnd.ahead.space", IsBinary = true)]
    AHEAD,

    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    AI,

    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIF,

    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIFC,

    [ContentTypeMetadate(MimeType = "audio/x-aiff", IsBinary = true)]
    AIFF,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.air-application-installer-package+zip", IsBinary = true)]
    AIR,

    [ContentTypeMetadate(MimeType = "application/vnd.dvb.ait", IsBinary = true)]
    AIT,

    [ContentTypeMetadate(MimeType = "application/vnd.amiga.ami", IsBinary = true)]
    AMI,

    [ContentTypeMetadate(MimeType = "application/vnd.android.package-archive", IsBinary = true)]
    APK,

    [ContentTypeMetadate(MimeType = "text/cache-manifest", IsText = true)]
    APPCACHE,

    [ContentTypeMetadate(MimeType = "application/x-ms-application", IsBinary = true)]
    APPLICATION,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-approach", IsBinary = true)]
    APR,

    [ContentTypeMetadate(MimeType = "application/x-freearc", IsBinary = true)]
    ARC,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    ASC,

    [ContentTypeMetadate(MimeType = "video/x-ms-asf", IsBinary = true)]
    ASF,

    [ContentTypeMetadate(MimeType = "text/x-asm", IsText = true)]
    ASM,

    [ContentTypeMetadate(MimeType = "application/vnd.accpac.simply.aso", IsBinary = true)]
    ASO,

    [ContentTypeMetadate(MimeType = "video/x-ms-asf", IsBinary = true)]
    ASX,

    [ContentTypeMetadate(MimeType = "application/vnd.acucorp", IsBinary = true)]
    ATC,

    [ContentTypeMetadate(MimeType = "application/atom+xml", IsText = true)]
    ATOM,

    [ContentTypeMetadate(MimeType = "application/atomcat+xml", IsText = true)]
    ATOMCAT,

    [ContentTypeMetadate(MimeType = "application/atomsvc+xml", IsText = true)]
    ATOMSVC,

    [ContentTypeMetadate(MimeType = "application/vnd.antix.game-component", IsBinary = true)]
    ATX,

    [ContentTypeMetadate(MimeType = "audio/basic", IsBinary = true)]
    AU,

    [ContentTypeMetadate(MimeType = "video/x-msvideo", IsBinary = true)]
    AVI,

    [ContentTypeMetadate(MimeType = "application/applixware", IsBinary = true)]
    AW,

    [ContentTypeMetadate(MimeType = "application/vnd.airzip.filesecure.azf", IsBinary = true)]
    AZF,

    [ContentTypeMetadate(MimeType = "application/vnd.airzip.filesecure.azs", IsBinary = true)]
    AZS,

    [ContentTypeMetadate(MimeType = "application/vnd.amazon.ebook", IsBinary = true)]
    AZW,

    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    BAT,

    [ContentTypeMetadate(MimeType = "application/x-bcpio", IsBinary = true)]
    BCPIO,

    [ContentTypeMetadate(MimeType = "application/x-font-bdf", IsBinary = true)]
    BDF,

    [ContentTypeMetadate(MimeType = "application/vnd.syncml.dm+wbxml", IsText = true)]
    BDM,

    [ContentTypeMetadate(MimeType = "application/vnd.realvnc.bed", IsBinary = true)]
    BED,

    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasysprs", IsBinary = true)]
    BH2,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    BIN,

    [ContentTypeMetadate(MimeType = "application/x-blorb", IsBinary = true)]
    BLB,

    [ContentTypeMetadate(MimeType = "application/x-blorb", IsBinary = true)]
    BLORB,

    [ContentTypeMetadate(MimeType = "application/vnd.bmi", IsBinary = true)]
    BMI,

    [ContentTypeMetadate(MimeType = "image/bmp", IsBinary = true)]
    BMP,

    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    BOOK,

    [ContentTypeMetadate(MimeType = "application/vnd.previewsystems.box", IsBinary = true)]
    BOX,

    [ContentTypeMetadate(MimeType = "application/x-bzip2", IsBinary = true)]
    BOZ,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    BPK,

    [ContentTypeMetadate(MimeType = "image/prs.btif", IsBinary = true)]
    BTIF,

    [ContentTypeMetadate(MimeType = "application/x-bzip", IsBinary = true)]
    BZ,

    [ContentTypeMetadate(MimeType = "application/x-bzip2", IsBinary = true)]
    BZ2,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    C,

    [ContentTypeMetadate(MimeType = "application/vnd.cluetrust.cartomobile-config", IsBinary = true)]
    C11AMC,

    [ContentTypeMetadate(MimeType = "application/vnd.cluetrust.cartomobile-config-pkg", IsBinary = true)]
    C11AMZ,

    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4D,

    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4F,

    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4G,

    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4P,

    [ContentTypeMetadate(MimeType = "application/vnd.clonk.c4group", IsBinary = true)]
    C4U,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-cab-compressed", IsBinary = true)]
    CAB,

    [ContentTypeMetadate(MimeType = "audio/x-caf", IsBinary = true)]
    CAF,

    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    CAP,

    [ContentTypeMetadate(MimeType = "application/vnd.curl.car", IsBinary = true)]
    CAR,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-pki.seccat", IsBinary = true)]
    CAT,

    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CB7,

    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBA,

    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBR,

    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBT,

    [ContentTypeMetadate(MimeType = "application/x-cbr", IsBinary = true)]
    CBZ,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CC,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CCT,

    [ContentTypeMetadate(MimeType = "application/ccxml+xml", IsText = true)]
    CCXML,

    [ContentTypeMetadate(MimeType = "application/vnd.contact.cmsg", IsBinary = true)]
    CDBCMSG,

    [ContentTypeMetadate(MimeType = "application/x-netcdf", IsBinary = true)]
    CDF,

    [ContentTypeMetadate(MimeType = "application/vnd.mediastation.cdkey", IsBinary = true)]
    CDKEY,

    [ContentTypeMetadate(MimeType = "application/cdmi-capability", IsBinary = true)]
    CDMIA,

    [ContentTypeMetadate(MimeType = "application/cdmi-container", IsBinary = true)]
    CDMIC,

    [ContentTypeMetadate(MimeType = "application/cdmi-domain", IsBinary = true)]
    CDMID,

    [ContentTypeMetadate(MimeType = "application/cdmi-object", IsBinary = true)]
    CDMIO,

    [ContentTypeMetadate(MimeType = "application/cdmi-queue", IsBinary = true)]
    CDMIQ,

    [ContentTypeMetadate(MimeType = "chemical/x-cdx", IsBinary = true)]
    CDX,

    [ContentTypeMetadate(MimeType = "application/vnd.chemdraw+xml", IsText = true)]
    CDXML,

    [ContentTypeMetadate(MimeType = "application/vnd.cinderella", IsBinary = true)]
    CDY,

    [ContentTypeMetadate(MimeType = "application/pkix-cert", IsBinary = true)]
    CER,

    [ContentTypeMetadate(MimeType = "application/x-cfs-compressed", IsBinary = true)]
    CFS,

    [ContentTypeMetadate(MimeType = "image/cgm", IsBinary = true)]
    CGM,

    [ContentTypeMetadate(MimeType = "application/x-chat", IsBinary = true)]
    CHAT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-htmlhelp", IsBinary = true)]
    CHM,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kchart", IsBinary = true)]
    CHRT,

    [ContentTypeMetadate(MimeType = "chemical/x-cif", IsBinary = true)]
    CIF,

    [ContentTypeMetadate(MimeType = "application/vnd.anser-web-certificate-issue-initiation", IsBinary = true)]
    CII,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-artgalry", IsBinary = true)]
    CIL,

    [ContentTypeMetadate(MimeType = "application/vnd.claymore", IsBinary = true)]
    CLA,

    [ContentTypeMetadate(MimeType = "application/java-vm", IsBinary = true)]
    CLASS,

    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.keyboard", IsBinary = true)]
    CLKK,

    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.palette", IsBinary = true)]
    CLKP,

    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.template", IsBinary = true)]
    CLKT,

    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker.wordbank", IsBinary = true)]
    CLKW,

    [ContentTypeMetadate(MimeType = "application/vnd.crick.clicker", IsBinary = true)]
    CLKX,

    [ContentTypeMetadate(MimeType = "application/x-msclip", IsBinary = true)]
    CLP,

    [ContentTypeMetadate(MimeType = "application/vnd.cosmocaller", IsBinary = true)]
    CMC,

    [ContentTypeMetadate(MimeType = "chemical/x-cmdf", IsBinary = true)]
    CMDF,

    [ContentTypeMetadate(MimeType = "chemical/x-cml", IsBinary = true)]
    CML,

    [ContentTypeMetadate(MimeType = "application/vnd.yellowriver-custom-menu", IsBinary = true)]
    CMP,

    [ContentTypeMetadate(MimeType = "image/x-cmx", IsBinary = true)]
    CMX,

    [ContentTypeMetadate(MimeType = "application/vnd.rim.cod", IsBinary = true)]
    COD,

    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    COM,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    CONF,

    [ContentTypeMetadate(MimeType = "application/x-cpio", IsBinary = true)]
    CPIO,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CPP,

    [ContentTypeMetadate(MimeType = "application/mac-compactpro", IsBinary = true)]
    CPT,

    [ContentTypeMetadate(MimeType = "application/x-mscardfile", IsBinary = true)]
    CRD,

    [ContentTypeMetadate(MimeType = "application/pkix-crl", IsBinary = true)]
    CRL,

    [ContentTypeMetadate(MimeType = "application/x-x509-ca-cert", IsBinary = true)]
    CRT,

    [ContentTypeMetadate(MimeType = "application/vnd.rig.cryptonote", IsBinary = true)]
    CRYPTONOTE,

    [ContentTypeMetadate(MimeType = "application/x-csh", IsBinary = true)]
    CSH,

    [ContentTypeMetadate(MimeType = "chemical/x-csml", IsBinary = true)]
    CSML,

    [ContentTypeMetadate(MimeType = "application/vnd.commonspace", IsBinary = true)]
    CSP,

    [ContentTypeMetadate(MimeType = "text/css", IsText = true, FileExtension = "css")]
    CSS,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CST,

    [ContentTypeMetadate(MimeType = "text/csv", IsText = true, FileExtension = "csv")]
    CSV,

    [ContentTypeMetadate(MimeType = "application/cu-seeme", IsBinary = true)]
    CU,

    [ContentTypeMetadate(MimeType = "text/vnd.curl", IsText = true)]
    CURL,

    [ContentTypeMetadate(MimeType = "application/prs.cww", IsBinary = true)]
    CWW,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    CXT,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    CXX,

    [ContentTypeMetadate(MimeType = "model/vnd.collada+xml", IsText = true)]
    DAE,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.daf", IsBinary = true)]
    DAF,

    [ContentTypeMetadate(MimeType = "application/vnd.dart", IsBinary = true)]
    DART,

    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.seed", IsBinary = true)]
    DATALESS,

    [ContentTypeMetadate(MimeType = "application/davmount+xml", IsText = true)]
    DAVMOUNT,

    [ContentTypeMetadate(MimeType = "application/docbook+xml", IsText = true)]
    DBK,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DCR,

    [ContentTypeMetadate(MimeType = "text/vnd.curl.dcurl", IsText = true)]
    DCURL,

    [ContentTypeMetadate(MimeType = "application/vnd.oma.dd2+xml", IsText = true)]
    DD2,

    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.ddd", IsBinary = true)]
    DDD,

    [ContentTypeMetadate(MimeType = "application/x-debian-package", IsBinary = true)]
    DEB,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    DEF,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DEPLOY,

    [ContentTypeMetadate(MimeType = "application/x-x509-ca-cert", IsBinary = true)]
    DER,

    [ContentTypeMetadate(MimeType = "application/vnd.dreamfactory", IsBinary = true)]
    DFAC,

    [ContentTypeMetadate(MimeType = "application/x-dgc-compressed", IsBinary = true)]
    DGC,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    DIC,

    [ContentTypeMetadate(MimeType = "video/x-dv", IsBinary = true)]
    DIF,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DIR,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.dis", IsBinary = true)]
    DIS,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DIST,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DISTZ,

    [ContentTypeMetadate(MimeType = "image/vnd.djvu", IsBinary = true)]
    DJV,

    [ContentTypeMetadate(MimeType = "image/vnd.djvu", IsBinary = true)]
    DJVU,

    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    DLL,

    [ContentTypeMetadate(MimeType = "application/x-apple-diskimage", IsBinary = true)]
    DMG,

    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    DMP,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DMS,

    [ContentTypeMetadate(MimeType = "application/vnd.dna", IsBinary = true)]
    DNA,

    [ContentTypeMetadate(MimeType = "application/msword", IsBinary = true)]
    DOC,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-word.document.macroenabled.12", IsBinary = true)]
    DOCM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        IsBinary = true)]
    DOCX,

    [ContentTypeMetadate(MimeType = "application/msword", IsBinary = true)]
    DOT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-word.template.macroenabled.12", IsBinary = true)]
    DOTM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
        IsBinary = true)]
    DOTX,

    [ContentTypeMetadate(MimeType = "application/vnd.osgi.dp", IsBinary = true)]
    DP,

    [ContentTypeMetadate(MimeType = "application/vnd.dpgraph", IsBinary = true)]
    DPG,

    [ContentTypeMetadate(MimeType = "audio/vnd.dra", IsBinary = true)]
    DRA,

    [ContentTypeMetadate(MimeType = "text/prs.lines.tag", IsText = true)]
    DSC,

    [ContentTypeMetadate(MimeType = "application/dssc+der", IsBinary = true)]
    DSSC,

    [ContentTypeMetadate(MimeType = "application/x-dtbook+xml", IsText = true)]
    DTB,

    [ContentTypeMetadate(MimeType = "application/xml-dtd", IsBinary = true)]
    DTD,

    [ContentTypeMetadate(MimeType = "audio/vnd.dts", IsBinary = true)]
    DTS,

    [ContentTypeMetadate(MimeType = "audio/vnd.dts.hd", IsBinary = true)]
    DTSHD,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DUMP,

    [ContentTypeMetadate(MimeType = "video/x-dv", IsBinary = true)]
    DV,

    [ContentTypeMetadate(MimeType = "video/vnd.dvb.file", IsBinary = true)]
    DVB,

    [ContentTypeMetadate(MimeType = "application/x-dvi", IsBinary = true)]
    DVI,

    [ContentTypeMetadate(MimeType = "model/vnd.dwf", IsBinary = true)]
    DWF,

    [ContentTypeMetadate(MimeType = "image/vnd.dwg", IsBinary = true)]
    DWG,

    [ContentTypeMetadate(MimeType = "image/vnd.dxf", IsBinary = true)]
    DXF,

    [ContentTypeMetadate(MimeType = "application/vnd.spotfire.dxp", IsBinary = true)]
    DXP,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    DXR,

    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp4800", IsBinary = true)]
    ECELP4800,

    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp7470", IsBinary = true)]
    ECELP7470,

    [ContentTypeMetadate(MimeType = "audio/vnd.nuera.ecelp9600", IsBinary = true)]
    ECELP9600,

    [ContentTypeMetadate(MimeType = "application/ecmascript", IsBinary = true)]
    ECMA,

    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.edm", IsBinary = true)]
    EDM,

    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.edx", IsBinary = true)]
    EDX,

    [ContentTypeMetadate(MimeType = "application/vnd.picsel", IsBinary = true)]
    EFIF,

    [ContentTypeMetadate(MimeType = "application/vnd.pg.osasli", IsBinary = true)]
    EI6,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    ELC,

    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    EMF,

    [ContentTypeMetadate(MimeType = "message/rfc822", IsBinary = true)]
    EML,

    [ContentTypeMetadate(MimeType = "application/emma+xml", IsText = true)]
    EMMA,

    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    EMZ,

    [ContentTypeMetadate(MimeType = "audio/vnd.digital-winds", IsBinary = true)]
    EOL,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-fontobject", IsBinary = true)]
    EOT,

    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    EPS,

    [ContentTypeMetadate(MimeType = "application/epub+zip", IsBinary = true)]
    EPUB,

    [ContentTypeMetadate(MimeType = "application/vnd.eszigno3+xml", IsText = true)]
    ES3,

    [ContentTypeMetadate(MimeType = "application/vnd.osgi.subsystem", IsBinary = true)]
    ESA,

    [ContentTypeMetadate(MimeType = "application/vnd.epson.esf", IsBinary = true)]
    ESF,

    [ContentTypeMetadate(MimeType = "application/vnd.eszigno3+xml", IsText = true)]
    ET3,

    [ContentTypeMetadate(MimeType = "text/x-setext", IsText = true)]
    ETX,

    [ContentTypeMetadate(MimeType = "application/x-eva", IsBinary = true)]
    EVA,

    [ContentTypeMetadate(MimeType = "application/x-envoy", IsBinary = true)]
    EVY,

    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    EXE,

    [ContentTypeMetadate(MimeType = "application/exi", IsBinary = true)]
    EXI,

    [ContentTypeMetadate(MimeType = "application/vnd.novadigm.ext", IsBinary = true)]
    EXT,

    [ContentTypeMetadate(MimeType = "MIME type (lowercased)", IsBinary = true)]
    EXTENSIONS,

    [ContentTypeMetadate(MimeType = "application/andrew-inset", IsBinary = true)]
    EZ,

    [ContentTypeMetadate(MimeType = "application/vnd.ezpix-album", IsBinary = true)]
    EZ2,

    [ContentTypeMetadate(MimeType = "application/vnd.ezpix-package", IsBinary = true)]
    EZ3,

    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F,

    [ContentTypeMetadate(MimeType = "video/x-f4v", IsBinary = true)]
    F4V,

    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F77,

    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    F90,

    [ContentTypeMetadate(MimeType = "image/vnd.fastbidsheet", IsBinary = true)]
    FBS,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.formscentral.fcdt", IsBinary = true)]
    FCDT,

    [ContentTypeMetadate(MimeType = "application/vnd.isac.fcs", IsBinary = true)]
    FCS,

    [ContentTypeMetadate(MimeType = "application/vnd.fdf", IsBinary = true)]
    FDF,

    [ContentTypeMetadate(MimeType = "application/vnd.denovo.fcselayout-link", IsBinary = true)]
    FE_LAUNCH,

    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasysgp", IsBinary = true)]
    FG5,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    FGD,

    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH,

    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH4,

    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH5,

    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FH7,

    [ContentTypeMetadate(MimeType = "image/x-freehand", IsBinary = true)]
    FHC,

    [ContentTypeMetadate(MimeType = "application/x-xfig", IsBinary = true)]
    FIG,

    [ContentTypeMetadate(MimeType = "audio/x-flac", IsBinary = true)]
    FLAC,

    [ContentTypeMetadate(MimeType = "video/x-fli", IsBinary = true)]
    FLI,

    [ContentTypeMetadate(MimeType = "application/vnd.micrografx.flo", IsBinary = true)]
    FLO,

    [ContentTypeMetadate(MimeType = "video/x-flv", IsBinary = true)]
    FLV,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kivio", IsBinary = true)]
    FLW,

    [ContentTypeMetadate(MimeType = "text/vnd.fmi.flexstor", IsText = true)]
    FLX,

    [ContentTypeMetadate(MimeType = "text/vnd.fly", IsText = true)]
    FLY,

    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    FM,

    [ContentTypeMetadate(MimeType = "application/vnd.frogans.fnc", IsBinary = true)]
    FNC,

    [ContentTypeMetadate(MimeType = "text/x-fortran", IsText = true)]
    FOR,

    [ContentTypeMetadate(MimeType = "image/vnd.fpx", IsBinary = true)]
    FPX,

    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    FRAME,

    [ContentTypeMetadate(MimeType = "application/vnd.fsc.weblaunch", IsBinary = true)]
    FSC,

    [ContentTypeMetadate(MimeType = "image/vnd.fst", IsBinary = true)]
    FST,

    [ContentTypeMetadate(MimeType = "application/x-www-form-urlencoded", IsBinary = false)]
    FORM,

    [ContentTypeMetadate(MimeType = "multipart/form-data", IsBinary = false)]
    MFORM,

    [ContentTypeMetadate(MimeType = "application/vnd.fluxtime.clip", IsBinary = true)]
    FTC,

    [ContentTypeMetadate(MimeType = "application/vnd.anser-web-funds-transfer-initiation", IsBinary = true)]
    FTI,

    [ContentTypeMetadate(MimeType = "video/vnd.fvt", IsBinary = true)]
    FVT,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.fxp", IsBinary = true)]
    FXP,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.fxp", IsBinary = true)]
    FXPL,

    [ContentTypeMetadate(MimeType = "application/vnd.fuzzysheet", IsBinary = true)]
    FZS,

    [ContentTypeMetadate(MimeType = "application/vnd.geoplan", IsBinary = true)]
    G2W,

    [ContentTypeMetadate(MimeType = "image/g3fax", IsBinary = true)]
    G3,

    [ContentTypeMetadate(MimeType = "application/vnd.geospace", IsBinary = true)]
    G3W,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-account", IsBinary = true)]
    GAC,

    [ContentTypeMetadate(MimeType = "application/x-tads", IsBinary = true)]
    GAM,

    [ContentTypeMetadate(MimeType = "application/rpki-ghostbusters", IsBinary = true)]
    GBR,

    [ContentTypeMetadate(MimeType = "application/x-gca-compressed", IsBinary = true)]
    GCA,

    [ContentTypeMetadate(MimeType = "model/vnd.gdl", IsBinary = true)]
    GDL,

    [ContentTypeMetadate(MimeType = "application/vnd.dynageo", IsBinary = true)]
    GEO,

    [ContentTypeMetadate(MimeType = "application/vnd.geometry-explorer", IsBinary = true)]
    GEX,

    [ContentTypeMetadate(MimeType = "application/vnd.geogebra.file", IsBinary = true)]
    GGB,

    [ContentTypeMetadate(MimeType = "application/vnd.geogebra.tool", IsBinary = true)]
    GGT,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-help", IsBinary = true)]
    GHF,

    [ContentTypeMetadate(MimeType = "image/gif", IsBinary = true)]
    GIF,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-identity-message", IsBinary = true)]
    GIM,

    [ContentTypeMetadate(MimeType = "application/gml+xml", IsText = true)]
    GML,

    [ContentTypeMetadate(MimeType = "application/vnd.gmx", IsBinary = true)]
    GMX,

    [ContentTypeMetadate(MimeType = "application/x-gnumeric", IsBinary = true)]
    GNUMERIC,

    [ContentTypeMetadate(MimeType = "application/vnd.flographit", IsBinary = true)]
    GPH,

    [ContentTypeMetadate(MimeType = "application/gpx+xml", IsText = true)]
    GPX,

    [ContentTypeMetadate(MimeType = "application/vnd.grafeq", IsBinary = true)]
    GQF,

    [ContentTypeMetadate(MimeType = "application/vnd.grafeq", IsBinary = true)]
    GQS,

    [ContentTypeMetadate(MimeType = "application/srgs", IsBinary = true)]
    GRAM,

    [ContentTypeMetadate(MimeType = "application/x-gramps-xml", IsText = true)]
    GRAMPS,

    [ContentTypeMetadate(MimeType = "application/vnd.geometry-explorer", IsBinary = true)]
    GRE,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-injector", IsBinary = true)]
    GRV,

    [ContentTypeMetadate(MimeType = "application/srgs+xml", IsText = true)]
    GRXML,

    [ContentTypeMetadate(MimeType = "application/x-font-ghostscript", IsBinary = true)]
    GSF,

    [ContentTypeMetadate(MimeType = "application/x-gtar", IsBinary = true)]
    GTAR,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-tool-message", IsBinary = true)]
    GTM,

    [ContentTypeMetadate(MimeType = "model/vnd.gtw", IsBinary = true)]
    GTW,

    [ContentTypeMetadate(MimeType = "text/vnd.graphviz", IsText = true)]
    GV,

    [ContentTypeMetadate(MimeType = "application/gxf", IsBinary = true)]
    GXF,

    [ContentTypeMetadate(MimeType = "application/vnd.geonext", IsBinary = true)]
    GXT,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    H,

    [ContentTypeMetadate(MimeType = "video/h261", IsBinary = true)]
    H261,

    [ContentTypeMetadate(MimeType = "video/h263", IsBinary = true)]
    H263,

    [ContentTypeMetadate(MimeType = "video/h264", IsBinary = true)]
    H264,

    [ContentTypeMetadate(MimeType = "application/vnd.hal+xml", IsText = true)]
    HAL,

    [ContentTypeMetadate(MimeType = "application/vnd.hbci", IsBinary = true)]
    HBCI,

    [ContentTypeMetadate(MimeType = "application/x-hdf", IsBinary = true)]
    HDF,

    [ContentTypeMetadate(MimeType = "text/x-c", IsText = true)]
    HH,

    [ContentTypeMetadate(MimeType = "application/winhlp", IsBinary = true)]
    HLP,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-hpgl", IsBinary = true)]
    HPGL,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-hpid", IsBinary = true)]
    HPID,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-hps", IsBinary = true)]
    HPS,

    [ContentTypeMetadate(MimeType = "application/mac-binhex40", IsBinary = true)]
    HQX,

    [ContentTypeMetadate(MimeType = "application/vnd.kenameaapp", IsBinary = true)]
    HTKE,

    [ContentTypeMetadate(MimeType = "text/html", IsText = true, FileExtension = "htm")]
    HTM,

    [ContentTypeMetadate(MimeType = "text/html", IsText = true, FileExtension = "html")]
    HTML,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-dic", IsBinary = true)]
    HVD,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-voice", IsBinary = true)]
    HVP,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.hv-script", IsBinary = true)]
    HVS,

    [ContentTypeMetadate(MimeType = "application/vnd.intergeo", IsBinary = true)]
    I2G,

    [ContentTypeMetadate(MimeType = "x-conference/x-cooltalk", IsBinary = true)]
    IC,

    [ContentTypeMetadate(MimeType = "application/vnd.iccprofile", IsBinary = true)]
    ICC,

    [ContentTypeMetadate(MimeType = "x-conference/x-cooltalk", IsBinary = true)]
    ICE,

    [ContentTypeMetadate(MimeType = "application/vnd.iccprofile", IsBinary = true)]
    ICM,

    [ContentTypeMetadate(MimeType = "image/x-icon", IsBinary = true)]
    ICO,

    [ContentTypeMetadate(MimeType = "text/calendar", IsText = true)]
    ICS,

    [ContentTypeMetadate(MimeType = "image/ief", IsBinary = true)]
    IEF,

    [ContentTypeMetadate(MimeType = "text/calendar", IsText = true)]
    IFB,

    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.formdata", IsBinary = true)]
    IFM,

    [ContentTypeMetadate(MimeType = "model/iges", IsBinary = true)]
    IGES,

    [ContentTypeMetadate(MimeType = "application/vnd.igloader", IsBinary = true)]
    IGL,

    [ContentTypeMetadate(MimeType = "application/vnd.insors.igm", IsBinary = true)]
    IGM,

    [ContentTypeMetadate(MimeType = "model/iges", IsBinary = true)]
    IGS,

    [ContentTypeMetadate(MimeType = "application/vnd.micrografx.igx", IsBinary = true)]
    IGX,

    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.interchange", IsBinary = true)]
    IIF,

    [ContentTypeMetadate(MimeType = "application/vnd.accpac.simply.imp", IsBinary = true)]
    IMP,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-ims", IsBinary = true)]
    IMS,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    IN,

    [ContentTypeMetadate(MimeType = "application/inkml+xml", IsText = true)]
    INK,

    [ContentTypeMetadate(MimeType = "application/inkml+xml", IsText = true)]
    INKML,

    [ContentTypeMetadate(MimeType = "application/x-install-instructions", IsBinary = true)]
    INSTALL,

    [ContentTypeMetadate(MimeType = "application/vnd.astraea-software.iota", IsBinary = true)]
    IOTA,

    [ContentTypeMetadate(MimeType = "application/ipfix", IsBinary = true)]
    IPFIX,

    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.package", IsBinary = true)]
    IPK,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.rights-management", IsBinary = true)]
    IRM,

    [ContentTypeMetadate(MimeType = "application/vnd.irepository.package+xml", IsText = true)]
    IRP,

    [ContentTypeMetadate(MimeType = "application/x-iso9660-image", IsBinary = true)]
    ISO,

    [ContentTypeMetadate(MimeType = "application/vnd.shana.informed.formtemplate", IsBinary = true)]
    ITP,

    [ContentTypeMetadate(MimeType = "application/vnd.immervision-ivp", IsBinary = true)]
    IVP,

    [ContentTypeMetadate(MimeType = "application/vnd.immervision-ivu", IsBinary = true)]
    IVU,

    [ContentTypeMetadate(MimeType = "text/vnd.sun.j2me.app-descriptor", IsText = true)]
    JAD,

    [ContentTypeMetadate(MimeType = "application/vnd.jam", IsBinary = true)]
    JAM,

    [ContentTypeMetadate(MimeType = "application/java-archive", IsBinary = true)]
    JAR,

    [ContentTypeMetadate(MimeType = "text/x-java-source", IsText = true)]
    JAVA,

    [ContentTypeMetadate(MimeType = "application/vnd.jisp", IsBinary = true)]
    JISP,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-jlyt", IsBinary = true)]
    JLT,

    [ContentTypeMetadate(MimeType = "application/x-java-jnlp-file", IsBinary = true)]
    JNLP,

    [ContentTypeMetadate(MimeType = "application/vnd.joost.joda-archive", IsBinary = true)]
    JODA,

    [ContentTypeMetadate(MimeType = "image/jp2", IsBinary = true)]
    JP2,

    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true)]
    JPE,

    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true)]
    JPEG,

    [ContentTypeMetadate(MimeType = "image/jpeg", IsBinary = true, FileExtension = "jpg")]
    JPG,

    [ContentTypeMetadate(MimeType = "video/jpm", IsBinary = true)]
    JPGM,

    [ContentTypeMetadate(MimeType = "video/jpeg", IsBinary = true)]
    JPGV,

    [ContentTypeMetadate(MimeType = "video/jpm", IsBinary = true)]
    JPM,

    [ContentTypeMetadate(MimeType = "application/javascript", IsText = true, FileExtension = "js")]
    JS,

    [ContentTypeMetadate(MimeType = "application/json", IsText = true, FileExtension = "json")]
    JSON,

    [ContentTypeMetadate(MimeType = "application/problem+json", IsText = true)]
    JSONPROBLEM,

    [ContentTypeMetadate(MimeType = "application/jsonml+json", IsText = true)]
    JSONML,

    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    KAR,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.karbon", IsBinary = true)]
    KARBON,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kformula", IsBinary = true)]
    KFO,

    [ContentTypeMetadate(MimeType = "application/vnd.kidspiration", IsBinary = true)]
    KIA,

    [ContentTypeMetadate(MimeType = "application/vnd.google-earth.kml+xml", IsText = true)]
    KML,

    [ContentTypeMetadate(MimeType = "application/vnd.google-earth.kmz", IsBinary = true)]
    KMZ,

    [ContentTypeMetadate(MimeType = "application/vnd.kinar", IsBinary = true)]
    KNE,

    [ContentTypeMetadate(MimeType = "application/vnd.kinar", IsBinary = true)]
    KNP,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kontour", IsBinary = true)]
    KON,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kpresenter", IsBinary = true)]
    KPR,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kpresenter", IsBinary = true)]
    KPT,

    [ContentTypeMetadate(MimeType = "application/vnd.ds-keypoint", IsBinary = true)]
    KPXX,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kspread", IsBinary = true)]
    KSP,

    [ContentTypeMetadate(MimeType = "application/vnd.kahootz", IsBinary = true)]
    KTR,

    [ContentTypeMetadate(MimeType = "image/ktx", IsBinary = true)]
    KTX,

    [ContentTypeMetadate(MimeType = "application/vnd.kahootz", IsBinary = true)]
    KTZ,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kword", IsBinary = true)]
    KWD,

    [ContentTypeMetadate(MimeType = "application/vnd.kde.kword", IsBinary = true)]
    KWT,

    [ContentTypeMetadate(MimeType = "application/vnd.las.las+xml", IsText = true)]
    LASXML,

    [ContentTypeMetadate(MimeType = "application/x-latex", IsBinary = true)]
    LATEX,

    [ContentTypeMetadate(MimeType = "application/vnd.llamagraphics.life-balance.desktop", IsBinary = true)]
    LBD,

    [ContentTypeMetadate(MimeType = "application/vnd.llamagraphics.life-balance.exchange+xml", IsText = true)]
    LBE,

    [ContentTypeMetadate(MimeType = "application/vnd.hhe.lesson-player", IsBinary = true)]
    LES,

    [ContentTypeMetadate(MimeType = "application/x-lzh-compressed", IsBinary = true)]
    LHA,

    [ContentTypeMetadate(MimeType = "application/vnd.route66.link66+xml", IsText = true)]
    LINK66,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    LIST,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    LIST3820,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.modcap", IsBinary = true)]
    LISTAFP,

    [ContentTypeMetadate(MimeType = "application/x-ms-shortcut", IsBinary = true)]
    LNK,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    LOG,

    [ContentTypeMetadate(MimeType = "application/lost+xml", IsText = true)]
    LOSTXML,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    LRF,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-lrm", IsBinary = true)]
    LRM,

    [ContentTypeMetadate(MimeType = "application/vnd.frogans.ltf", IsBinary = true)]
    LTF,

    [ContentTypeMetadate(MimeType = "audio/vnd.lucent.voice", IsBinary = true)]
    LVP,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-wordpro", IsBinary = true)]
    LWP,

    [ContentTypeMetadate(MimeType = "application/x-lzh-compressed", IsBinary = true)]
    LZH,

    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    M13,

    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    M14,

    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    M1V,

    [ContentTypeMetadate(MimeType = "application/mp21", IsBinary = true)]
    M21,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    M2A,

    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    M2V,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    M3A,

    [ContentTypeMetadate(MimeType = "audio/x-mpegurl", IsBinary = true)]
    M3U,

    [ContentTypeMetadate(MimeType = "application/vnd.apple.mpegurl", IsBinary = true)]
    M3U8,

    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4A,

    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4B,

    [ContentTypeMetadate(MimeType = "audio/mp4a-latm", IsBinary = true)]
    M4P,

    [ContentTypeMetadate(MimeType = "video/vnd.mpegurl", IsBinary = true)]
    M4U,

    [ContentTypeMetadate(MimeType = "video/x-m4v", IsBinary = true)]
    M4V,

    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    MA,

    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    MAC,

    [ContentTypeMetadate(MimeType = "application/mads+xml", IsText = true)]
    MADS,

    [ContentTypeMetadate(MimeType = "application/vnd.ecowin.chart", IsBinary = true)]
    MAG,

    [ContentTypeMetadate(MimeType = "application/vnd.framemaker", IsBinary = true)]
    MAKER,

    [ContentTypeMetadate(MimeType = "application/x-troff-man", IsBinary = true)]
    MAN,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    MAR,

    [ContentTypeMetadate(MimeType = "application/mathml+xml", IsText = true)]
    MATHML,

    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    MB,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.mbk", IsBinary = true)]
    MBK,

    [ContentTypeMetadate(MimeType = "application/mbox", IsBinary = true)]
    MBOX,

    [ContentTypeMetadate(MimeType = "application/vnd.medcalcdata", IsBinary = true)]
    MC1,

    [ContentTypeMetadate(MimeType = "application/vnd.mcd", IsBinary = true)]
    MCD,

    [ContentTypeMetadate(MimeType = "text/vnd.curl.mcurl", IsText = true)]
    MCURL,

    [ContentTypeMetadate(MimeType = "text/markdown", IsText = true)]
    MD,

    [ContentTypeMetadate(MimeType = "application/x-msaccess", IsBinary = true)]
    MDB,

    [ContentTypeMetadate(MimeType = "image/vnd.ms-modi", IsBinary = true)]
    MDI,

    [ContentTypeMetadate(MimeType = "application/x-troff-me", IsBinary = true)]
    ME,

    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    MESH,

    [ContentTypeMetadate(MimeType = "application/metalink4+xml", IsText = true)]
    META4,

    [ContentTypeMetadate(MimeType = "application/metalink+xml", IsText = true)]
    METALINK,

    [ContentTypeMetadate(MimeType = "application/mets+xml", IsText = true)]
    METS,

    [ContentTypeMetadate(MimeType = "application/vnd.mfmp", IsBinary = true)]
    MFM,

    [ContentTypeMetadate(MimeType = "application/rpki-manifest", IsBinary = true)]
    MFT,

    [ContentTypeMetadate(MimeType = "application/vnd.osgeo.mapguide.package", IsBinary = true)]
    MGP,

    [ContentTypeMetadate(MimeType = "application/vnd.proteus.magazine", IsBinary = true)]
    MGZ,

    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    MID,

    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    MIDI,

    [ContentTypeMetadate(MimeType = "application/x-mie", IsBinary = true)]
    MIE,

    [ContentTypeMetadate(MimeType = "application/vnd.mif", IsBinary = true)]
    MIF,

    [ContentTypeMetadate(MimeType = "message/rfc822", IsBinary = true)]
    MIME,

    [ContentTypeMetadate(MimeType = "video/mj2", IsBinary = true)]
    MJ2,

    [ContentTypeMetadate(MimeType = "video/mj2", IsBinary = true)]
    MJP2,

    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MK3D,

    [ContentTypeMetadate(MimeType = "audio/x-matroska", IsBinary = true)]
    MKA,

    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MKS,

    [ContentTypeMetadate(MimeType = "video/x-matroska", IsBinary = true)]
    MKV,

    [ContentTypeMetadate(MimeType = "application/vnd.dolby.mlp", IsBinary = true)]
    MLP,

    [ContentTypeMetadate(MimeType = "application/vnd.chipnuts.karaoke-mmd", IsBinary = true)]
    MMD,

    [ContentTypeMetadate(MimeType = "application/vnd.smaf", IsBinary = true)]
    MMF,

    [ContentTypeMetadate(MimeType = "image/vnd.fujixerox.edmics-mmr", IsBinary = true)]
    MMR,

    [ContentTypeMetadate(MimeType = "video/x-mng", IsBinary = true)]
    MNG,

    [ContentTypeMetadate(MimeType = "application/x-msmoney", IsBinary = true)]
    MNY,

    [ContentTypeMetadate(MimeType = "application/x-mobipocket-ebook", IsBinary = true)]
    MOBI,

    [ContentTypeMetadate(MimeType = "application/mods+xml", IsText = true)]
    MODS,

    [ContentTypeMetadate(MimeType = "video/quicktime", IsBinary = true)]
    MOV,

    [ContentTypeMetadate(MimeType = "video/x-sgi-movie", IsBinary = true)]
    MOVIE,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP2,

    [ContentTypeMetadate(MimeType = "application/mp21", IsBinary = true)]
    MP21,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP2A,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MP3,

    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MP4,

    [ContentTypeMetadate(MimeType = "audio/mp4", IsBinary = true)]
    MP4A,

    [ContentTypeMetadate(MimeType = "application/mp4", IsBinary = true)]
    MP4S,

    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MP4V,

    [ContentTypeMetadate(MimeType = "application/vnd.mophun.certificate", IsBinary = true)]
    MPC,

    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPE,

    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPEG,

    [ContentTypeMetadate(MimeType = "video/mpeg", IsBinary = true)]
    MPG,

    [ContentTypeMetadate(MimeType = "video/mp4", IsBinary = true)]
    MPG4,

    [ContentTypeMetadate(MimeType = "audio/mpeg", IsBinary = true)]
    MPGA,

    [ContentTypeMetadate(MimeType = "application/vnd.apple.installer+xml", IsText = true)]
    MPKG,

    [ContentTypeMetadate(MimeType = "application/vnd.blueice.multipass", IsBinary = true)]
    MPM,

    [ContentTypeMetadate(MimeType = "application/vnd.mophun.application", IsBinary = true)]
    MPN,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-project", IsBinary = true)]
    MPP,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-project", IsBinary = true)]
    MPT,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.minipay", IsBinary = true)]
    MPY,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.mqy", IsBinary = true)]
    MQY,

    [ContentTypeMetadate(MimeType = "application/marc", IsBinary = true)]
    MRC,

    [ContentTypeMetadate(MimeType = "application/marcxml+xml", IsText = true)]
    MRCX,

    [ContentTypeMetadate(MimeType = "application/x-troff-ms", IsBinary = true)]
    MS,

    [ContentTypeMetadate(MimeType = "application/mediaservercontrol+xml", IsText = true)]
    MSCML,

    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.mseed", IsBinary = true)]
    MSEED,

    [ContentTypeMetadate(MimeType = "application/vnd.mseq", IsBinary = true)]
    MSEQ,

    [ContentTypeMetadate(MimeType = "application/vnd.epson.msf", IsBinary = true)]
    MSF,

    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    MSH,

    [ContentTypeMetadate(MimeType = "application/x-msdownload", IsBinary = true)]
    MSI,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.msl", IsBinary = true)]
    MSL,

    [ContentTypeMetadate(MimeType = "application/vnd.muvee.style", IsBinary = true)]
    MSTY,

    [ContentTypeMetadate(MimeType = "model/vnd.mts", IsBinary = true)]
    MTS,

    [ContentTypeMetadate(MimeType = "application/vnd.musician", IsBinary = true)]
    MUS,

    [ContentTypeMetadate(MimeType = "application/vnd.recordare.musicxml+xml", IsText = true)]
    MUSICXML,

    [ContentTypeMetadate(MimeType = "application/x-msmediaview", IsBinary = true)]
    MVB,

    [ContentTypeMetadate(MimeType = "application/vnd.mfer", IsBinary = true)]
    MWF,

    [ContentTypeMetadate(MimeType = "application/mxf", IsBinary = true)]
    MXF,

    [ContentTypeMetadate(MimeType = "application/vnd.recordare.musicxml", IsText = true)]
    MXL,

    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    MXML,

    [ContentTypeMetadate(MimeType = "application/vnd.triscape.mxs", IsBinary = true)]
    MXS,

    [ContentTypeMetadate(MimeType = "video/vnd.mpegurl", IsBinary = true)]
    MXU,

    [ContentTypeMetadate(MimeType = "text/n3", IsText = true)]
    N3,

    [ContentTypeMetadate(MimeType = "application/mathematica", IsBinary = true)]
    NB,

    [ContentTypeMetadate(MimeType = "application/vnd.wolfram.player", IsBinary = true)]
    NBP,

    [ContentTypeMetadate(MimeType = "application/x-netcdf", IsBinary = true)]
    NC,

    [ContentTypeMetadate(MimeType = "application/x-dtbncx+xml", IsText = true)]
    NCX,

    [ContentTypeMetadate(MimeType = "text/x-nfo", IsText = true)]
    NFO,

    [ContentTypeMetadate(MimeType = "application/vnd.nokia.n-gage.data", IsBinary = true)]
    NGDAT,

    [ContentTypeMetadate(MimeType = "application/vnd.nitf", IsBinary = true)]
    NITF,

    [ContentTypeMetadate(MimeType = "application/vnd.neurolanguage.nlu", IsBinary = true)]
    NLU,

    [ContentTypeMetadate(MimeType = "application/vnd.enliven", IsBinary = true)]
    NML,

    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-directory", IsBinary = true)]
    NND,

    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-sealer", IsBinary = true)]
    NNS,

    [ContentTypeMetadate(MimeType = "application/vnd.noblenet-web", IsBinary = true)]
    NNW,

    [ContentTypeMetadate(MimeType = "image/vnd.net-fpx", IsBinary = true)]
    NPX,

    [ContentTypeMetadate(MimeType = "application/x-conference", IsBinary = true)]
    NSC,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-notes", IsBinary = true)]
    NSF,

    [ContentTypeMetadate(MimeType = "application/vnd.nitf", IsBinary = true)]
    NTF,

    [ContentTypeMetadate(MimeType = "application/x-nzb", IsBinary = true)]
    NZB,

    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys2", IsBinary = true)]
    OA2,

    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys3", IsBinary = true)]
    OA3,

    [ContentTypeMetadate(MimeType = "application/vnd.fujitsu.oasys", IsBinary = true)]
    OAS,

    [ContentTypeMetadate(MimeType = "application/x-msbinder", IsBinary = true)]
    OBD,

    [ContentTypeMetadate(MimeType = "application/x-tgif", IsBinary = true)]
    OBJ,

    [ContentTypeMetadate(MimeType = "application/oda", IsBinary = true)]
    ODA,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.database", IsBinary = true)]
    ODB,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.chart", IsBinary = true)]
    ODC,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.formula", IsBinary = true)]
    ODF,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.formula-template", IsBinary = true)]
    ODFT,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.graphics", IsBinary = true)]
    ODG,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.image", IsBinary = true)]
    ODI,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-master", IsBinary = true)]
    ODM,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.presentation", IsBinary = true)]
    ODP,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.spreadsheet", IsBinary = true)]
    ODS,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text", IsBinary = true)]
    ODT,

    [ContentTypeMetadate(MimeType = "audio/ogg", IsBinary = true)]
    OGA,

    [ContentTypeMetadate(MimeType = "video/ogg", IsBinary = true)]
    OGG,

    [ContentTypeMetadate(MimeType = "video/ogg", IsBinary = true)]
    OGV,

    [ContentTypeMetadate(MimeType = "application/ogg", IsBinary = true)]
    OGX,

    [ContentTypeMetadate(MimeType = "application/omdoc+xml", IsText = true)]
    OMDOC,

    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONEPKG,

    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETMP,

    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETOC,

    [ContentTypeMetadate(MimeType = "application/onenote", IsBinary = true)]
    ONETOC2,

    [ContentTypeMetadate(MimeType = "application/oebps-package+xml", IsText = true)]
    OPF,

    [ContentTypeMetadate(MimeType = "text/x-opml", IsText = true)]
    OPML,

    [ContentTypeMetadate(MimeType = "application/vnd.palm", IsBinary = true)]
    OPRC,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-organizer", IsBinary = true)]
    ORG,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.openscoreformat", IsBinary = true)]
    OSF,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.openscoreformat.osfpvg+xml", IsText = true)]
    OSFPVG,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.chart-template", IsBinary = true)]
    OTC,

    [ContentTypeMetadate(MimeType = "application/x-font-otf", IsBinary = true)]
    OTF,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.graphics-template", IsBinary = true)]
    OTG,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-web", IsBinary = true)]
    OTH,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.image-template", IsBinary = true)]
    OTI,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.presentation-template", IsBinary = true)]
    OTP,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.spreadsheet-template", IsBinary = true)]
    OTS,

    [ContentTypeMetadate(MimeType = "application/vnd.oasis.opendocument.text-template", IsBinary = true)]
    OTT,

    [ContentTypeMetadate(MimeType = "application/oxps", IsBinary = true)]
    OXPS,

    [ContentTypeMetadate(MimeType = "application/vnd.openofficeorg.extension", IsBinary = true)]
    OXT,

    [ContentTypeMetadate(MimeType = "text/x-pascal", IsText = true)]
    P,

    [ContentTypeMetadate(MimeType = "application/pkcs10", IsBinary = true)]
    P10,

    [ContentTypeMetadate(MimeType = "application/x-pkcs12", IsBinary = true)]
    P12,

    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certificates", IsBinary = true)]
    P7B,

    [ContentTypeMetadate(MimeType = "application/pkcs7-mime", IsBinary = true)]
    P7C,

    [ContentTypeMetadate(MimeType = "application/pkcs7-mime", IsBinary = true)]
    P7M,

    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certreqresp", IsBinary = true)]
    P7R,

    [ContentTypeMetadate(MimeType = "application/pkcs7-signature", IsBinary = true)]
    P7S,

    [ContentTypeMetadate(MimeType = "application/pkcs8", IsBinary = true)]
    P8,

    [ContentTypeMetadate(MimeType = "text/x-pascal", IsText = true)]
    PAS,

    [ContentTypeMetadate(MimeType = "application/vnd.pawaafile", IsBinary = true)]
    PAW,

    [ContentTypeMetadate(MimeType = "application/vnd.powerbuilder6", IsBinary = true)]
    PBD,

    [ContentTypeMetadate(MimeType = "image/x-portable-bitmap", IsBinary = true)]
    PBM,

    [ContentTypeMetadate(MimeType = "application/vnd.tcpdump.pcap", IsBinary = true)]
    PCAP,

    [ContentTypeMetadate(MimeType = "application/x-font-pcf", IsBinary = true)]
    PCF,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-pcl", IsBinary = true)]
    PCL,

    [ContentTypeMetadate(MimeType = "application/vnd.hp-pclxl", IsBinary = true)]
    PCLXL,

    [ContentTypeMetadate(MimeType = "image/x-pict", IsBinary = true)]
    PCT,

    [ContentTypeMetadate(MimeType = "application/vnd.curl.pcurl", IsBinary = true)]
    PCURL,

    [ContentTypeMetadate(MimeType = "image/x-pcx", IsBinary = true)]
    PCX,

    [ContentTypeMetadate(MimeType = "applicaton/octet-stream", IsBinary = true)]
    PDB,

    [ContentTypeMetadate(MimeType = "application/pdf", IsBinary = true, FileExtension = "pdf")]
    PDF,

    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFA,

    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFB,

    [ContentTypeMetadate(MimeType = "application/x-font-type1", IsBinary = true)]
    PFM,

    [ContentTypeMetadate(MimeType = "application/font-tdpfr", IsBinary = true)]
    PFR,

    [ContentTypeMetadate(MimeType = "application/x-pkcs12", IsBinary = true)]
    PFX,

    [ContentTypeMetadate(MimeType = "image/x-portable-graymap", IsBinary = true)]
    PGM,

    [ContentTypeMetadate(MimeType = "application/x-chess-pgn", IsBinary = true)]
    PGN,

    [ContentTypeMetadate(MimeType = "application/pgp-encrypted", IsBinary = true)]
    PGP,

    [ContentTypeMetadate(MimeType = "image/x-pict", IsBinary = true)]
    PIC,

    [ContentTypeMetadate(MimeType = "image/pict", IsBinary = true)]
    PICT,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    PKG,

    [ContentTypeMetadate(MimeType = "application/pkixcmp", IsBinary = true)]
    PKI,

    [ContentTypeMetadate(MimeType = "application/pkix-pkipath", IsBinary = true)]
    PKIPATH,

    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-large", IsBinary = true)]
    PLB,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.plc", IsBinary = true)]
    PLC,

    [ContentTypeMetadate(MimeType = "application/vnd.pocketlearn", IsBinary = true)]
    PLF,

    [ContentTypeMetadate(MimeType = "application/pls+xml", IsText = true)]
    PLS,

    [ContentTypeMetadate(MimeType = "application/vnd.ctc-posml", IsBinary = true)]
    PML,

    [ContentTypeMetadate(MimeType = "image/png", IsBinary = true, FileExtension = "png")]
    PNG,

    [ContentTypeMetadate(MimeType = "image/x-portable-anymap", IsBinary = true)]
    PNM,

    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    PNT,

    [ContentTypeMetadate(MimeType = "image/x-macpaint", IsBinary = true)]
    PNTG,

    [ContentTypeMetadate(MimeType = "application/vnd.macports.portpkg", IsBinary = true)]
    PORTPKG,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    POT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.template.macroenabled.12", IsBinary = true)]
    POTM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.template",
        IsBinary = true)]
    POTX,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.addin.macroenabled.12", IsBinary = true)]
    PPAM,

    [ContentTypeMetadate(MimeType = "application/vnd.cups-ppd", IsBinary = true)]
    PPD,

    [ContentTypeMetadate(MimeType = "image/x-portable-pixmap", IsBinary = true)]
    PPM,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    PPS,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.slideshow.macroenabled.12", IsBinary = true)]
    PPSM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
        IsBinary = true)]
    PPSX,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint", IsBinary = true)]
    PPT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.presentation.macroenabled.12", IsBinary = true)]
    PPTM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        IsBinary = true)]
    PPTX,

    [ContentTypeMetadate(MimeType = "application/vnd.palm", IsBinary = true)]
    PQA,

    [ContentTypeMetadate(MimeType = "application/x-mobipocket-ebook", IsBinary = true)]
    PRC,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-freelance", IsBinary = true)]
    PRE,

    [ContentTypeMetadate(MimeType = "application/pics-rules", IsBinary = true)]
    PRF,

    [ContentTypeMetadate(MimeType = "application/postscript", IsBinary = true)]
    PS,

    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-small", IsBinary = true)]
    PSB,

    [ContentTypeMetadate(MimeType = "image/vnd.adobe.photoshop", IsBinary = true)]
    PSD,

    [ContentTypeMetadate(MimeType = "application/x-font-linux-psf", IsBinary = true)]
    PSF,

    [ContentTypeMetadate(MimeType = "application/pskc+xml", IsText = true)]
    PSKCXML,

    [ContentTypeMetadate(MimeType = "application/vnd.pvi.ptid1", IsBinary = true)]
    PTID,

    [ContentTypeMetadate(MimeType = "application/x-mspublisher", IsBinary = true)]
    PUB,

    [ContentTypeMetadate(MimeType = "application/vnd.3gpp.pic-bw-var", IsBinary = true)]
    PVB,

    [ContentTypeMetadate(MimeType = "application/vnd.3m.post-it-notes", IsBinary = true)]
    PWN,

    [ContentTypeMetadate(MimeType = "audio/vnd.ms-playready.media.pya", IsBinary = true)]
    PYA,

    [ContentTypeMetadate(MimeType = "video/vnd.ms-playready.media.pyv", IsBinary = true)]
    PYV,

    [ContentTypeMetadate(MimeType = "application/vnd.epson.quickanime", IsBinary = true)]
    QAM,

    [ContentTypeMetadate(MimeType = "application/vnd.intu.qbo", IsBinary = true)]
    QBO,

    [ContentTypeMetadate(MimeType = "application/vnd.intu.qfx", IsBinary = true)]
    QFX,

    [ContentTypeMetadate(MimeType = "application/vnd.publishare-delta-tree", IsBinary = true)]
    QPS,

    [ContentTypeMetadate(MimeType = "video/quicktime", IsBinary = true)]
    QT,

    [ContentTypeMetadate(MimeType = "image/x-quicktime", IsBinary = true)]
    QTI,

    [ContentTypeMetadate(MimeType = "image/x-quicktime", IsBinary = true)]
    QTIF,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QWD,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QWT,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXB,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXD,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXL,

    [ContentTypeMetadate(MimeType = "application/vnd.quark.quarkxpress", IsBinary = true)]
    QXT,

    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio", IsBinary = true)]
    RA,

    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio", IsBinary = true)]
    RAM,

    [ContentTypeMetadate(MimeType = "application/x-rar-compressed", IsBinary = true)]
    RAR,

    [ContentTypeMetadate(MimeType = "image/x-cmu-raster", IsBinary = true)]
    RAS,

    [ContentTypeMetadate(MimeType = "application/vnd.ipunplugged.rcprofile", IsBinary = true)]
    RCPROFILE,

    [ContentTypeMetadate(MimeType = "application/rdf+xml", IsText = true)]
    RDF,

    [ContentTypeMetadate(MimeType = "application/vnd.data-vision.rdz", IsBinary = true)]
    RDZ,

    [ContentTypeMetadate(MimeType = "application/vnd.businessobjects", IsBinary = true)]
    REP,

    [ContentTypeMetadate(MimeType = "application/x-dtbresource+xml", IsText = true)]
    RES,

    [ContentTypeMetadate(MimeType = "image/x-rgb", IsBinary = true)]
    RGB,

    [ContentTypeMetadate(MimeType = "application/reginfo+xml", IsText = true)]
    RIF,

    [ContentTypeMetadate(MimeType = "audio/vnd.rip", IsBinary = true)]
    RIP,

    [ContentTypeMetadate(MimeType = "application/x-research-info-systems", IsBinary = true)]
    RIS,

    [ContentTypeMetadate(MimeType = "application/resource-lists+xml", IsText = true)]
    RL,

    [ContentTypeMetadate(MimeType = "image/vnd.fujixerox.edmics-rlc", IsBinary = true)]
    RLC,

    [ContentTypeMetadate(MimeType = "application/resource-lists-diff+xml", IsText = true)]
    RLD,

    [ContentTypeMetadate(MimeType = "application/vnd.rn-realmedia", IsBinary = true)]
    RM,

    [ContentTypeMetadate(MimeType = "audio/midi", IsBinary = true)]
    RMI,

    [ContentTypeMetadate(MimeType = "audio/x-pn-realaudio-plugin", IsBinary = true)]
    RMP,

    [ContentTypeMetadate(MimeType = "application/vnd.jcp.javame.midlet-rms", IsBinary = true)]
    RMS,

    [ContentTypeMetadate(MimeType = "application/vnd.rn-realmedia-vbr", IsBinary = true)]
    RMVB,

    [ContentTypeMetadate(MimeType = "application/relax-ng-compact-syntax", IsBinary = true)]
    RNC,

    [ContentTypeMetadate(MimeType = "application/rpki-roa", IsBinary = true)]
    ROA,

    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    ROFF,

    [ContentTypeMetadate(MimeType = "application/vnd.cloanto.rp9", IsBinary = true)]
    RP9,

    [ContentTypeMetadate(MimeType = "application/vnd.nokia.radio-presets", IsBinary = true)]
    RPSS,

    [ContentTypeMetadate(MimeType = "application/vnd.nokia.radio-preset", IsBinary = true)]
    RPST,

    [ContentTypeMetadate(MimeType = "application/sparql-query", IsBinary = true)]
    RQ,

    [ContentTypeMetadate(MimeType = "application/rls-services+xml", IsText = true)]
    RS,

    [ContentTypeMetadate(MimeType = "application/rsd+xml", IsText = true)]
    RSD,

    [ContentTypeMetadate(MimeType = "application/rss+xml", IsText = true)]
    RSS,

    [ContentTypeMetadate(MimeType = "application/rtf", IsBinary = true)]
    RTF,

    [ContentTypeMetadate(MimeType = "text/richtext", IsText = true)]
    RTX,

    [ContentTypeMetadate(MimeType = "text/x-asm", IsText = true)]
    S,

    [ContentTypeMetadate(MimeType = "audio/s3m", IsBinary = true)]
    S3M,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.smaf-audio", IsBinary = true)]
    SAF,

    [ContentTypeMetadate(MimeType = "application/sbml+xml", IsText = true)]
    SBML,

    [ContentTypeMetadate(MimeType = "application/vnd.ibm.secure-container", IsBinary = true)]
    SC,

    [ContentTypeMetadate(MimeType = "application/x-msschedule", IsBinary = true)]
    SCD,

    [ContentTypeMetadate(MimeType = "application/vnd.lotus-screencam", IsBinary = true)]
    SCM,

    [ContentTypeMetadate(MimeType = "application/scvp-cv-request", IsBinary = true)]
    SCQ,

    [ContentTypeMetadate(MimeType = "application/scvp-cv-response", IsBinary = true)]
    SCS,

    [ContentTypeMetadate(MimeType = "text/vnd.curl.scurl", IsText = true)]
    SCURL,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.draw", IsBinary = true)]
    SDA,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.calc", IsBinary = true)]
    SDC,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.impress", IsBinary = true)]
    SDD,

    [ContentTypeMetadate(MimeType = "application/vnd.solent.sdkm+xml", IsText = true)]
    SDKD,

    [ContentTypeMetadate(MimeType = "application/vnd.solent.sdkm+xml", IsText = true)]
    SDKM,

    [ContentTypeMetadate(MimeType = "application/sdp", IsBinary = true)]
    SDP,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer", IsBinary = true)]
    SDW,

    [ContentTypeMetadate(MimeType = "application/vnd.seemail", IsBinary = true)]
    SEE,

    [ContentTypeMetadate(MimeType = "application/vnd.fdsn.seed", IsBinary = true)]
    SEED,

    [ContentTypeMetadate(MimeType = "application/vnd.sema", IsBinary = true)]
    SEMA,

    [ContentTypeMetadate(MimeType = "application/vnd.semd", IsBinary = true)]
    SEMD,

    [ContentTypeMetadate(MimeType = "application/vnd.semf", IsBinary = true)]
    SEMF,

    [ContentTypeMetadate(MimeType = "application/java-serialized-object", IsBinary = true)]
    SER,

    [ContentTypeMetadate(MimeType = "application/set-payment-initiation", IsBinary = true)]
    SETPAY,

    [ContentTypeMetadate(MimeType = "application/set-registration-initiation", IsBinary = true)]
    SETREG,

    [ContentTypeMetadate(MimeType = "application/vnd.spotfire.sfs", IsBinary = true)]
    SFS,

    [ContentTypeMetadate(MimeType = "text/x-sfv", IsText = true)]
    SFV,

    [ContentTypeMetadate(MimeType = "image/sgi", IsBinary = true)]
    SGI,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer-global", IsBinary = true)]
    SGL,

    [ContentTypeMetadate(MimeType = "text/sgml", IsText = true)]
    SGM,

    [ContentTypeMetadate(MimeType = "text/sgml", IsText = true)]
    SGML,

    [ContentTypeMetadate(MimeType = "application/x-sh", IsBinary = true)]
    SH,

    [ContentTypeMetadate(MimeType = "application/x-shar", IsBinary = true)]
    SHAR,

    [ContentTypeMetadate(MimeType = "application/shf+xml", IsText = true)]
    SHF,

    [ContentTypeMetadate(MimeType = "image/x-mrsid-image", IsBinary = true)]
    SID,

    [ContentTypeMetadate(MimeType = "application/pgp-signature", IsBinary = true)]
    SIG,

    [ContentTypeMetadate(MimeType = "audio/silk", IsBinary = true)]
    SIL,

    [ContentTypeMetadate(MimeType = "model/mesh", IsBinary = true)]
    SILO,

    [ContentTypeMetadate(MimeType = "application/vnd.symbian.install", IsBinary = true)]
    SIS,

    [ContentTypeMetadate(MimeType = "application/vnd.symbian.install", IsBinary = true)]
    SISX,

    [ContentTypeMetadate(MimeType = "application/x-stuffit", IsBinary = true)]
    SIT,

    [ContentTypeMetadate(MimeType = "application/x-stuffitx", IsBinary = true)]
    SITX,

    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKD,

    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKM,

    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKP,

    [ContentTypeMetadate(MimeType = "application/x-koan", IsBinary = true)]
    SKT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-powerpoint.slide.macroenabled.12", IsBinary = true)]
    SLDM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.presentationml.slide",
        IsBinary = true)]
    SLDX,

    [ContentTypeMetadate(MimeType = "application/vnd.epson.salt", IsBinary = true)]
    SLT,

    [ContentTypeMetadate(MimeType = "application/vnd.stepmania.stepchart", IsBinary = true)]
    SM,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.math", IsBinary = true)]
    SMF,

    [ContentTypeMetadate(MimeType = "application/smil+xml", IsText = true)]
    SMI,

    [ContentTypeMetadate(MimeType = "application/smil+xml", IsText = true)]
    SMIL,

    [ContentTypeMetadate(MimeType = "video/x-smv", IsBinary = true)]
    SMV,

    [ContentTypeMetadate(MimeType = "application/vnd.stepmania.package", IsBinary = true)]
    SMZIP,

    [ContentTypeMetadate(MimeType = "audio/basic", IsBinary = true)]
    SND,

    [ContentTypeMetadate(MimeType = "application/x-font-snf", IsBinary = true)]
    SNF,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    SO,

    [ContentTypeMetadate(MimeType = "application/x-pkcs7-certificates", IsBinary = true)]
    SPC,

    [ContentTypeMetadate(MimeType = "application/vnd.yamaha.smaf-phrase", IsBinary = true)]
    SPF,

    [ContentTypeMetadate(MimeType = "application/x-futuresplash", IsBinary = true)]
    SPL,

    [ContentTypeMetadate(MimeType = "text/vnd.in3d.spot", IsText = true)]
    SPOT,

    [ContentTypeMetadate(MimeType = "application/scvp-vp-response", IsBinary = true)]
    SPP,

    [ContentTypeMetadate(MimeType = "application/scvp-vp-request", IsBinary = true)]
    SPQ,

    [ContentTypeMetadate(MimeType = "audio/ogg", IsBinary = true)]
    SPX,

    [ContentTypeMetadate(MimeType = "application/x-sql", IsBinary = true)]
    SQL,

    [ContentTypeMetadate(MimeType = "application/x-wais-source", IsBinary = true)]
    SRC,

    [ContentTypeMetadate(MimeType = "application/x-subrip", IsBinary = true)]
    SRT,

    [ContentTypeMetadate(MimeType = "application/sru+xml", IsText = true)]
    SRU,

    [ContentTypeMetadate(MimeType = "application/sparql-results+xml", IsText = true)]
    SRX,

    [ContentTypeMetadate(MimeType = "application/ssdl+xml", IsText = true)]
    SSDL,

    [ContentTypeMetadate(MimeType = "application/vnd.kodak-descriptor", IsBinary = true)]
    SSE,

    [ContentTypeMetadate(MimeType = "application/vnd.epson.ssf", IsBinary = true)]
    SSF,

    [ContentTypeMetadate(MimeType = "application/ssml+xml", IsText = true)]
    SSML,

    [ContentTypeMetadate(MimeType = "application/vnd.sailingtracker.track", IsBinary = true)]
    ST,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.calc.template", IsBinary = true)]
    STC,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.draw.template", IsBinary = true)]
    STD,

    [ContentTypeMetadate(MimeType = "application/vnd.wt.stf", IsBinary = true)]
    STF,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.impress.template", IsBinary = true)]
    STI,

    [ContentTypeMetadate(MimeType = "application/hyperstudio", IsBinary = true)]
    STK,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-pki.stl", IsBinary = true)]
    STL,

    [ContentTypeMetadate(MimeType = "application/vnd.pg.format", IsBinary = true)]
    STR,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer.template", IsBinary = true)]
    STW,

    [ContentTypeMetadate(MimeType = "text/vnd.dvb.subtitle", IsText = true)]
    SUB,

    [ContentTypeMetadate(MimeType = "application/vnd.sus-calendar", IsBinary = true)]
    SUS,

    [ContentTypeMetadate(MimeType = "application/vnd.sus-calendar", IsBinary = true)]
    SUSP,

    [ContentTypeMetadate(MimeType = "application/x-sv4cpio", IsBinary = true)]
    SV4CPIO,

    [ContentTypeMetadate(MimeType = "application/x-sv4crc", IsBinary = true)]
    SV4CRC,

    [ContentTypeMetadate(MimeType = "application/vnd.dvb.service", IsBinary = true)]
    SVC,

    [ContentTypeMetadate(MimeType = "application/vnd.svd", IsBinary = true)]
    SVD,

    [ContentTypeMetadate(MimeType = "image/svg+xml", IsText = true)]
    SVG,

    [ContentTypeMetadate(MimeType = "image/svg+xml", IsText = true)]
    SVGZ,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    SWA,

    [ContentTypeMetadate(MimeType = "application/x-shockwave-flash", IsBinary = true)]
    SWF,

    [ContentTypeMetadate(MimeType = "application/vnd.aristanetworks.swi", IsBinary = true)]
    SWI,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.calc", IsBinary = true)]
    SXC,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.draw", IsBinary = true)]
    SXD,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer.global", IsBinary = true)]
    SXG,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.impress", IsBinary = true)]
    SXI,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.math", IsBinary = true)]
    SXM,

    [ContentTypeMetadate(MimeType = "application/vnd.sun.xml.writer", IsBinary = true)]
    SXW,

    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    T,

    [ContentTypeMetadate(MimeType = "application/x-t3vm-image", IsBinary = true)]
    T3,

    [ContentTypeMetadate(MimeType = "application/vnd.mynfc", IsBinary = true)]
    TAGLET,

    [ContentTypeMetadate(MimeType = "application/vnd.tao.intent-module-archive", IsBinary = true)]
    TAO,

    [ContentTypeMetadate(MimeType = "application/x-tar", IsBinary = true)]
    TAR,

    [ContentTypeMetadate(MimeType = "application/vnd.3gpp2.tcap", IsBinary = true)]
    TCAP,

    [ContentTypeMetadate(MimeType = "application/x-tcl", IsBinary = true)]
    TCL,

    [ContentTypeMetadate(MimeType = "application/vnd.smart.teacher", IsBinary = true)]
    TEACHER,

    [ContentTypeMetadate(MimeType = "application/tei+xml", IsText = true)]
    TEI,

    [ContentTypeMetadate(MimeType = "application/tei+xml", IsText = true)]
    TEICORPUS,

    [ContentTypeMetadate(MimeType = "application/x-tex", IsBinary = true)]
    TEX,

    [ContentTypeMetadate(MimeType = "application/x-texinfo", IsBinary = true)]
    TEXI,

    [ContentTypeMetadate(MimeType = "application/x-texinfo", IsBinary = true)]
    TEXINFO,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    TEXT,

    [ContentTypeMetadate(MimeType = "application/thraud+xml", IsText = true)]
    TFI,

    [ContentTypeMetadate(MimeType = "application/x-tex-tfm", IsBinary = true)]
    TFM,

    [ContentTypeMetadate(MimeType = "image/x-tga", IsBinary = true)]
    TGA,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-officetheme", IsBinary = true)]
    THMX,

    [ContentTypeMetadate(MimeType = "image/tiff", IsBinary = true)]
    TIF,

    [ContentTypeMetadate(MimeType = "image/tiff", IsBinary = true)]
    TIFF,

    [ContentTypeMetadate(MimeType = "application/vnd.tmobile-livetv", IsBinary = true)]
    TMO,

    [ContentTypeMetadate(MimeType = "application/x-bittorrent", IsBinary = true)]
    TORRENT,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-tool-template", IsBinary = true)]
    TPL,

    [ContentTypeMetadate(MimeType = "application/vnd.trid.tpt", IsBinary = true)]
    TPT,

    [ContentTypeMetadate(MimeType = "application/x-troff", IsBinary = true)]
    TR,

    [ContentTypeMetadate(MimeType = "application/vnd.trueapp", IsBinary = true)]
    TRA,

    [ContentTypeMetadate(MimeType = "application/x-msterminal", IsBinary = true)]
    TRM,

    [ContentTypeMetadate(MimeType = "application/timestamped-data", IsBinary = true)]
    TSD,

    [ContentTypeMetadate(MimeType = "text/tab-separated-values", IsText = true)]
    TSV,

    [ContentTypeMetadate(MimeType = "application/x-font-ttf", IsBinary = true)]
    TTC,

    [ContentTypeMetadate(MimeType = "application/x-font-ttf", IsBinary = true)]
    TTF,

    [ContentTypeMetadate(MimeType = "text/turtle", IsText = true)]
    TTL,

    [ContentTypeMetadate(MimeType = "application/vnd.simtech-mindmapper", IsBinary = true)]
    TWD,

    [ContentTypeMetadate(MimeType = "application/vnd.simtech-mindmapper", IsBinary = true)]
    TWDS,

    [ContentTypeMetadate(MimeType = "application/vnd.genomatix.tuxedo", IsBinary = true)]
    TXD,

    [ContentTypeMetadate(MimeType = "application/vnd.mobius.txf", IsBinary = true)]
    TXF,

    [ContentTypeMetadate(MimeType = "text/plain", IsText = true)]
    TXT,

    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    U32,

    [ContentTypeMetadate(MimeType = "application/x-debian-package", IsBinary = true)]
    UDEB,

    [ContentTypeMetadate(MimeType = "application/vnd.ufdl", IsBinary = true)]
    UFD,

    [ContentTypeMetadate(MimeType = "application/vnd.ufdl", IsBinary = true)]
    UFDL,

    [ContentTypeMetadate(MimeType = "application/x-glulx", IsBinary = true)]
    ULX,

    [ContentTypeMetadate(MimeType = "application/vnd.umajin", IsBinary = true)]
    UMJ,

    [ContentTypeMetadate(MimeType = "application/vnd.unity", IsBinary = true)]
    UNITYWEB,

    [ContentTypeMetadate(MimeType = "application/vnd.uoml+xml", IsText = true)]
    UOML,

    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URI,

    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URIS,

    [ContentTypeMetadate(MimeType = "text/uri-list", IsText = true)]
    URLS,

    [ContentTypeMetadate(MimeType = "application/x-ustar", IsBinary = true)]
    USTAR,

    [ContentTypeMetadate(MimeType = "application/vnd.uiq.theme", IsBinary = true)]
    UTZ,

    [ContentTypeMetadate(MimeType = "text/x-uuencode", IsText = true)]
    UU,

    [ContentTypeMetadate(MimeType = "audio/vnd.dece.audio", IsBinary = true)]
    UVA,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVD,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVF,

    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVG,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.hd", IsBinary = true)]
    UVH,

    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVI,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.mobile", IsBinary = true)]
    UVM,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.pd", IsBinary = true)]
    UVP,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.sd", IsBinary = true)]
    UVS,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.ttml+xml", IsText = true)]
    UVT,

    [ContentTypeMetadate(MimeType = "video/vnd.uvvu.mp4", IsBinary = true)]
    UVU,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.video", IsBinary = true)]
    UVV,

    [ContentTypeMetadate(MimeType = "audio/vnd.dece.audio", IsBinary = true)]
    UVVA,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVVD,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.data", IsBinary = true)]
    UVVF,

    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVVG,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.hd", IsBinary = true)]
    UVVH,

    [ContentTypeMetadate(MimeType = "image/vnd.dece.graphic", IsBinary = true)]
    UVVI,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.mobile", IsBinary = true)]
    UVVM,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.pd", IsBinary = true)]
    UVVP,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.sd", IsBinary = true)]
    UVVS,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.ttml+xml", IsText = true)]
    UVVT,

    [ContentTypeMetadate(MimeType = "video/vnd.uvvu.mp4", IsBinary = true)]
    UVVU,

    [ContentTypeMetadate(MimeType = "video/vnd.dece.video", IsBinary = true)]
    UVVV,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.unspecified", IsBinary = true)]
    UVVX,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.zip", IsBinary = true)]
    UVVZ,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.unspecified", IsBinary = true)]
    UVX,

    [ContentTypeMetadate(MimeType = "application/vnd.dece.zip", IsBinary = true)]
    UVZ,

    [ContentTypeMetadate(MimeType = "text/vcard", IsText = true)]
    VCARD,

    [ContentTypeMetadate(MimeType = "application/x-cdlink", IsBinary = true)]
    VCD,

    [ContentTypeMetadate(MimeType = "text/x-vcard", IsText = true)]
    VCF,

    [ContentTypeMetadate(MimeType = "application/vnd.groove-vcard", IsBinary = true)]
    VCG,

    [ContentTypeMetadate(MimeType = "text/x-vcalendar", IsText = true)]
    VCS,

    [ContentTypeMetadate(MimeType = "application/vnd.vcx", IsBinary = true)]
    VCX,

    [ContentTypeMetadate(MimeType = "application/vnd.visionary", IsBinary = true)]
    VIS,

    [ContentTypeMetadate(MimeType = "video/vnd.vivo", IsBinary = true)]
    VIV,

    [ContentTypeMetadate(MimeType = "video/x-ms-vob", IsBinary = true)]
    VOB,

    [ContentTypeMetadate(MimeType = "application/vnd.stardivision.writer", IsBinary = true)]
    VOR,

    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    VOX,

    [ContentTypeMetadate(MimeType = "model/vrml", IsBinary = true)]
    VRML,

    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSD,

    [ContentTypeMetadate(MimeType = "application/vnd.vsf", IsBinary = true)]
    VSF,

    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSS,

    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VST,

    [ContentTypeMetadate(MimeType = "application/vnd.visio", IsBinary = true)]
    VSW,

    [ContentTypeMetadate(MimeType = "model/vnd.vtu", IsBinary = true)]
    VTU,

    [ContentTypeMetadate(MimeType = "application/voicexml+xml", IsText = true)]
    VXML,

    [ContentTypeMetadate(MimeType = "application/x-director", IsBinary = true)]
    W3D,

    [ContentTypeMetadate(MimeType = "application/x-doom", IsBinary = true)]
    WAD,

    [ContentTypeMetadate(MimeType = "audio/x-wav", IsBinary = true)]
    WAV,

    [ContentTypeMetadate(MimeType = "audio/x-ms-wax", IsBinary = true)]
    WAX,

    [ContentTypeMetadate(MimeType = "image/vnd.wap.wbmp", IsBinary = true)]
    WBMP,

    [ContentTypeMetadate(MimeType = "application/vnd.wap.wbxml", IsText = true)]
    WBMXL,

    [ContentTypeMetadate(MimeType = "application/vnd.criticaltools.wbs+xml", IsText = true)]
    WBS,

    [ContentTypeMetadate(MimeType = "application/vnd.wap.wbxml", IsText = true)]
    WBXML,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WCM,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WDB,

    [ContentTypeMetadate(MimeType = "image/vnd.ms-photo", IsBinary = true)]
    WDP,

    [ContentTypeMetadate(MimeType = "audio/webm", IsBinary = true)]
    WEBA,

    [ContentTypeMetadate(MimeType = "video/webm", IsBinary = true)]
    WEBM,

    [ContentTypeMetadate(MimeType = "image/webp", IsBinary = true)]
    WEBP,

    [ContentTypeMetadate(MimeType = "application/vnd.pmi.widget", IsBinary = true)]
    WG,

    [ContentTypeMetadate(MimeType = "application/widget", IsBinary = true)]
    WGT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WKS,

    [ContentTypeMetadate(MimeType = "video/x-ms-wm", IsBinary = true)]
    WM,

    [ContentTypeMetadate(MimeType = "audio/x-ms-wma", IsBinary = true)]
    WMA,

    [ContentTypeMetadate(MimeType = "application/x-ms-wmd", IsBinary = true)]
    WMD,

    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    WMF,

    [ContentTypeMetadate(MimeType = "text/vnd.wap.wml", IsText = true)]
    WML,

    [ContentTypeMetadate(MimeType = "application/vnd.wap.wmlc", IsBinary = true)]
    WMLC,

    [ContentTypeMetadate(MimeType = "text/vnd.wap.wmlscript", IsText = true)]
    WMLS,

    [ContentTypeMetadate(MimeType = "application/vnd.wap.wmlscriptc", IsBinary = true)]
    WMLSC,

    [ContentTypeMetadate(MimeType = "video/x-ms-wmv", IsBinary = true)]
    WMV,

    [ContentTypeMetadate(MimeType = "video/x-ms-wmx", IsBinary = true)]
    WMX,

    [ContentTypeMetadate(MimeType = "application/x-msmetafile", IsBinary = true)]
    WMZ,

    [ContentTypeMetadate(MimeType = "application/font-woff", IsBinary = true)]
    WOFF,

    [ContentTypeMetadate(MimeType = "application/vnd.wordperfect", IsBinary = true)]
    WPD,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-wpl", IsBinary = true)]
    WPL,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-works", IsBinary = true)]
    WPS,

    [ContentTypeMetadate(MimeType = "application/vnd.wqd", IsBinary = true)]
    WQD,

    [ContentTypeMetadate(MimeType = "application/x-mswrite", IsBinary = true)]
    WRI,

    [ContentTypeMetadate(MimeType = "model/vrml", IsBinary = true)]
    WRL,

    [ContentTypeMetadate(MimeType = "application/wsdl+xml", IsText = true)]
    WSDL,

    [ContentTypeMetadate(MimeType = "application/wspolicy+xml", IsText = true)]
    WSPOLICY,

    [ContentTypeMetadate(MimeType = "application/vnd.webturbo", IsBinary = true)]
    WTB,

    [ContentTypeMetadate(MimeType = "video/x-ms-wvx", IsBinary = true)]
    WVX,

    [ContentTypeMetadate(MimeType = "application/x-authorware-bin", IsBinary = true)]
    X32,

    [ContentTypeMetadate(MimeType = "model/x3d+xml", IsText = true)]
    X3D,

    [ContentTypeMetadate(MimeType = "model/x3d+binary", IsBinary = true)]
    X3DB,

    [ContentTypeMetadate(MimeType = "model/x3d+binary", IsBinary = true)]
    X3DBZ,

    [ContentTypeMetadate(MimeType = "model/x3d+vrml", IsBinary = true)]
    X3DV,

    [ContentTypeMetadate(MimeType = "model/x3d+vrml", IsBinary = true)]
    X3DVZ,

    [ContentTypeMetadate(MimeType = "model/x3d+xml", IsText = true)]
    X3DZ,

    [ContentTypeMetadate(MimeType = "application/xaml+xml", IsText = true)]
    XAML,

    [ContentTypeMetadate(MimeType = "application/x-silverlight-app", IsBinary = true)]
    XAP,

    [ContentTypeMetadate(MimeType = "application/vnd.xara", IsBinary = true)]
    XAR,

    [ContentTypeMetadate(MimeType = "application/x-ms-xbap", IsBinary = true)]
    XBAP,

    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.docuworks.binder", IsBinary = true)]
    XBD,

    [ContentTypeMetadate(MimeType = "image/x-xbitmap", IsBinary = true)]
    XBM,

    [ContentTypeMetadate(MimeType = "application/xcap-diff+xml", IsText = true)]
    XDF,

    [ContentTypeMetadate(MimeType = "application/vnd.syncml.dm+xml", IsText = true)]
    XDM,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.xdp+xml", IsText = true)]
    XDP,

    [ContentTypeMetadate(MimeType = "application/dssc+xml", IsText = true)]
    XDSSC,

    [ContentTypeMetadate(MimeType = "application/vnd.fujixerox.docuworks", IsBinary = true)]
    XDW,

    [ContentTypeMetadate(MimeType = "application/xenc+xml", IsText = true)]
    XENC,

    [ContentTypeMetadate(MimeType = "application/patch-ops-error+xml", IsText = true)]
    XER,

    [ContentTypeMetadate(MimeType = "application/vnd.adobe.xfdf", IsBinary = true)]
    XFDF,

    [ContentTypeMetadate(MimeType = "application/vnd.xfdl", IsBinary = true)]
    XFDL,

    [ContentTypeMetadate(MimeType = "application/xhtml+xml", IsText = true)]
    XHT,

    [ContentTypeMetadate(MimeType = "application/xhtml+xml", IsText = true)]
    XHTML,

    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XHVML,

    [ContentTypeMetadate(MimeType = "image/vnd.xiff", IsBinary = true)]
    XIF,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLA,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.addin.macroenabled.12", IsBinary = true)]
    XLAM,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLC,

    [ContentTypeMetadate(MimeType = "application/x-xliff+xml", IsText = true)]
    XLF,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLM,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLS,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.sheet.binary.macroenabled.12", IsBinary = true)]
    XLSB,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.sheet.macroenabled.12", IsBinary = true)]
    XLSM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        IsBinary = true)]
    XLSX,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLT,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel.template.macroenabled.12", IsBinary = true)]
    XLTM,

    [ContentTypeMetadate(MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
        IsBinary = true)]
    XLTX,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-excel", IsBinary = true)]
    XLW,

    [ContentTypeMetadate(MimeType = "audio/xm", IsBinary = true)]
    XM,

    [ContentTypeMetadate(MimeType = "application/xml", IsText = true)]
    XML,

    [ContentTypeMetadate(MimeType = "application/vnd.olpc-sugar", IsBinary = true)]
    XO,

    [ContentTypeMetadate(MimeType = "application/xop+xml", IsText = true)]
    XOP,

    [ContentTypeMetadate(MimeType = "application/x-xpinstall", IsBinary = true)]
    XPI,

    [ContentTypeMetadate(MimeType = "application/xproc+xml", IsText = true)]
    XPL,

    [ContentTypeMetadate(MimeType = "image/x-xpixmap", IsBinary = true)]
    XPM,

    [ContentTypeMetadate(MimeType = "application/vnd.is-xpr", IsBinary = true)]
    XPR,

    [ContentTypeMetadate(MimeType = "application/vnd.ms-xpsdocument", IsBinary = true)]
    XPS,

    [ContentTypeMetadate(MimeType = "application/vnd.intercon.formnet", IsBinary = true)]
    XPW,

    [ContentTypeMetadate(MimeType = "application/vnd.intercon.formnet", IsBinary = true)]
    XPX,

    [ContentTypeMetadate(MimeType = "application/xml", IsText = true)]
    XSL,

    [ContentTypeMetadate(MimeType = "application/xslt+xml", IsText = true)]
    XSLT,

    [ContentTypeMetadate(MimeType = "application/vnd.syncml+xml", IsText = true)]
    XSM,

    [ContentTypeMetadate(MimeType = "application/xspf+xml", IsText = true)]
    XSPF,

    [ContentTypeMetadate(MimeType = "application/vnd.mozilla.xul+xml", IsText = true)]
    XUL,

    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XVM,

    [ContentTypeMetadate(MimeType = "application/xv+xml", IsText = true)]
    XVML,

    [ContentTypeMetadate(MimeType = "image/x-xwindowdump", IsBinary = true)]
    XWD,

    [ContentTypeMetadate(MimeType = "chemical/x-xyz", IsBinary = true)]
    XYZ,

    [ContentTypeMetadate(MimeType = "application/x-xz", IsBinary = true)]
    XZ,

    [ContentTypeMetadate(MimeType = "text/yaml", IsText = true)]
    YAML,

    [ContentTypeMetadate(MimeType = "application/yang", IsBinary = true)]
    YANG,

    [ContentTypeMetadate(MimeType = "application/yin+xml", IsText = true)]
    YIN,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z1,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z2,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z3,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z4,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z5,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z6,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z7,

    [ContentTypeMetadate(MimeType = "application/x-zmachine", IsBinary = true)]
    Z8,

    [ContentTypeMetadate(MimeType = "application/vnd.zzazz.deck+xml", IsText = true)]
    ZAZ,

    [ContentTypeMetadate(MimeType = "application/zip", IsBinary = true)]
    ZIP,

    [ContentTypeMetadate(MimeType = "application/vnd.zul", IsBinary = true)]
    ZIR,

    [ContentTypeMetadate(MimeType = "application/vnd.zul", IsBinary = true)]
    ZIRZ,

    [ContentTypeMetadate(MimeType = "application/vnd.handheld-entertainment+xml", IsText = true)]
    ZMM,

    [ContentTypeMetadate(MimeType = "application/octet-stream", IsBinary = true)]
    DEFAULT
}