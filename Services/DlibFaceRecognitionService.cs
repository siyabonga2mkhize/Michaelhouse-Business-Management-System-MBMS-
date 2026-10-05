using FaceRecognitionDotNet;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web;
using System.Web.Hosting;
using FaceImage = FaceRecognitionDotNet.Image;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC16 — Real face recognition (runs locally, no cloud)
    //
    // FaceRecognitionDotNet (dlib): finds faces in a photo and turns
    // a face into 128 measurements. Two photos of the same person are
    // close together (distance below ~0.6); different people are far
    // apart.
    //
    //   • A photo must contain exactly ONE face
    //   • A match must be within the threshold (Web.config
    //     FaceRecognition:MatchThreshold, default 0.5 — stricter than
    //     dlib's usual 0.6, so strangers are rejected)
    //   • and clearly closer than the next student
    //     (FaceRecognition:AmbiguityMargin, default 0.06)
    //
    // Needs the four dlib model files in App_Data\FaceModels
    // (Download-FaceModels.ps1) and 64-bit IIS Express.
    // Encodings are stored as "dlib1:" + 128 numbers; anything else
    // (the earlier simulated thumbnails) never matches — those
    // students must enrol again.
    // ============================================================
    public class DlibFaceRecognitionService : IFaceRecognitionService
    {
        public const string EncodingPrefix = "dlib1:";

        public static readonly string[] ModelFiles =
        {
            "dlib_face_recognition_resnet_model_v1.dat",
            "mmod_human_face_detector.dat",
            "shape_predictor_5_face_landmarks.dat",
            "shape_predictor_68_face_landmarks.dat"
        };

        // Photos are shrunk to this before looking for faces (speed)
        private const int MaxImageSize = 800;
        private const double MaxDistance = 2.0;

        private static readonly object Gate = new object();
        private static FaceRecognition _engine;
        private static string _loadError;

        public static double MatchThreshold
        {
            get { return Setting("FaceRecognition:MatchThreshold", 0.5); }
        }

        public static double AmbiguityMargin
        {
            get { return Setting("FaceRecognition:AmbiguityMargin", 0.06); }
        }

        public static bool IsUsableEncoding(string encoding)
        {
            return encoding != null && encoding.StartsWith(EncodingPrefix, StringComparison.Ordinal);
        }

        // ============================================================
        // ENGINE — loaded once (a few seconds), shared by every request
        // ============================================================

        // Called in the background when the website starts, so the
        // first scan doesn't wait for the models to load
        public static void Warmup()
        {
            string error;
            Engine(out error);
        }

        // For the Demo Data page: "Ready" or what's wrong
        public static string StatusText()
        {
            string error;
            return Engine(out error) != null ? "Ready" : error;
        }

        private static FaceRecognition Engine(out string error)
        {
            lock (Gate)
            {
                if (_engine != null) { error = null; return _engine; }

                try
                {
                    var dir = ModelDirectory();
                    var missing = ModelFiles.Where(f => !File.Exists(Path.Combine(dir, f))).ToList();
                    if (missing.Count > 0)
                    {
                        error = "Face recognition is not set up: model file(s) missing in App_Data\\FaceModels (" +
                                string.Join(", ", missing) + "). Run Download-FaceModels.ps1.";
                        return null;
                    }

                    if (!Environment.Is64BitProcess)
                    {
                        error = "Face recognition needs the website to run as 64-bit (64-bit IIS Express). " +
                                "Exit IIS Express from the system tray, then start the website again.";
                        return null;
                    }

                    // ASP.NET runs a copy of the .NET DLLs from a temporary
                    // folder, so tell Windows where the engine's native
                    // DLLs (DlibDotNetNative*.dll) are: the website's bin
                    SetDllDirectory(HttpRuntime.BinDirectory);

                    var watch = Stopwatch.StartNew();
                    _engine = FaceRecognition.Create(dir);
                    Debug.WriteLine("[FaceMatch] Face recognition models loaded in " + watch.ElapsedMilliseconds + " ms.");

                    _loadError = null;
                    error = null;
                    return _engine;
                }
                catch (Exception ex)
                {
                    _loadError = "Face recognition could not start: " + ex.GetBaseException().Message;
                    Debug.WriteLine("[FaceMatch] " + _loadError);
                    error = _loadError;
                    return null;
                }
            }
        }

        private static string ModelDirectory()
        {
            var configured = ConfigurationManager.AppSettings["FaceRecognition:ModelsPath"];
            var path = string.IsNullOrWhiteSpace(configured) ? "~/App_Data/FaceModels" : configured;
            return path.StartsWith("~") ? HostingEnvironment.MapPath(path) : path;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        // ============================================================
        // ANALYSE — exactly one face, then its 128 measurements
        // ============================================================

        public FaceAnalysis Analyse(byte[] imageBytes)
        {
            string error;
            var engine = Engine(out error);
            if (engine == null)
            {
                return new FaceAnalysis { Status = FaceAnalysisStatus.EngineUnavailable, Message = error };
            }

            Bitmap photo = PreparePhoto(imageBytes);
            if (photo == null)
            {
                return new FaceAnalysis { Status = FaceAnalysisStatus.NotAnImage, Message = "The photo could not be read." };
            }

            try
            {
                var watch = Stopwatch.StartNew();

                // The engine isn't safe to use from two requests at once
                lock (Gate)
                {
                    using (var image = FaceRecognition.LoadImage(photo))
                    {
                        var faces = engine.FaceLocations(image, 1, Model.Hog).ToList();

                        if (faces.Count == 0)
                        {
                            Debug.WriteLine("[FaceMatch] No face found in the photo.");
                            return new FaceAnalysis
                            {
                                Status = FaceAnalysisStatus.NoFace,
                                Message = "No face was found in the photo. Make sure the face is clearly visible, well lit and facing the camera."
                            };
                        }

                        if (faces.Count > 1)
                        {
                            Debug.WriteLine("[FaceMatch] " + faces.Count + " faces found in the photo.");
                            return new FaceAnalysis
                            {
                                Status = FaceAnalysisStatus.MultipleFaces,
                                Message = "More than one face is in the photo (" + faces.Count + "). Only one person should be in the picture."
                            };
                        }

                        var encodings = engine.FaceEncodings(image, faces, 1, PredictorModel.Small, Model.Hog).ToList();
                        try
                        {
                            if (encodings.Count == 0)
                            {
                                return new FaceAnalysis
                                {
                                    Status = FaceAnalysisStatus.NoFace,
                                    Message = "No face was found in the photo. Make sure the face is clearly visible, well lit and facing the camera."
                                };
                            }

                            var values = encodings[0].GetRawEncoding();
                            Debug.WriteLine("[FaceMatch] Face found and encoded in " + watch.ElapsedMilliseconds + " ms.");

                            return new FaceAnalysis
                            {
                                Status = FaceAnalysisStatus.Ok,
                                Encoding = EncodingPrefix + string.Join(",",
                                    values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))
                            };
                        }
                        finally
                        {
                            foreach (var e in encodings) e.Dispose();
                        }
                    }
                }
            }
            finally
            {
                photo.Dispose();
            }
        }

        public string GenerateEncoding(byte[] imageBytes)
        {
            var analysis = Analyse(imageBytes);
            return analysis.IsOk ? analysis.Encoding : null;
        }

        // Upright (phone photos store their rotation in EXIF), at most
        // MaxImageSize on the long side, 24-bit RGB. Null if unreadable.
        private static Bitmap PreparePhoto(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;

            try
            {
                using (var stream = new MemoryStream(imageBytes))
                using (var source = System.Drawing.Image.FromStream(stream))
                {
                    ApplyExifOrientation(source);

                    double scale = Math.Min(1.0, (double)MaxImageSize / Math.Max(source.Width, source.Height));
                    int width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int height = Math.Max(1, (int)Math.Round(source.Height * scale));

                    var result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(result))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, width, height);
                    }
                    return result;
                }
            }
            catch (ArgumentException)
            {
                return null;   // not an image
            }
        }

        private static void ApplyExifOrientation(System.Drawing.Image image)
        {
            const int OrientationId = 0x0112;
            if (!image.PropertyIdList.Contains(OrientationId)) return;

            var value = image.GetPropertyItem(OrientationId).Value;
            if (value == null || value.Length == 0) return;

            switch (value[0])
            {
                case 2: image.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: image.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: image.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: image.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }

        // ============================================================
        // COMPARE / MATCH
        // ============================================================

        // Euclidean distance between two faces (same as dlib's
        // FaceDistance); MaxDistance for anything that isn't a dlib encoding
        public double CompareEncodings(string known, string candidate)
        {
            var a = Parse(known);
            var b = Parse(candidate);
            if (a == null || b == null || a.Length != b.Length) return MaxDistance;

            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double d = a[i] - b[i];
                sum += d * d;
            }
            return Math.Sqrt(sum);
        }

        public FaceMatchResult FindBestMatch(
            string candidateEncoding,
            IEnumerable<StudentFaceSignature> enrolledSignatures)
        {
            if (!IsUsableEncoding(candidateEncoding) || enrolledSignatures == null) return null;

            double threshold = MatchThreshold;
            double margin = AmbiguityMargin;

            // Compare against every enrolled student, closest first
            var ranked = enrolledSignatures
                .Where(x => x.IsActive && IsUsableEncoding(x.FaceEncoding))
                .Select(x => new { Signature = x, Distance = CompareEncodings(x.FaceEncoding, candidateEncoding) })
                .OrderBy(x => x.Distance)
                .ToList();

            if (ranked.Count == 0)
            {
                Debug.WriteLine("[FaceMatch] No students are enrolled with face recognition.");
                return null;
            }

            var best = ranked[0];
            var runnerUp = ranked.Skip(1).FirstOrDefault(x => x.Signature.StudentId != best.Signature.StudentId);

            bool closeEnough = best.Distance <= threshold;
            bool unambiguous = runnerUp == null || runnerUp.Distance - best.Distance >= margin;

            // Debug builds only: why a scan did or didn't match, in
            // Visual Studio's Output window (Show output from: Debug)
            Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[FaceMatch] {0} enrolled. Best: student {1} at {2:0.000} (needs <= {3:0.00}){4}. Runner-up: {5}. Result: {6}",
                ranked.Count,
                best.Signature.StudentId,
                best.Distance,
                threshold,
                closeEnough ? "" : " TOO DIFFERENT",
                runnerUp == null ? "none" : string.Format(CultureInfo.InvariantCulture,
                    "student {0} at {1:0.000} (gap {2:0.000}, needs >= {3:0.00}){4}",
                    runnerUp.Signature.StudentId, runnerUp.Distance, runnerUp.Distance - best.Distance,
                    margin, unambiguous ? "" : " TOO CLOSE TO CALL"),
                closeEnough && unambiguous ? "MATCH" : "NO MATCH"));

            return new FaceMatchResult
            {
                Signature = best.Signature,
                StudentId = best.Signature.StudentId,
                Distance = best.Distance,
                // 1.0 at distance 0, 0.5 at the threshold, 0 at twice the threshold
                Confidence = Math.Max(0m, Math.Min(1m, Math.Round((decimal)(1.0 - best.Distance / (2 * threshold)), 2))),
                IsAutoVerified = closeEnough && unambiguous
            };
        }

        private static double[] Parse(string encoding)
        {
            if (!IsUsableEncoding(encoding)) return null;

            var parts = encoding.Substring(EncodingPrefix.Length).Split(',');
            var result = new double[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                {
                    return null;
                }
            }

            return result;
        }

        private static double Setting(string key, double fallback)
        {
            double value;
            var raw = ConfigurationManager.AppSettings[key];
            return raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }
    }
}
