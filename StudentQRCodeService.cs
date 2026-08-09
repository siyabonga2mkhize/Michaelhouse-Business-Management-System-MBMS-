using Michaelhouse.Models;
using QRCoder;
using System;
using System.Linq;
using System.Text;
using System.Web.Security;

namespace Michaelhouse.Services
{
    public class StudentQRCodeService
    {
        private readonly DBContextClass db = new DBContextClass();

        private const string Prefix = "MH-STU";
        private const int MaxGenerationAttempts = 10;

        public StudentQRCode GenerateQRCode(int studentId)
        {
            var student = db.Students.Find(studentId);

            if (student == null)
                throw new InvalidOperationException("Student not found.");

            var existing = GetActiveQRCode(studentId);

            if (existing != null)
            {
                // Repair older QR records that have a value but no stored image.
                if (existing.QRImage == null || existing.QRImage.Length == 0)
                {
                    existing.QRImage = GenerateQRImage(existing.QRCodeValue);
                    db.SaveChanges();
                }

                return existing;
            }

            string value = GenerateUniqueValue(student);
            byte[] image = GenerateQRImage(value);

            var qr = new StudentQRCode
            {
                StudentId = studentId,
                QRCodeValue = value,
                QRImage = image,
                DateGenerated = DateTime.Now,
                IsActive = true
            };

            db.StudentQRCodes.Add(qr);
            db.SaveChanges();

            return qr;
        }

        public StudentQRCode RegenerateQRCode(int studentId)
        {
            var current = GetActiveQRCode(studentId);

            if (current != null)
            {
                current.IsActive = false;
                db.SaveChanges();
            }

            var student = db.Students.Find(studentId);
            string value = GenerateUniqueValue(student);
            byte[] image = GenerateQRImage(value);

            var qr = new StudentQRCode
            {
                StudentId = studentId,
                QRCodeValue = value,
                QRImage = image,
                DateGenerated = DateTime.Now,
                IsActive = true,
                RegeneratedFromId = current?.QRCodeId
            };

            db.StudentQRCodes.Add(qr);
            db.SaveChanges();

            return qr;
        }

        public StudentQRCode GetActiveQRCode(int studentId)
        {
            return db.StudentQRCodes
                .Where(x => x.StudentId == studentId && x.IsActive)
                .OrderByDescending(x => x.DateGenerated)
                .FirstOrDefault();
        }

        public StudentQRCode GetByValue(string qrValue)
        {
            return db.StudentQRCodes
                .FirstOrDefault(x => x.QRCodeValue == qrValue && x.IsActive);
        }

        private string GenerateUniqueValue(Student student)
        {
            if (student == null)
                throw new InvalidOperationException("Student not found.");

            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                var tokenSource = $"{student.StudentId}|{student.StudentNumber}|{Guid.NewGuid():N}|{DateTime.UtcNow.Ticks}";
                var protectedBytes = MachineKey.Protect(Encoding.UTF8.GetBytes(tokenSource), "Michaelhouse.StudentQR");
                var token = Convert.ToBase64String(protectedBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

                string candidate = $"{Prefix}|{student.StudentId}|{student.StudentNumber}|{token}";
                if (!db.StudentQRCodes.Any(x => x.QRCodeValue == candidate))
                    return candidate;
            }

            throw new InvalidOperationException("Could not generate a unique QR code after multiple attempts.");
        }

        public byte[] GenerateQRImage(string value)
        {
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(value ?? string.Empty, QRCodeGenerator.ECCLevel.Q))
            {
                var qrCode = new PngByteQRCode(data);
                return qrCode.GetGraphic(20);
            }
        }
    }
}
