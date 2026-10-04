namespace MhmsMobileApp.Models
{
    public class LoginRequestDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // Returned by /api/auth/login and /api/auth/me
    public class CurrentUserDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; }

        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int? ParentId { get; set; }
        public int? StudentId { get; set; }
        public bool MustConfirmSafety { get; set; }
    }
}
