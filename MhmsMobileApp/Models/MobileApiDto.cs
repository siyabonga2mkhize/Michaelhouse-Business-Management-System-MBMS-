using System;
using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // Web API endpoints (api/mobile/*, MobileApiController) — data
    // classes from Michaelhouse.Mobile, Increment 1 & 2 only.
    // ============================================================

    // A Web API reply: Data is the server's object/list (null when the
    // server answered null, e.g. "no active emergency")
    public class MobileResult<T>
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public T? Data { get; set; }
    }

    // GET student/alerts
    public class StudentAlertDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string? Type { get; set; }
    }

    // GET student/emergency (null when there's no active alert)
    public class EmergencyAlertDto
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public string AssemblyPoint { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusMeters { get; set; }
        public bool SirenStopped { get; set; }
    }

    // POST student/emergency/confirm { latitude, longitude }
    public class EmergencyConfirmationDto
    {
        public bool Success { get; set; }
        public bool WithinGeofence { get; set; }
        public double Distance { get; set; }
        public string Message { get; set; } = "";
    }

    // GET housemaster/alerts
    public class HouseMasterAlertDto
    {
        public int Id { get; set; }
        public string Residence { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    // GET housemaster/residences
    public class HouseMasterResidenceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // GET housemaster/residences/{id}/rollcall
    public class RollCallStudentDto
    {
        public int StudentId { get; set; }
        public string Name { get; set; } = "";
    }

    // GET students/{id}/qr
    public class StudentQrDto
    {
        public int StudentId { get; set; }
        public string Value { get; set; } = "";
        public string ImageBase64 { get; set; } = "";
    }

    // POST qr/scan { qrValue }
    public class QrScanResultDto
    {
        public bool Approved { get; set; }
        public string? Reason { get; set; }
    }

    // POST ... { success = true }
    public class SuccessDto
    {
        public bool Success { get; set; }
    }
}
