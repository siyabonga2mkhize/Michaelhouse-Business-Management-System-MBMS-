using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC16 — Face Recognition Service
    //
    // Two implementations:
    //  1. DlibFaceRecognitionService — real face recognition, used by
    //     the website and the mobile API (FaceRecognitionServices.Create)
    //  2. SimulatedFaceRecognitionService — the earlier stand-in, kept
    //     for automated tests only
    //
    // The interface is engine-agnostic: an encoding is a vector
    // stored as CSV in StudentFaceSignature.FaceEncoding, and
    // FindBestMatch compares a capture against EVERY enrolled
    // student and only accepts a clear, close match.
    // ============================================================

    public interface IFaceRecognitionService
    {
        // Checks the photo (exactly one face) and encodes that face;
        // Status says why it can't be used
        FaceAnalysis Analyse(byte[] imageBytes);

        // Null when the photo can't be used (see Analyse for why)
        string GenerateEncoding(byte[] imageBytes);

        double CompareEncodings(string known, string candidate);

        FaceMatchResult FindBestMatch(
            string candidateEncoding,
            IEnumerable<StudentFaceSignature> enrolledSignatures);
    }

    public class FaceMatchResult
    {
        public StudentFaceSignature Signature { get; set; }
        public int StudentId { get; set; }
        public double Distance { get; set; }
        public decimal Confidence { get; set; }
        public bool IsAutoVerified { get; set; }
    }

    public enum FaceAnalysisStatus
    {
        Ok = 1,
        NotAnImage = 2,
        NoFace = 3,
        MultipleFaces = 4,
        EngineUnavailable = 5
    }

    public class FaceAnalysis
    {
        public FaceAnalysisStatus Status { get; set; }

        // Set when Status is Ok
        public string Encoding { get; set; }

        // Plain-language reason when it isn't Ok
        public string Message { get; set; }

        public bool IsOk { get { return Status == FaceAnalysisStatus.Ok; } }
    }

    // The engine the website and the mobile API use
    public static class FaceRecognitionServices
    {
        public static IFaceRecognitionService Create()
        {
            return new DlibFaceRecognitionService();
        }
    }

    // ============================================================
    // Simulated engine (tests only) — IMPORTANT: this is not biometric face
    // recognition. Its "encoding" is a small normalised greyscale
    // thumbnail of the centre of the photo, so two photos match when
    // they look alike (same person, pose, lighting and framing). It
    // makes the collection workflow work end to end; a real engine
    // must replace GenerateEncoding before relying on it for identity.
    //
    // Students enrolled under the earlier placeholder (random
    // numbers), or before the crop was made the same shape for every
    // photo size, won't match anyone and need to enrol again.
    // ============================================================

    public class SimulatedFaceRecognitionService : IFaceRecognitionService
    {
        // Encodings are unit vectors, so distances run from 0 (same) to 2.
        // A match must be within MatchThreshold AND at least
        // AmbiguityMargin closer than the next-closest student.
        public const double MatchThreshold = 0.35;
        public const double AmbiguityMargin = 0.05;

        private const int GridWidth = 8;
        private const int GridHeight = 16;     // 8 x 16 = 128 values
        private const double MaxDistance = 2.0;

        // No face detection here: any readable image is "one face"
        public FaceAnalysis Analyse(byte[] imageBytes)
        {
            var encoding = GenerateEncoding(imageBytes);
            return encoding == null
                ? new FaceAnalysis { Status = FaceAnalysisStatus.NotAnImage, Message = "The photo could not be read." }
                : new FaceAnalysis { Status = FaceAnalysisStatus.Ok, Encoding = encoding };
        }

        // Null when the bytes aren't a usable image
        public string GenerateEncoding(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return null;

            double[] values;

            try
            {
                using (var stream = new MemoryStream(imageBytes))
                using (var source = Image.FromStream(stream))
                using (var thumb = new Bitmap(GridWidth, GridHeight))
                {
                    // The face sits in the centre of the frame. Crop the SAME
                    // shape (3 wide x 4 tall, sized from the shorter side)
                    // from every photo, so a 4:3 webcam, a 16:9 webcam and a
                    // portrait phone photo all give comparable thumbnails.
                    int shorter = Math.Min(source.Width, source.Height);
                    int cropWidth = Math.Max(1, (int)(shorter * 0.6));
                    int cropHeight = Math.Max(1, (int)(shorter * 0.8));
                    var crop = new Rectangle((source.Width - cropWidth) / 2, (source.Height - cropHeight) / 2, cropWidth, cropHeight);

                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(source, new Rectangle(0, 0, GridWidth, GridHeight), crop, GraphicsUnit.Pixel);
                    }

                    values = new double[GridWidth * GridHeight];
                    for (int y = 0; y < GridHeight; y++)
                    {
                        for (int x = 0; x < GridWidth; x++)
                        {
                            var c = thumb.GetPixel(x, y);
                            values[y * GridWidth + x] = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                return null;   // not an image
            }

            // Remove overall brightness and contrast, then scale to unit length
            double mean = values.Average();
            for (int i = 0; i < values.Length; i++) values[i] -= mean;

            double norm = Math.Sqrt(values.Sum(v => v * v));
            if (norm < 1e-6) return null;   // blank / uniform frame

            return string.Join(",", values.Select(v => (v / norm).ToString("0.######", CultureInfo.InvariantCulture)));
        }

        public double CompareEncodings(string known, string candidate)
        {
            var a = Parse(known);
            var b = Parse(candidate);

            if (a == null || b == null || a.Length != b.Length)
            {
                return MaxDistance;
            }

            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double d = a[i] - b[i];
                sum += d * d;
            }

            return Math.Min(MaxDistance, Math.Sqrt(sum));
        }

        public FaceMatchResult FindBestMatch(
            string candidateEncoding,
            IEnumerable<StudentFaceSignature> enrolledSignatures)
        {
            if (string.IsNullOrWhiteSpace(candidateEncoding) || enrolledSignatures == null)
            {
                return null;
            }

            // Compare against every enrolled student, closest first
            var ranked = enrolledSignatures
                .Where(x => x.IsActive)
                .Select(x => new { Signature = x, Distance = CompareEncodings(x.FaceEncoding, candidateEncoding) })
                .OrderBy(x => x.Distance)
                .ToList();

            if (ranked.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[FaceMatch] No students are enrolled.");
                return null;
            }

            var best = ranked[0];
            var runnerUp = ranked.Skip(1).FirstOrDefault(x => x.Signature.StudentId != best.Signature.StudentId);

            bool closeEnough = best.Distance <= MatchThreshold;
            bool unambiguous = runnerUp == null || runnerUp.Distance - best.Distance >= AmbiguityMargin;

            // Debug builds only: why a scan did or didn't match, in
            // Visual Studio's Output window (Show output from: Debug)
            System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[FaceMatch] {0} enrolled. Best: student {1} at {2:0.000} (needs <= {3:0.00}){4}. Runner-up: {5}. Result: {6}",
                ranked.Count,
                best.Signature.StudentId,
                best.Distance,
                MatchThreshold,
                closeEnough ? "" : " TOO DIFFERENT",
                runnerUp == null ? "none" : string.Format(CultureInfo.InvariantCulture,
                    "student {0} at {1:0.000} (gap {2:0.000}, needs >= {3:0.00}){4}",
                    runnerUp.Signature.StudentId, runnerUp.Distance, runnerUp.Distance - best.Distance,
                    AmbiguityMargin, unambiguous ? "" : " TOO CLOSE TO CALL"),
                closeEnough && unambiguous ? "MATCH" : "NO MATCH"));

            return new FaceMatchResult
            {
                Signature = best.Signature,
                StudentId = best.Signature.StudentId,
                Distance = best.Distance,
                Confidence = Math.Max(0m, Math.Min(1m, Math.Round((decimal)(1.0 - best.Distance / MaxDistance), 2))),
                IsAutoVerified = closeEnough && unambiguous
            };
        }

        private static double[] Parse(string encoding)
        {
            if (string.IsNullOrWhiteSpace(encoding)) return null;

            var parts = encoding.Split(',');
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
    }
}
