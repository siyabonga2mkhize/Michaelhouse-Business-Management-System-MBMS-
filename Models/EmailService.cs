using Michaelhouse.Infrastructure;
﻿using Michaelhouse.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using static System.Net.WebRequestMethods;


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
            _smtpHost = AppConfig.AppSettings("Email:SmtpHost") ?? "";
            _smtpPort = int.TryParse(AppConfig.AppSettings("Email:SmtpPort"), out int p) ? p : 587;
            _smtpUser = AppConfig.AppSettings("Email:Username") ?? "";
            _smtpPass = AppConfig.AppSettings("Email:Password") ?? "";
            _fromEmail = AppConfig.AppSettings("Email:FromAddress") ?? "";
            _fromName = AppConfig.AppSettings("Email:FromName") ?? "Michaelhouse Admissions";
            _baseUrl = AppConfig.AppSettings("App:BaseUrl") ?? "#";
            _enabled = !string.IsNullOrEmpty(_smtpHost) && !string.IsNullOrEmpty(_smtpUser);
        }

        // Public wrapper to send arbitrary HTML emails. Uses internal configuration to decide whether to actually deliver.
        public void SendPlain(string toEmail, string subject, string htmlBody)
        {
            Send(toEmail, subject, htmlBody);
        }

        // ─── Application Approved — parent must complete registration ─────────────

        public void SendApplicationApproved(
    string parentEmail, string parentName,
    string studentName, int grade)
        {
            var subject = $"OFFICIAL NOTICE: Enrollment Approved — {studentName} (Grade {grade})";

            // Path to your hosted logo - ensure this is a transparent PNG or white-on-dark version
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>Enrollment Management Division</p>
        </div>

        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {parentName},</h2>
            
            <p style='font-size:16px; margin-bottom:25px;'>It is our distinct pleasure to inform you that the application for <strong>{studentName}</strong> has been formally <strong>Approved</strong> for entry into Grade {grade}.</p>
            
            <div style='margin:40px 0; padding:30px; border-left:4px solid #C21E2E; background-color:#fafafa;'>
                <h4 style='margin:0 0 12px; text-transform:uppercase; letter-spacing:2px; font-size:11px; color:#1a1a1a; font-weight:bold;'>Registration Protocol</h4>
                <p style='margin:0; font-size:14px; color:#555;'>To finalize this placement, you are now required to access the digital ledger and complete the following:</p>
                <ul style='margin:15px 0 0; padding-left:20px; font-size:13px; color:#444;'>
                    <li style='margin-bottom:10px;'>Verification of core student data and legal documentation.</li>
                    {(grade >= 10
                                ? "<li style='margin-bottom:10px;'><strong>Academic Stream Selection:</strong> Configuration of Senior Phase elective modules.</li>"
                                : "<li style='margin-bottom:10px;'>Final review of the Grade " + grade + " prescribed curriculum.</li>")}
                    <li>Authentication and submission of the Institutional Registration.</li>
                </ul>
            </div>

            <p style='font-size:14px; color:#666;'>Once this record is formalized, the student's institutional identity will be provisioned, and secure access credentials will be dispatched to this address.</p>

            <div style='text-align:center; margin-top:50px;'>
                <a href='{_baseUrl}/Account/Login'
                   style='background-color:#1a1a1a; color:#ffffff; padding:20px 45px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block; border:1px solid #1a1a1a;'>
                    Log In to Registration Ledger
                </a>
            </div>
        </div>

        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Balgowan &nbsp;·&nbsp; KwaZulu-Natal
            </p>
            <p style='margin:15px 0 0; font-size:8px; color:#cccccc; text-transform:uppercase; letter-spacing:1px;'>
                Secure Communication &nbsp;·&nbsp; Ref: MHS-ENR-{DateTime.Now.Year}
            </p>
        </div>
    </div>
</body>
</html>";

            Send(parentEmail, subject, body);
        }
        // ─── Application Flagged ──────────────────────────────────────────────────

        public void SendApplicationFlagged(
    string parentEmail, string parentName,
    string studentName, int appId, string adminNotes)
        {
            var subject = $"URGENT: Application Action Required — {studentName} (Ref: #{appId})";
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>Enrollment Management Division</p>
        </div>

        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {parentName},</h2>
            
            <p style='font-size:16px; margin-bottom:25px;'>The enrollment application for <strong>{studentName}</strong> (Dossier #{appId}) has been reviewed by our admissions office and requires <strong>immediate attention</strong>.</p>
            
            <div style='margin:40px 0; padding:30px; border-left:4px solid #C21E2E; background-color:#fff5f5;'>
                <h4 style='margin:0 0 12px; text-transform:uppercase; letter-spacing:2px; font-size:11px; color:#C21E2E; font-weight:bold;'>Admissions Query / Flag</h4>
                <p style='margin:0; font-size:14px; color:#1a1a1a;'>An administrative flag has been placed on the record for the following reason:</p>
                
                <div style='margin-top:15px; padding:15px; background-color:#ffffff; border:1px solid #ebdada; font-style:italic; font-size:13px; color:#444;'>
                    ""{(string.IsNullOrEmpty(adminNotes) ? "Please review your submitted documentation for errors or missing attachments." : adminNotes)}""
                </div>
            </div>

            <p style='font-size:14px; color:#666;'>To proceed with the enrollment process, please log in to the Parent Portal to resubmit the flagged documentation or provide the requested information.</p>

            <div style='text-align:center; margin-top:50px;'>
                <a href='{_baseUrl}/Account/Login'
                   style='background-color:#C21E2E; color:#ffffff; padding:20px 45px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block; border:1px solid #C21E2E;'>
                    Access Application Dossier
                </a>
            </div>
        </div>

        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Balgowan &nbsp;·&nbsp; KwaZulu-Natal
            </p>
            <p style='margin:15px 0 0; font-size:8px; color:#cccccc; text-transform:uppercase; letter-spacing:1px;'>
                Secure Communication &nbsp;·&nbsp; Case Ref: FLAG-{appId}-{DateTime.Now.Year}
            </p>
        </div>
    </div>
</body>
</html>";

            Send(parentEmail, subject, body);
        }

        // ─── Student Account Created ──────────────────────────────────────────────

        public void SendStudentAccountCreated(
    string parentEmail, string parentName,
    string studentName, string studentEmail,
    string tempPassword, int grade)
        {
            var subject = $"OFFICIAL NOTICE: Student Identity Provisioned — {studentName} (Grade {grade})";
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
    <link href='https://fonts.googleapis.com/css2?family=Source+Code+Pro:wght@600&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>Information Technology Division</p>
        </div>

        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {parentName},</h2>
            
            <p style='font-size:16px; margin-bottom:25px;'>Registration for <strong>{studentName}</strong> (Grade {grade}) has been finalized. An institutional identity has been provisioned within the Michaelhouse digital network.</p>
            
            <div style='margin:40px 0; border:1px solid #1a1a1a; background-color:#ffffff; position:relative;'>
                <div style='background-color:#1a1a1a; color:#ffffff; padding:10px 20px; font-size:10px; font-weight:bold; letter-spacing:2px; text-transform:uppercase;'>
                    Access Credentials
                </div>
                <div style='padding:30px;'>
                    <table style='width:100%; border-collapse:collapse;'>
                        <tr>
                            <td style='padding:10px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase; width:40%;'>Institutional ID</td>
                            <td style='padding:10px 0; font-size:14px; color:#1a1a1a; font-weight:bold;'>{studentEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase;'>Access Key</td>
                            <td style='padding:10px 0; font-size:16px; color:#C21E2E; font-family:""Source Code Pro"", monospace; font-weight:bold;'>{tempPassword}</td>
                        </tr>
                    </table>
                </div>
            </div>

            <div style='background-color:#fafafa; border-left:4px solid #1a1a1a; padding:20px; margin-bottom:40px;'>
                <p style='margin:0; font-size:12px; color:#555;'><strong>Security Protocol:</strong> For the protection of student data, this temporary access key must be rotated upon the first successful authentication.</p>
            </div>

            <div style='text-align:center;'>
                <a href='{_baseUrl}/Account/Login'
                   style='background-color:#1a1a1a; color:#ffffff; padding:20px 45px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block;'>
                    Initialize Login Sequence
                </a>
            </div>
        </div>

        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Information Systems &nbsp;·&nbsp; KZN
            </p>
            <p style='margin:15px 0 0; font-size:8px; color:#cccccc; text-transform:uppercase; letter-spacing:1px;'>
                Secure Provisioning Notice &nbsp;·&nbsp; Ref: SYS-ID-{DateTime.Now.Ticks.ToString().Substring(10)}
            </p>
        </div>
    </div>
</body>
</html>";

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

        public void SendApplicationApprovedWithPayment(
     string parentEmail, string parentName,
     string studentName, int grade,
     string invoiceNumber, decimal registrationFee)
        {
            var subject = $"FINANCIAL NOTICE: Enrollment Approved — Payment Required: {studentName}";
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>Bursary & Enrollment Division</p>
        </div>

        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {parentName},</h2>
            
            <p style='font-size:16px; margin-bottom:25px;'>We are pleased to offer <strong>{studentName}</strong> a place at Michaelhouse for Grade {grade}.</p>
            
            <div style='margin:40px 0; border:1px solid #1a1a1a; background-color:#ffffff; position:relative;'>
                <div style='background-color:#1a1a1a; color:#ffffff; padding:12px 20px; font-size:10px; font-weight:bold; letter-spacing:2px; text-transform:uppercase;'>
                    Securing Placement — Action Required
                </div>
                <div style='padding:30px; background-color:#fafafa;'>
                    <p style='margin:0 0 20px; font-size:13px; color:#666;'>To formally secure this position, the non-refundable registration fee must be settled through the financial ledger.</p>
                    <table style='width:100%; border-collapse:collapse;'>
                        <tr style='border-bottom:1px solid #eee;'>
                            <td style='padding:12px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase;'>Invoice Number</td>
                            <td style='padding:12px 0; font-size:14px; color:#1a1a1a; font-weight:bold; text-align:right;'>{invoiceNumber}</td>
                        </tr>
                        <tr>
                            <td style='padding:12px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase;'>Amount Due</td>
                            <td style='padding:12px 0; font-size:22px; color:#C21E2E; font-family:""Playfair Display"", serif; font-weight:bold; text-align:right;'>ZAR {registrationFee:N2}</td>
                        </tr>
                    </table>
                </div>
            </div>

            <div style='text-align:center; margin-bottom:50px;'>
                <a href='{_baseUrl}/Account/Login'
                   style='background-color:#1a1a1a; color:#ffffff; padding:20px 45px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block; border:1px solid #1a1a1a;'>
                    Settle Account Ledger
                </a>
            </div>

            <div style='border-top:2px solid #1a1a1a; pt-30;'>
                <h4 style='font-family:""Playfair Display"", serif; font-size:18px; color:#1a1a1a; margin:30px 0 15px;'>Institutional Fee Structure — 2026</h4>
                <table style='width:100%; border-collapse:collapse; font-size:12px;'>
                    <tr style='border-bottom:1px solid #f0f0f0;'>
                        <td style='padding:10px 0; color:#666;'>Registration Fee (Non-Refundable)</td>
                        <td style='padding:10px 0; font-weight:bold; text-align:right; color:#1a1a1a;'>ZAR 950</td>
                    </tr>
                    <tr style='border-bottom:1px solid #f0f0f0;'>
                        <td style='padding:10px 0; color:#666;'>Annual Board & Tuition (E to A Block)</td>
                        <td style='padding:10px 0; font-weight:bold; text-align:right; color:#1a1a1a;'>ZAR 417,000</td>
                    </tr>
                    <tr style='border-bottom:1px solid #f0f0f0;'>
                        <td style='padding:10px 0; color:#666;'>Voluntary Development Levy</td>
                        <td style='padding:10px 0; font-weight:bold; text-align:right; color:#1a1a1a;'>ZAR 6,800</td>
                    </tr>
                    <tr>
                        <td style='padding:10px 0; color:#C21E2E; font-weight:bold;'>Advance Payment Rebate (Annual)</td>
                        <td style='padding:10px 0; font-weight:bold; text-align:right; color:#C21E2E;'>- ZAR 16,680</td>
                    </tr>
                </table>
            </div>
        </div>

        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Balgowan &nbsp;·&nbsp; KwaZulu-Natal
            </p>
            <p style='margin:15px 0 0; font-size:8px; color:#cccccc; text-transform:uppercase; letter-spacing:1px;'>
                Official Financial Communication &nbsp;·&nbsp; Ref: MHS-FIN-{DateTime.Now.Year}-{invoiceNumber}
            </p>
        </div>
    </div>
</body>
</html>";

            Send(parentEmail, subject, body);
        }
        public void SendTeacherAccountCreated(
     string teacherEmail, string teacherName,
     string loginEmail, string tempPassword,
     System.Collections.Generic.List<string> subjectAssignments)
        {
            var subject = $"OFFICIAL NOTICE: Faculty Provisioning Complete — {teacherName}";
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var assignmentRows = string.Join("",
                subjectAssignments.Select(s =>
                    $"<tr style='border-bottom:1px solid #f9f9f9;'><td style='padding:12px 0; font-size:13px; color:#1a1a1a;'><span style='color:#C21E2E; margin-right:10px;'>&bull;</span> {s}</td></tr>"));

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
    <link href='https://fonts.googleapis.com/css2?family=Source+Code+Pro:wght@600&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>Faculty Registry & Human Capital</p>
        </div>

        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {teacherName},</h2>
            
            <p style='font-size:16px; margin-bottom:25px;'>Your faculty profile has been successfully provisioned within the Michaelhouse digital ledger. Your institutional access credentials have been initialized.</p>
            
            <div style='margin:40px 0; border:1px solid #1a1a1a; background-color:#ffffff;'>
                <div style='background-color:#1a1a1a; color:#ffffff; padding:10px 20px; font-size:10px; font-weight:bold; letter-spacing:2px; text-transform:uppercase;'>
                    Staff Access Credentials
                </div>
                <div style='padding:30px; background-color:#fafafa;'>
                    <table style='width:100%; border-collapse:collapse;'>
                        <tr style='border-bottom:1px solid #eee;'>
                            <td style='padding:10px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase; width:40%;'>Official ID</td>
                            <td style='padding:10px 0; font-size:14px; color:#1a1a1a; font-weight:bold;'>{loginEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 0; font-size:11px; font-weight:bold; color:#999; text-transform:uppercase;'>Temporary Key</td>
                            <td style='padding:10px 0; font-size:16px; color:#C21E2E; font-family:""Source Code Pro"", monospace; font-weight:bold;'>{tempPassword}</td>
                        </tr>
                    </table>
                </div>
            </div>

            <div style='margin-bottom:40px;'>
                <h4 style='font-family:""Playfair Display"", serif; font-size:16px; color:#1a1a1a; border-bottom:2px solid #C21E2E; padding-bottom:8px; margin-bottom:15px;'>Current Subject Allocations</h4>
                <table style='width:100%; border-collapse:collapse;'>
                    {assignmentRows}
                </table>
            </div>

            <div style='background-color:#f9f9f9; border-left:4px solid #1a1a1a; padding:20px; margin-bottom:40px;'>
                <p style='margin:0; font-size:11px; color:#555; text-transform:uppercase; letter-spacing:1px;'><strong>Security Requirement:</strong> This temporary key must be rotated upon first authentication to secure your staff record.</p>
            </div>

            <div style='text-align:center;'>
                <a href='{_baseUrl}/Account/Login'
                   style='background-color:#1a1a1a; color:#ffffff; padding:20px 45px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block;'>
                    Access Staff Portal
                </a>
            </div>
        </div>

        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>
                Michaelhouse &nbsp;·&nbsp; Academic Administration &nbsp;·&nbsp; Balgowan
            </p>
            <p style='margin:15px 0 0; font-size:8px; color:#cccccc; text-transform:uppercase; letter-spacing:1px;'>
                Institutional Provisioning Notice &nbsp;·&nbsp; Case Ref: FAC-{DateTime.Now.Ticks.ToString().Substring(10)}
            </p>
        </div>
    </div>
</body>
</html>";

            Send(teacherEmail, subject, body);
        }

        // ========== NEW: School Store Order Confirmation ==========
        public void SendOrderConfirmation(string customerEmail, string customerName, string orderNumber, double totalAmount, List<OrderItem> items)
        {
            var subject = $"Order Confirmation - Michaelhouse School Store (#{orderNumber})";
            string logoUrl = $"https://i.postimg.cc/Ss8DWBVf/Content/logo.svg.png";

            var itemsHtml = string.Join("", items.Select(item => $@"
                <tr style='border-bottom:1px solid #eeeeee;'>
                    <td style='padding:12px 5px;'>{item.Product.Name}</td>
                    <td style='padding:12px 5px; text-align:center;'>{item.Quantity}</td>
                    <td style='padding:12px 5px; text-align:right;'>R {item.UnitPrice:N2}</td>
                    <td style='padding:12px 5px; text-align:right;'>R {item.Subtotal:N2}</td>
                </tr>"));

            var body = $@"
<html>
<head>
    <link href='https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&display=swap' rel='stylesheet'>
</head>
<body style='font-family:sans-serif; background-color:#f4f4f4; margin:0; padding:40px 0;'>
    <div style='max-width:600px; margin:0 auto; background-color:#ffffff; border:1px solid #dddddd; box-shadow:0 15px 40px rgba(0,0,0,0.05);'>
        <div style='background-color:#1a1a1a; padding:50px 40px; border-bottom:5px solid #C21E2E; text-align:center;'>
            <img src='{logoUrl}' alt='Michaelhouse' style='height:80px; width:auto; margin-bottom:20px; display:inline-block;' />
            <h1 style='color:#ffffff; margin:0; font-family:""Playfair Display"", serif; letter-spacing:5px; text-transform:uppercase; font-size:22px;'>Michaelhouse</h1>
            <div style='height:1px; width:40px; background-color:#C21E2E; margin:15px auto;'></div>
            <p style='color:#999999; margin:0; letter-spacing:3px; text-transform:uppercase; font-size:9px; font-weight:bold;'>School Store</p>
        </div>
        <div style='padding:60px 50px; color:#333333; line-height:1.8;'>
            <h2 style='font-family:""Playfair Display"", serif; font-size:30px; color:#1a1a1a; margin-top:0; margin-bottom:25px;'>Dear {customerName},</h2>
            <p style='font-size:16px; margin-bottom:25px;'>Thank you for shopping at the Michaelhouse School Store. Your order <strong>#{orderNumber}</strong> has been received and is now being processed.</p>
            <div style='margin:40px 0;'>
                <h4 style='margin:0 0 15px; text-transform:uppercase; letter-spacing:2px; font-size:11px; color:#1a1a1a; border-bottom:2px solid #C21E2E; display:inline-block;'>Order Summary</h4>
                <table style='width:100%; border-collapse:collapse; margin-top:15px;'>
                    <thead>
                        <tr style='background-color:#f5f5f5;'>
                            <th style='padding:10px 5px; text-align:left;'>Product</th>
                            <th style='padding:10px 5px; text-align:center;'>Qty</th>
                            <th style='padding:10px 5px; text-align:right;'>Unit Price</th>
                            <th style='padding:10px 5px; text-align:right;'>Subtotal</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                    </tbody>
                    <tfoot>
                        <tr style='border-top:2px solid #1a1a1a;'>
                            <td colspan='3' style='padding:15px 5px; text-align:right; font-weight:bold;'>Total</td>
                            <td style='padding:15px 5px; text-align:right; font-weight:bold; color:#C21E2E;'>R {totalAmount:N2}</td>
                        </tr>
                    </tfoot>
                </table>
            </div>
            <p style='font-size:14px; color:#666;'>You can track the status of your order by visiting the <a href='{_baseUrl}/Order/TrackOrder' style='color:#C21E2E;'>Track Order</a> page and entering your order number.</p>
            <div style='text-align:center; margin-top:50px;'>
                <a href='{_baseUrl}/Order/TrackOrder' style='background-color:#1a1a1a; color:#ffffff; padding:15px 30px; text-decoration:none; font-size:11px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; display:inline-block;'>Track Your Order</a>
            </div>
        </div>
        <div style='background-color:#fafafa; padding:40px; text-align:center; border-top:1px solid #eeeeee;'>
            <p style='margin:0; font-size:10px; color:#999999; letter-spacing:1px; text-transform:uppercase;'>Michaelhouse School Store &nbsp;·&nbsp; Balgowan</p>
        </div>
    </div>
</body>
</html>";

            Send(customerEmail, subject, body);
        }
    }
}


//Add email after placing the order 
//Send email to the user with the order details and a link to view the order 
