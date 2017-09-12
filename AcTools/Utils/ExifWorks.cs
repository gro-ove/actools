using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text;

namespace AcTools.Utils {
    /// <summary>
    /// Taken from https://www.codeproject.com/Articles/4956/The-ExifWorks-class.
    /// </summary>
    public class ExifWorks : IDisposable {
        /// <summary>
        /// Contains possible values of EXIF tag names (ID).
        /// </summary>
        public enum TagNames {
            ExifIfd = 0x8769,
            GpsIfd = 0x8825,
            NewSubfileType = 0xfe,
            SubfileType = 0xff,
            ImageWidth = 0x100,
            ImageHeight = 0x101,
            BitsPerSample = 0x102,
            Compression = 0x103,
            PhotometricInterp = 0x106,
            ThreshHolding = 0x107,
            CellWidth = 0x108,
            CellHeight = 0x109,
            FillOrder = 0x10a,
            DocumentName = 0x10d,
            ImageDescription = 0x10e,
            EquipMake = 0x10f,
            EquipModel = 0x110,
            StripOffsets = 0x111,
            Orientation = 0x112,
            SamplesPerPixel = 0x115,
            RowsPerStrip = 0x116,
            StripBytesCount = 0x117,
            MinSampleValue = 0x118,
            MaxSampleValue = 0x119,
            XResolution = 0x11a,
            YResolution = 0x11b,
            PlanarConfig = 0x11c,
            PageName = 0x11d,
            XPosition = 0x11e,
            YPosition = 0x11f,
            FreeOffset = 0x120,
            FreeByteCounts = 0x121,
            GrayResponseUnit = 0x122,
            GrayResponseCurve = 0x123,
            T4Option = 0x124,
            T6Option = 0x125,
            ResolutionUnit = 0x128,
            PageNumber = 0x129,
            TransferFuncition = 0x12d,
            SoftwareUsed = 0x131,
            DateTime = 0x132,
            Artist = 0x13b,
            HostComputer = 0x13c,
            Predictor = 0x13d,
            WhitePoint = 0x13e,
            PrimaryChromaticities = 0x13f,
            ColorMap = 0x140,
            HalftoneHints = 0x141,
            TileWidth = 0x142,
            TileLength = 0x143,
            TileOffset = 0x144,
            TileByteCounts = 0x145,
            InkSet = 0x14c,
            InkNames = 0x14d,
            NumberOfInks = 0x14e,
            DotRange = 0x150,
            TargetPrinter = 0x151,
            ExtraSamples = 0x152,
            SampleFormat = 0x153,
            SMinSampleValue = 0x154,
            SMaxSampleValue = 0x155,
            TransferRange = 0x156,
            JpegProc = 0x200,
            JpegInterFormat = 0x201,
            JpegInterLength = 0x202,
            JpegRestartInterval = 0x203,
            JpegLosslessPredictors = 0x205,
            JpegPointTransforms = 0x206,
            JpegqTables = 0x207,
            JpegdcTables = 0x208,
            JpegacTables = 0x209,
            YCbCrCoefficients = 0x211,
            YCbCrSubsampling = 0x212,
            YCbCrPositioning = 0x213,
            RefBlackWhite = 0x214,
            IccProfile = 0x8773,
            Gamma = 0x301,
            IccProfileDescriptor = 0x302,
            SrgbRenderingIntent = 0x303,
            ImageTitle = 0x320,
            Copyright = 0x8298,
            ResolutionXUnit = 0x5001,
            ResolutionYUnit = 0x5002,
            ResolutionXLengthUnit = 0x5003,
            ResolutionYLengthUnit = 0x5004,
            PrintFlags = 0x5005,
            PrintFlagsVersion = 0x5006,
            PrintFlagsCrop = 0x5007,
            PrintFlagsBleedWidth = 0x5008,
            PrintFlagsBleedWidthScale = 0x5009,
            HalftoneLpi = 0x500a,
            HalftoneLpiUnit = 0x500b,
            HalftoneDegree = 0x500c,
            HalftoneShape = 0x500d,
            HalftoneMisc = 0x500e,
            HalftoneScreen = 0x500f,
            JpegQuality = 0x5010,
            GridSize = 0x5011,
            ThumbnailFormat = 0x5012,
            ThumbnailWidth = 0x5013,
            ThumbnailHeight = 0x5014,
            ThumbnailColorDepth = 0x5015,
            ThumbnailPlanes = 0x5016,
            ThumbnailRawBytes = 0x5017,
            ThumbnailSize = 0x5018,
            ThumbnailCompressedSize = 0x5019,
            ColorTransferFunction = 0x501a,
            ThumbnailData = 0x501b,
            ThumbnailImageWidth = 0x5020,
            ThumbnailImageHeight = 0x502,
            ThumbnailBitsPerSample = 0x5022,
            ThumbnailCompression = 0x5023,
            ThumbnailPhotometricInterp = 0x5024,
            ThumbnailImageDescription = 0x5025,
            ThumbnailEquipMake = 0x5026,
            ThumbnailEquipModel = 0x5027,
            ThumbnailStripOffsets = 0x5028,
            ThumbnailOrientation = 0x5029,
            ThumbnailSamplesPerPixel = 0x502a,
            ThumbnailRowsPerStrip = 0x502b,
            ThumbnailStripBytesCount = 0x502c,
            ThumbnailResolutionX = 0x502d,
            ThumbnailResolutionY = 0x502e,
            ThumbnailPlanarConfig = 0x502f,
            ThumbnailResolutionUnit = 0x5030,
            ThumbnailTransferFunction = 0x5031,
            ThumbnailSoftwareUsed = 0x5032,
            ThumbnailDateTime = 0x5033,
            ThumbnailArtist = 0x5034,
            ThumbnailWhitePoint = 0x5035,
            ThumbnailPrimaryChromaticities = 0x5036,
            ThumbnailYCbCrCoefficients = 0x5037,
            ThumbnailYCbCrSubsampling = 0x5038,
            ThumbnailYCbCrPositioning = 0x5039,
            ThumbnailRefBlackWhite = 0x503a,
            ThumbnailCopyRight = 0x503b,
            LuminanceTable = 0x5090,
            ChrominanceTable = 0x5091,
            FrameDelay = 0x5100,
            LoopCount = 0x5101,
            PixelUnit = 0x5110,
            PixelPerUnitX = 0x5111,
            PixelPerUnitY = 0x5112,
            PaletteHistogram = 0x5113,
            ExifExposureTime = 0x829a,
            ExifFNumber = 0x829d,
            ExifExposureProg = 0x8822,
            ExifSpectralSense = 0x8824,
            ExifIsoSpeed = 0x8827,
            ExifOecf = 0x8828,
            ExifVer = 0x9000,
            ExifDtOrig = 0x9003,
            ExifDtDigitized = 0x9004,
            ExifCompConfig = 0x9101,
            ExifCompBpp = 0x9102,
            ExifShutterSpeed = 0x9201,
            ExifAperture = 0x9202,
            ExifBrightness = 0x9203,
            ExifExposureBias = 0x9204,
            ExifMaxAperture = 0x9205,
            ExifSubjectDist = 0x9206,
            ExifMeteringMode = 0x9207,
            ExifLightSource = 0x9208,
            ExifFlash = 0x9209,
            ExifFocalLength = 0x920a,
            ExifMakerNote = 0x927c,
            ExifUserComment = 0x9286,
            ExifDtSubsec = 0x9290,
            ExifDtOrigSs = 0x9291,
            ExifDtDigSs = 0x9292,
            ExifFpxVer = 0xa000,
            ExifColorSpace = 0xa001,
            ExifPixXDim = 0xa002,
            ExifPixYDim = 0xa003,
            ExifRelatedWav = 0xa004,
            ExifInterop = 0xa005,
            ExifFlashEnergy = 0xa20b,
            ExifSpatialFr = 0xa20c,
            ExifFocalXRes = 0xa20e,
            ExifFocalYRes = 0xa20f,
            ExifFocalResUnit = 0xa210,
            ExifSubjectLoc = 0xa214,
            ExifExposureIndex = 0xa215,
            ExifSensingMethod = 0xa217,
            ExifFileSource = 0xa300,
            ExifSceneType = 0xa301,
            ExifCfaPattern = 0xa302,
            GpsVer = 0x0,
            GpsLatitudeRef = 0x1,
            GpsLatitude = 0x2,
            GpsLongitudeRef = 0x3,
            GpsLongitude = 0x4,
            GpsAltitudeRef = 0x5,
            GpsAltitude = 0x6,
            GpsGpsTime = 0x7,
            GpsGpsSatellites = 0x8,
            GpsGpsStatus = 0x9,
            GpsGpsMeasureMode = 0xa,
            GpsGpsDop = 0xb,
            GpsSpeedRef = 0xc,
            GpsSpeed = 0xd,
            GpsTrackRef = 0xe,
            GpsTrack = 0xf,
            GpsImgDirRef = 0x10,
            GpsImgDir = 0x11,
            GpsMapDatum = 0x12,
            GpsDestLatRef = 0x13,
            GpsDestLat = 0x14,
            GpsDestLongRef = 0x15,
            GpsDestLong = 0x16,
            GpsDestBearRef = 0x17,
            GpsDestBear = 0x18,
            GpsDestDistRef = 0x19,
            GpsDestDist = 0x1a
        }

        /// <summary>
        /// Real position of 0th row and column of picture.
        /// </summary>
        public enum Orientations {
            TopLeft = 1,
            TopRight = 2,
            BottomRight = 3,
            BottomLeft = 4,
            LeftTop = 5,
            RightTop = 6,
            RightBottom = 7,
            LftBottom = 8
        }

        /// <summary>
        /// Exposure programs.
        /// </summary>
        public enum ExposurePrograms {
            Manual = 1,
            Normal = 2,
            AperturePriority = 3,
            ShutterPriority = 4,
            Creative = 5,
            Action = 6,
            Portrait = 7,
            Landscape = 8
        }

        /// <summary>
        /// Exposure metering modes.
        /// </summary>
        public enum ExposureMeteringModes {
            Unknown = 0,
            Average = 1,
            CenterWeightedAverage = 2,
            Spot = 3,
            MultiSpot = 4,
            MultiSegment = 5,
            Partial = 6,
            Other = 255
        }

        /// <summary>
        /// Flash activity modes.
        /// </summary>
        public enum FlashModes {
            NotFired = 0,
            Fired = 1,
            FiredButNoStrobeReturned = 5,
            FiredAndStrobeReturned = 7
        }

        /// <summary>
        /// Possible light sources (white balance).
        /// </summary>
        public enum LightSources {
            Unknown = 0,
            Daylight = 1,
            Fluorescent = 2,
            Tungsten = 3,
            Flash = 10,
            StandardLightA = 17,
            StandardLightB = 18,
            StandardLightC = 19,
            D55 = 20,
            D65 = 21,
            D75 = 22,
            Other = 255
        }

        /// <summary>
        /// EXIF data types.
        /// </summary>
        public enum ExifDataTypes : short {
            UnsignedByte = 1,
            AsciiString = 2,
            UnsignedShort = 3,
            UnsignedLong = 4,
            UnsignedRational = 5,
            SignedByte = 6,
            Undefined = 7,
            SignedShort = 8,
            SignedLong = 9,
            SignedRational = 10,
            SingleFloat = 11,
            DoubleFloat = 12
        }

        /// <summary>
        /// Represents rational which is type of some Exif properties.
        /// </summary>
        public struct Rational {
            public int Numerator;
            public int Denominator;

            /// <summary>
            /// Converts rational to string representation.
            /// </summary>
            public string ToString(string delimiter = "/") {
                return Numerator + delimiter + Denominator;
            }

            /// <summary>
            /// Converts rational to double precision real number.
            /// </summary>
            public double ToDouble() {
                return (double)Numerator / Denominator;
            }
        }

        private readonly Image _image;
        private readonly string _filename;

        /// <summary>
        /// Initializes new instance of this class.
        /// </summary>
        /// <param name="bitmap">Bitmap to read exif information from</param>
        public ExifWorks(Image bitmap) {
            _image = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        }

        /// <summary>
        /// Initializes new instance of this class.
        /// </summary>
        /// <param name="filename">Name of file to be loaded</param>
        public ExifWorks(string filename) {
            _filename = filename;
            _image = Image.FromFile(filename);
        }

        public void Save() {
            if (_filename == null) throw new Exception("Created this way, can’t be saved");
            _image.Save(_filename);
        }

        private Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Get or set encoding used for string metadata.
        /// </summary>
        /// <value>Encoding used for string metadata</value>
        /// <remarks>Default encoding is UTF-8</remarks>
        public Encoding Encoding {
            get => _encoding;
            set {
                if (value == null) throw new ArgumentNullException();
                _encoding = Encoding;
            }
        }

        /// <summary>
        /// Returns copy of bitmap this instance is working on.
        /// </summary>
        public Bitmap GetBitmap() {
            return (Bitmap)_image.Clone();
        }

        /// <summary>
        /// Brand of equipment (EXIF EquipMake).
        /// </summary>
        public string EquipmentMaker => GetPropertyString(TagNames.EquipMake);

        /// <summary>
        /// Model of equipment (EXIF EquipModel).
        /// </summary>
        public string EquipmentModel => GetPropertyString(TagNames.EquipModel);

        /// <summary>
        /// Software used for processing (EXIF Software).
        /// </summary>
        public string Software {
            get => GetPropertyString(TagNames.SoftwareUsed);
            set {
                try {
                    SetPropertyString(TagNames.SoftwareUsed, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Subject (EXIF Subject).
        /// </summary>
        public string Subject {
            get => GetPropertyString(TagNames.ExifSubjectDist);
            set {
                try {
                    SetPropertyString(TagNames.SoftwareUsed, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Orientation of image (EXIF Orientation).
        /// </summary>
        public Orientations Orientation {
            get {
                int x = GetPropertyInt16(TagNames.Orientation);
                if (!Enum.IsDefined(typeof(Orientations), x)) return Orientations.TopLeft;

                var name = Enum.GetName(typeof(Orientations), x);
                return name == null ? Orientations.TopLeft : (Orientations)Enum.Parse(typeof(Orientations), name);
            }
        }

        /// <summary>
        /// Time when image was last modified (EXIF DateTime).
        /// </summary>
        public DateTime DateTimeLastModified {
            get {
                try {
                    return DateTime.ParseExact(GetPropertyString(TagNames.DateTime), "yyyy\\:MM\\:dd HH\\:mm\\:ss", null);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                    return DateTime.MinValue;
                }
            }
            set {
                try {
                    SetPropertyString(TagNames.DateTime, value.ToString("yyyy\\:MM\\:dd HH\\:mm\\:ss"));
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Time when image was taken (EXIF DateTimeOriginal).
        /// </summary>
        public DateTime DateTimeOriginal {
            get {
                try {
                    return DateTime.ParseExact(GetPropertyString(TagNames.ExifDtOrig), "yyyy\\:MM\\:dd HH\\:mm\\:ss", null);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                    return DateTime.MinValue;
                }
            }
            set {
                try {
                    SetPropertyString(TagNames.ExifDtOrig, value.ToString("yyyy\\:MM\\:dd HH\\:mm\\:ss"));
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Time when image was digitized (EXIF DateTimeDigitized).
        /// </summary>
        public DateTime DateTimeDigitized {
            get {
                try {
                    return DateTime.ParseExact(GetPropertyString(TagNames.ExifDtDigitized), "yyyy\\:MM\\:dd HH\\:mm\\:ss", null);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                    return DateTime.MinValue;
                }
            }
            set {
                try {
                    SetPropertyString(TagNames.ExifDtDigitized, value.ToString("yyyy\\:MM\\:dd HH\\:mm\\:ss"));
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        public int Width => _image.Width;

        public int Height => _image.Height;

        public double ResolutionX {
            get {
                var r = GetPropertyRational(TagNames.XResolution).ToDouble();

                // Resolution is in points/cm
                if (GetPropertyInt16(TagNames.ResolutionUnit) == 3) return r * 2.54;

                // Resolution is in points/inch
                return r;
            }
        }

        public double ResolutionY {
            get {
                var r = GetPropertyRational(TagNames.YResolution).ToDouble();

                // Resolution is in points/cm
                if (GetPropertyInt16(TagNames.ResolutionUnit) == 3) return r * 2.54;

                // Resolution is in points/inch
                return r;
            }
        }

        public string Title {
            get => GetPropertyString(TagNames.ImageTitle);
            set {
                try {
                    SetPropertyString(TagNames.ImageTitle, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        public string UserComment {
            get => GetPropertyString(TagNames.ExifUserComment);
            set {
                try {
                    SetPropertyString(TagNames.ExifUserComment, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Artist name (EXIF Artist).
        /// </summary>
        public string Artist {
            get => GetPropertyString(TagNames.Artist);
            set {
                try {
                    SetPropertyString(TagNames.Artist, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Image description (EXIF ImageDescription).
        /// </summary>
        public string Description {
            get => GetPropertyString(TagNames.ImageDescription);
            set {
                try {
                    SetPropertyString(TagNames.ImageDescription, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Image copyright (EXIF Copyright).
        /// </summary>
        public string Copyright {
            get => GetPropertyString(TagNames.Copyright);
            set {
                try {
                    SetPropertyString(TagNames.Copyright, value);
                } catch (Exception ex) {
                    AcToolsLogging.Write(ex);
                }
            }
        }

        /// <summary>
        /// Exposure time in seconds (EXIF ExifExposureTime/ExifShutterSpeed).
        /// </summary>
        public double ExposureTime {
            get {
                if (IsPropertyDefined(TagNames.ExifExposureTime)) {
                    // Exposure time is explicitly specified
                    return GetPropertyRational(TagNames.ExifExposureTime).ToDouble();
                }

                if (IsPropertyDefined(TagNames.ExifShutterSpeed)) {
                    // Compute exposure time from shutter speed
                    return 1 / Math.Pow(2, GetPropertyRational(TagNames.ExifShutterSpeed).ToDouble());
                }

                // Can't figure out
                return 0;
            }
        }

        /// <summary>
        /// Aperture value as F number (EXIF ExifFNumber/ExifApertureValue).
        /// </summary>
        public double Aperture => IsPropertyDefined(TagNames.ExifFNumber) ? GetPropertyRational(TagNames.ExifFNumber).ToDouble()
                : IsPropertyDefined(TagNames.ExifAperture) ? Math.Pow(Math.Sqrt(2), GetPropertyRational(TagNames.ExifAperture).ToDouble()) : 0;

        /// <summary>
        /// Exposure program used (EXIF ExifExposureProg).
        /// </summary>
        public ExposurePrograms ExposureProgram {
            get {
                int x = GetPropertyInt16(TagNames.ExifExposureProg);
                if (!Enum.IsDefined(typeof(ExposurePrograms), x)) return ExposurePrograms.Normal;

                var name = Enum.GetName(typeof(ExposurePrograms), x);
                return name == null ? ExposurePrograms.Normal : (ExposurePrograms)Enum.Parse(typeof(ExposurePrograms), name);
            }
        }

        public short Iso => GetPropertyInt16(TagNames.ExifIsoSpeed);

        /// <summary>
        /// Subject distance in meters (EXIF SubjectDistance).
        /// </summary>
        public double SubjectDistance => GetPropertyRational(TagNames.ExifSubjectDist).ToDouble();

        /// <summary>
        /// Exposure method metering mode used (EXIF MeteringMode).
        /// </summary>
        public ExposureMeteringModes ExposureMeteringMode {
            get {
                int x = GetPropertyInt16(TagNames.ExifMeteringMode);
                if (!Enum.IsDefined(typeof(ExposureMeteringModes), x)) return ExposureMeteringModes.Unknown;

                var name = Enum.GetName(typeof(ExposureMeteringModes), x);
                return name == null ? ExposureMeteringModes.Unknown : (ExposureMeteringModes)Enum.Parse(typeof(ExposureMeteringModes), name);
            }
        }

        /// <summary>
        /// Focal length of lenses in mm (EXIF FocalLength).
        /// </summary>
        public double FocalLength => GetPropertyRational(TagNames.ExifFocalLength).ToDouble();

        /// <summary>
        /// Flash mode (EXIF Flash).
        /// </summary>
        public FlashModes FlashMode {
            get {
                int x = GetPropertyInt16(TagNames.ExifFlash);
                if (!Enum.IsDefined(typeof(FlashModes), x)) return FlashModes.NotFired;

                var name = Enum.GetName(typeof(FlashModes), x);
                return name == null ? FlashModes.NotFired : (FlashModes)Enum.Parse(typeof(FlashModes), name);
            }
        }

        /// <summary>
        /// Light source or white balance (EXIF LightSource).
        /// </summary>
        public LightSources LightSource {
            get {
                int x = GetPropertyInt16(TagNames.ExifLightSource);
                if (!Enum.IsDefined(typeof(LightSources), x)) return LightSources.Unknown;

                var name = Enum.GetName(typeof(LightSources), x);
                return name == null ? LightSources.Unknown : (LightSources)Enum.Parse(typeof(LightSources), name);
            }
        }

        /// <summary>
        /// Checks if current image has specified certain property.
        /// </summary>
        public bool IsPropertyDefined(TagNames pid) {
            return Convert.ToBoolean(Array.IndexOf(_image.PropertyIdList, (int)pid) > -1);
        }

        /// <summary>
        /// Gets specified Int32 property.
        /// </summary>
        public int GetPropertyInt32(TagNames pid, int defaultValue = 0) {
            return IsPropertyDefined(pid) ? GetInt32(_image.GetPropertyItem((int)pid).Value) : defaultValue;
        }

        /// <summary>
        /// Gets specified Int16 property.
        /// </summary>
        public short GetPropertyInt16(TagNames pid, short defaultValue = 0) {
            return IsPropertyDefined(pid) ? GetInt16(_image.GetPropertyItem((int)pid).Value) : defaultValue;
        }

        /// <summary>
        /// Gets specified string property.
        /// </summary>
        public string GetPropertyString(TagNames pid, string defaultValue = "") {
            return IsPropertyDefined(pid) ? GetString(_image.GetPropertyItem((int)pid).Value) : defaultValue;
        }

        /// <summary>
        /// Gets specified property in raw form.
        /// </summary>
        public byte[] GetProperty(TagNames pid, byte[] defaultValue = null) {
            return IsPropertyDefined(pid) ? _image.GetPropertyItem((int)pid).Value : defaultValue;
        }

        /// <summary>
        /// Gets specified rational property.
        /// </summary>
        public Rational GetPropertyRational(TagNames pid) {
            if (IsPropertyDefined(pid)) {
                return GetRational(_image.GetPropertyItem((int)pid).Value);
            }
            var r = default(Rational);
            r.Numerator = 0;
            r.Denominator = 1;
            return r;
        }

        /// <summary>
        /// Sets specified string property.
        /// </summary>
        public void SetPropertyString(TagNames pid, string value) {
            var data = _encoding.GetBytes(value + '\0');
            SetProperty(pid, data, ExifDataTypes.AsciiString);
        }

        /// <summary>
        /// Sets specified Int16 property.
        /// </summary>
        public void SetPropertyInt16(TagNames pid, short value) {
            var data = new byte[2];
            data[0] = Convert.ToByte(value & 0xff);
            data[1] = Convert.ToByte((value & 0xff00) >> 8);
            SetProperty(pid, data, ExifDataTypes.SignedShort);
        }

        /// <summary>
        /// Sets specified Int32 property.
        /// </summary>
        public void SetPropertyInt32(TagNames pid, int value) {
            var data = new byte[4];
            for (var I = 0; I <= 3; I++) {
                data[I] = Convert.ToByte(value & 0xff);
                value >>= 8;
            }
            SetProperty(pid, data, ExifDataTypes.SignedLong);
        }

        private static T CreateInstance<T>(params object[] args) {
            return (T)typeof(T).Assembly.CreateInstance(typeof(T).FullName ?? "", false,
                    BindingFlags.Instance | BindingFlags.NonPublic, null, args, null, null);
        }

        /// <summary>
        /// Sets specified propery in raw form.
        /// </summary>
        public void SetProperty(TagNames pid, byte[] data, ExifDataTypes type) {
            var p = CreateInstance<PropertyItem>();
            p.Id = (int)pid;
            p.Value = data;
            p.Type = (short)type;
            p.Len = data.Length;
            _image.SetPropertyItem(p);
        }

        /// <summary>
        /// Reads Int32 from EXIF bytes.
        /// </summary>
        private int GetInt32(byte[] b) {
            if (b.Length < 4) throw new ArgumentException("Data too short (4 bytes expected)", nameof(b));
            return b[3] << 24 | b[2] << 16 | b[1] << 8 | b[0];
        }

        /// <summary>
        /// Reads Int16 from EXIF bytes.
        /// </summary>
        private short GetInt16(byte[] b) {
            if (b.Length < 2) throw new ArgumentException("Data too short (2 bytes expected)", nameof(b));
            return (short)((b[1] << 8) | b[0]);
        }

        /// <summary>
        /// Reads string from EXIF bytes.
        /// </summary>
        private string GetString(byte[] b) {
            var r = _encoding.GetString(b);
            if (r.Length > 0 && r[r.Length - 1] == '\0') r = r.Substring(0, r.Length - 1);
            return r;
        }

        /// <summary>
        /// Reads rational from EXIF bytes.
        /// </summary>
        private Rational GetRational(byte[] b) {
            var r = new Rational();
            var n = new byte[4];
            var d = new byte[4];
            Array.Copy(b, 0, n, 0, 4);
            Array.Copy(b, 4, d, 0, 4);
            r.Denominator = GetInt32(d);
            r.Numerator = GetInt32(n);
            return r;
        }

        public void Dispose() {
            if (_filename != null) {
                _image.Dispose();
            }
        }
    }
}