using Michaelhouse.Models;
using System.Data.Entity;

namespace Michaelhouse.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Your context has been configured to use a 'ApplicationDbContext' connection string from your application's 
        // configuration file (App.config or Web.config). By default, this connection string targets the 
        // 'Michaelhouse.Data.ApplicationDbContext' database on your LocalDb instance. 
        // 
        // If you wish to target a different database and/or database provider, modify the 'ApplicationDbContext' 
        // connection string in the application configuration file.
        public ApplicationDbContext()
            : base("name=ApplicationDbContext")
        {
        }
        public DbSet<Student> Students { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<TeacherAttendance> TeacherAttendances { get; set; }

        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Parent> Parents { get; set; }



        // Add a DbSet for each entity type that you want to include in your model. For more information 
        // on configuring and using a Code First model, see http://go.microsoft.com/fwlink/?LinkId=390109.

        // public virtual DbSet<MyEntity> MyEntities { get; set; }
    }

    //public class MyEntity
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //}
}