using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC16 — Face Recognition Service
    //
    // Two implementations exist:
    //  1. SimulatedFaceRecognitionService  — used today.
    //  2. RealFaceRecognitionService (future) — same interface,
    //     powered by FaceRecognitionDotNet.
    // ============================================================

    public interface IFaceRecognitionService
    {
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

    public class SimulatedFaceRecognitionService : IFaceRecognitionService
    {
        // Thresholds (lower distance = better match)
        public const double ThresholdAutoVerify = 0.60;
        public const double ThresholdConfirm = 0.70;

        public string GenerateEncoding(byte[] imageBytes)
        {
            int seed = 17;
            if (imageBytes != null && imageBytes.Length > 0)
            {
                seed = imageBytes.Length + imageBytes[0] + imageBytes[imageBytes.Length - 1];
            }

            var rng = new Random(seed);
            var numbers = new decimal[128];
            for (int i = 0; i < 128; i++)
            {
                numbers[i] = Math.Round((decimal)rng.NextDouble(), 6);
            }

            return string.Join(",", numbers);
        }

        public double CompareEncodings(string known, string candidate)
        {
            if (string.IsNullOrWhiteSpace(known) || string.IsNullOrWhiteSpace(candidate))
            {
                return 1.0;
            }

            int combinedHash = known.GetHashCode() ^ candidate.GetHashCode();
            double fraction = Math.Abs(combinedHash % 1000) / 1000.0;

            // 0.05 – 0.25  →  confidence 75 – 95 %
            return 0.05 + (fraction * 0.20);
        }

        public FaceMatchResult FindBestMatch(
            string candidateEncoding,
            IEnumerable<StudentFaceSignature> enrolledSignatures)
        {
            if (string.IsNullOrWhiteSpace(candidateEncoding) || enrolledSignatures == null)
            {
                return null;
            }

            var first = enrolledSignatures.FirstOrDefault(x => x.IsActive);
            if (first == null)
            {
                return null;
            }

            double distance = CompareEncodings(first.FaceEncoding, candidateEncoding);

            return new FaceMatchResult
            {
                Signature = first,
                StudentId = first.StudentId,
                Distance = distance,
                Confidence = Math.Round((decimal)(1.0 - distance), 2),
                IsAutoVerified = distance <= ThresholdAutoVerify
            };
        }
    }
}