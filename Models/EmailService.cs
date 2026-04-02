using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;


namespace Michaelhouse.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _baseUrl;
        private readonly bool _enabled;

        public EmailService()
        {
            _smtpHost = ConfigurationManager.AppSettings["Email:SmtpHost"] ?? "";
            _smtpPort = int.TryParse(ConfigurationManager.AppSettings["Email:SmtpPort"], out int p) ? p : 587;
            _smtpUser = ConfigurationManager.AppSettings["Email:Username"] ?? "";
            _smtpPass = ConfigurationManager.AppSettings["Email:Password"] ?? "";
            _fromEmail = ConfigurationManager.AppSettings["Email:FromAddress"] ?? "";
            _fromName = ConfigurationManager.AppSettings["Email:FromName"] ?? "Michaelhouse Admissions";
            _baseUrl = ConfigurationManager.AppSettings["App:BaseUrl"] ?? "#";
            _enabled = !string.IsNullOrEmpty(_smtpHost) && !string.IsNullOrEmpty(_smtpUser);
        }

        // ─── Application Approved — parent must complete registration ─────────────

        public void SendApplicationApproved(
            string parentEmail, string parentName,
            string studentName, int grade)
        {
            var subject = $"🎉 Application Approved — Please Complete {studentName}'s Registration";

            var body = $@"
<html><body style='font-family:Segoe UI,sans-serif;color:#333;max-width:600px;margin:0 auto;'>
    <div style='background:#1a3c5e;padding:24px 32px;'>
        <h2 style='color:#fff;margin:0;'>Michaelhouse</h2>
        <p style='color:rgba(255,255,255,0.7);margin:4px 0 0;'>Enrollment Management System</p>
    </div>
    <div style='padding:32px;background:#fff;'>
        <div style='background:#e8f5e9;border-left:4px solid #2e7d32;padding:16px;border-radius:4px;margin-bottom:24px;'>
            <h3 style='margin:0 0 8px;color:#1b5e20;'>✅ Application Approved!</h3>
            <p style='margin:0;color:#2e7d32;'><strong>{studentName}</strong>'s application for Grade {grade} has been approved.</p>
        </div>
        <p>Dear {parentName},</p>
        <p>We are pleased to inform you that <strong>{studentName}</strong>'s enrollment application has been approved.</p>
        <p><strong>The next step is to complete the registration process.</strong> Please log in to the system to:</p>
        <ul>
            <li>Confirm student details</li>
            {(grade >= 10 ? "<li>Select subjects of choice</li>" : "<li>Review the predetermined subject list</li>")}
            <li>Submit the registration</li>
        </ul>
        <p>Once you complete registration, a student account will be created automatically and login credentials will be sent to you.</p>
        <div style='text-align:center;margin:32px 0;'>
            <a href='{_baseUrl}/Account/Login'
               style='background:#1a3c5e;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;'>
                Log In to Complete Registration
            </a>
        </div>
    </div>
    <div style='background:#f4f6f9;padding:16px 32px;text-align:center;color:#888;font-size:0.8rem;'>
        Michaelhouse Enrollment System — This is an automated message.
    </div>
</body></html>";

            Send(parentEmail, subject, body);
        }

        // ─── Application Flagged ──────────────────────────────────────────────────

        public void SendApplicationFlagged(
            string parentEmail, string parentName,
            string studentName, int appId, string adminNotes)
        {
            var subject = $"Action Required — {studentName}'s Application Has Been Flagged";

            var body = $@"
<html><body style='font-family:Segoe UI,sans-serif;color:#333;max-width:600px;margin:0 auto;'>
    <div style='background:#1a3c5e;padding:24px 32px;'>
        <h2 style='color:#fff;margin:0;'>Michaelhouse</h2>
        <p style='color:rgba(255,255,255,0.7);margin:4px 0 0;'>Enrollment Management System</p>
    </div>
    <div style='padding:32px;background:#fff;'>
        <div style='background:#fff8e1;border-left:4px solid #e8a020;padding:16px;border-radius:4px;margin-bottom:24px;'>
            <h3 style='margin:0 0 8px;color:#b35c00;'>⚠ Application Flagged — Action Required</h3>
            <p style='margin:0;color:#5a4000;'>Application #{appId} for <strong>{studentName}</strong> requires your attention.</p>
        </div>
        <p>Dear {parentName},</p>
        <p>Your application has been reviewed and flagged. Please log in to resubmit the required documents.</p>
        {(!string.IsNullOrEmpty(adminNotes) ? $"<div style='background:#f8f9fa;padding:16px;border-radius:8px;margin:20px 0;'><p style='margin:0;font-weight:600;'>Message from Admissions:</p><p style='margin:8px 0 0;'>{adminNotes}</p></div>" : "")}
        <div style='text-align:center;margin:32px 0;'>
            <a href='{_baseUrl}/Account/Login'
               style='background:#1a3c5e;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:600;'>
                Log In to Resubmit
            </a>
        </div>
    </div>
    <div style='background:#f4f6f9;padding:16px 32px;text-align:center;color:#888;font-size:0.8rem;'>
        Michaelhouse Enrollment System — This is an automated message.
    </div>
</body></html>";

            Send(parentEmail, subject, body);
        }

        // ─── Student Account Created ──────────────────────────────────────────────

        public void SendStudentAccountCreated(
            string parentEmail, string parentName,
            string studentName, string studentEmail,
            string tempPassword, int grade)
        {
            var subject = $"Student Account Created — {studentName}'s Login Details";

            var body = $@"
<html><body style='font-family:Segoe UI,sans-serif;color:#333;max-width:600px;margin:0 auto;'>
    <div style='background:#1a3c5e;padding:24px 32px;'>
        <h2 style='color:#fff;margin:0;'>Michaelhouse</h2>
    </div>
    <div style='padding:32px;background:#fff;'>
        <p>Dear {parentName},</p>
        <p>Registration for <strong>{studentName}</strong> (Grade {grade}) has been completed successfully. A student account has been created.</p>
        <div style='background:#f8f9fa;padding:20px;border-radius:8px;margin:24px 0;border:1px solid #e0e0e0;'>
            <p style='margin:0 0 12px;font-weight:600;color:#1a3c5e;'>Student Login Credentials</p>
            <table style='width:100%;'>
                <tr><td style='padding:6px 0;color:#666;width:40%;'>Email</td><td style='font-weight:600;'>{studentEmail}</td></tr>
                <tr><td style='padding:6px 0;color:#666;'>Temporary Password</td><td style='font-weight:600;font-family:monospace;font-size:1.1rem;'>{tempPassword}</td></tr>
            </table>
        </div>
        <div style='background:#fff3e0;padding:12px 16px;border-radius:6px;'>
            <p style='margin:0;font-size:0.9rem;color:#e65100;'><strong>Important:</strong> Please log in and change the temporary password immediately.</p>
        </div>
    </div>
    <div style='background:#f4f6f9;padding:16px 32px;text-align:center;color:#888;font-size:0.8rem;'>
        Michaelhouse Enrollment System — This is an automated message.
    </div>
</body></html>";

            Send(parentEmail, subject, body);
        }

        // ─── Core Send ────────────────────────────────────────────────────────────

        private void Send(string toEmail, string subject, string htmlBody)
        {
            if (!_enabled)
            {
                System.Diagnostics.Debug.WriteLine($"[EMAIL NOT SENT — SMTP not configured]");
                System.Diagnostics.Debug.WriteLine($"To: {toEmail} | Subject: {subject}");
                return;
            }

            try
            {
                using (var client = new SmtpClient(_smtpHost, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);

                    var msg = new MailMessage
                    {
                        From = new MailAddress(_fromEmail, _fromName),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };
                    msg.To.Add(toEmail);
                    client.Send(msg);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email failed: {ex.Message}");
            }
        }
    }
}