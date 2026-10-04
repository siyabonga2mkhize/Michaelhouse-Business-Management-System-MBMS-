namespace Michaelhouse.Mobile.Models;
public sealed class LoginResponse { public string Name { get; set; } = ""; public string? Role { get; set; } public string? Token { get; set; } }
public sealed class StudentDto { public int StudentId { get; set; } public string Name { get; set; } = ""; public string? StudentNumber { get; set; } public string? Grade { get; set; } public string? MedicalConditions { get; set; } }
public sealed class TimetableSlotDto { public int DayOfWeek { get; set; } public string Period { get; set; } = ""; public string StartTime { get; set; } = ""; public string EndTime { get; set; } = ""; public string Subject { get; set; } = ""; public string Teacher { get; set; } = ""; }
public sealed class ResultDto { public int Term { get; set; } public string Subject { get; set; } = ""; public decimal Percentage { get; set; } public string Symbol { get; set; } = ""; public bool Passed { get; set; } }
public sealed class AttendanceDto { public List<AttendanceSummaryDto> Summary { get; set; } = []; public List<AttendanceRecordDto> Records { get; set; } = []; }
public sealed class AttendanceSummaryDto { public string Subject { get; set; } = ""; public int TotalClasses { get; set; } public int Present { get; set; } public int Late { get; set; } public int Absent { get; set; } }
public sealed class AttendanceRecordDto { public DateTime Date { get; set; } public string Subject { get; set; } = ""; public string Status { get; set; } = ""; }
public sealed class AlertDto { public int Id { get; set; } public string Message { get; set; } = ""; public DateTime CreatedAt { get; set; } public bool IsRead { get; set; } public string? Type { get; set; } }
public sealed class TeacherSessionDto { public int SubjectId { get; set; } public string Subject { get; set; } = ""; public int Grade { get; set; } public string Stream { get; set; } = ""; }
public sealed class TeacherRegisterDto { public DateTime Date { get; set; } public List<TeacherRegisterStudentDto> Students { get; set; } = []; }
public sealed class TeacherRegisterStudentDto { public int StudentId { get; set; } public string Name { get; set; } = ""; public string Status { get; set; } = "Present"; }
public sealed class DriverTripDto { public int Id { get; set; } public string Title { get; set; } = ""; public DateTime ScheduledDate { get; set; } public string Status { get; set; } = ""; public int Students { get; set; } }
public sealed class DriverManifestDto { public int Id { get; set; } public string Title { get; set; } = ""; public DateTime ScheduledDate { get; set; } public List<DriverManifestStudentDto> Students { get; set; } = []; }
public sealed class DriverManifestStudentDto { public int StudentId { get; set; } public string Name { get; set; } = ""; public bool Boarded { get; set; } public bool Returned { get; set; } }
public sealed class DriverScanResultDto { public bool Success { get; set; } public string Message { get; set; } = ""; public string StudentName { get; set; } = ""; }
public sealed class HouseMasterAlertDto { public int Id { get; set; } public string Residence { get; set; } = ""; public string Title { get; set; } = ""; public string Message { get; set; } = ""; public DateTime CreatedAt { get; set; } }
public sealed class HouseMasterResidenceDto { public int Id { get; set; } public string Name { get; set; } = ""; }
public sealed class RollCallStudentDto { public int StudentId { get; set; } public string Name { get; set; } = ""; }
public sealed class EmergencyAlertDto { public int Id { get; set; } public string Message { get; set; } = ""; public string AssemblyPoint { get; set; } = ""; public double Latitude { get; set; } public double Longitude { get; set; } public double RadiusMeters { get; set; } public bool SirenStopped { get; set; } }
public sealed class EmergencyConfirmationDto { public bool Success { get; set; } public bool WithinGeofence { get; set; } public double Distance { get; set; } public string Message { get; set; } = ""; }
public sealed class ResidenceDto { public int ResidenceId { get; set; } public string Name { get; set; } = ""; public int AvailableBeds { get; set; } }
public sealed class AssignmentDto { public ResidenceDto? Residence { get; set; } public RoomDto? Room { get; set; } public BedDto? Bed { get; set; } } public sealed class RoomDto { public string RoomNumber { get; set; } = ""; } public sealed class BedDto { public string BedNumber { get; set; } = ""; }
