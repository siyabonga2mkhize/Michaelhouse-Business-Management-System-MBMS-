using Michaelhouse.Models;
using System;
using System.Linq;

namespace Michaelhouse.Services
{
    public class StudentNumberService
    {
        private readonly DBContextClass db;

        public StudentNumberService(DBContextClass context)
        {
            db = context;
        }

        public string GenerateStudentNumber()
        {
            int year = DateTime.Now.Year;
            string prefix = $"BDS{year}-";

            var existingNumbers = db.Students
                .Where(s => s.StudentNumber != null &&
                            s.StudentNumber.StartsWith(prefix))
                .Select(s => s.StudentNumber)
                .ToList();

            int highestNumber = 0;

            foreach (var number in existingNumbers)
            {
                string suffix = number.Substring(prefix.Length);

                int parsed;
                if (int.TryParse(suffix, out parsed))
                {
                    if (parsed > highestNumber)
                        highestNumber = parsed;
                }
            }

            int nextNumber = highestNumber + 1;

            return $"{prefix}{nextNumber:D5}";
        }
    }
}