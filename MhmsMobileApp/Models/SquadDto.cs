using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    public class SquadListDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public List<SquadSportDto> Sports { get; set; }

        public SquadListDto()
        {
            Sports = new List<SquadSportDto>();
        }
    }

    public class SquadSportDto
    {
        public string SportName { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public List<SquadStudentDto> Students { get; set; }

        public SquadSportDto()
        {
            Students = new List<SquadStudentDto>();
        }
    }

    public class SquadStudentDto
    {
        public int StatusId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentNumber { get; set; }
        public bool IsActive { get; set; }
        public string StatusReason { get; set; }
        public string UnavailableUntil { get; set; }
    }

    public class MarkRequestDto
    {
        public int StatusId { get; set; }
        public bool IsActive { get; set; }
        public string Reason { get; set; }
        public string UnavailableUntil { get; set; }
    }

    public class MarkResponseDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public int StatusId { get; set; }
        public bool IsActive { get; set; }
    }
}