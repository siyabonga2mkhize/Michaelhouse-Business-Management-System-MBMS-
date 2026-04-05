select * from SchoolClasses

select * from appusers

Select * from ClassSubjects

select * from Subjects;


INSERT INTO Subjects (SubjectName, SubjectCode, Credits, GradeLevel)
VALUES 
('Information Technology', 'ICT301', 12, 12),
('Computer Science', 'CS102', 12, 12),
('Mathematics', 'MATH401', 10, 12),
('Physical Science', 'PHYS202', 10, 12),
('English First Language', 'ENG101', 8, 12),
('Business Studies', 'BUS301', 8, 12);

-- Verify the subjects are in the table
SELECT * FROM Subjects;


SELECT ClassId, ClassName, GradeLevel FROM SchoolClasses;
SELECT StudentId, FirstName, LastName, GradeLevel, ClassId FROM Students;

UPDATE Students 
SET ClassId = 1
WHERE GradeLevel = 8 AND StudentId = 1;


-- Create a Teacher
Select * from Teachers;


INSERT INTO Teachers (FirstName, LastName, Email, Phone, EmployeeNumber, Department, Specialization, HireDate)
VALUES ('John', 'Staff', 'j.staff@michaelhouse.org', '0331234567', 'MH1001', 'ICT', 'App Dev', GETDATE());

-- Get the ID of the teacher you just made (likely 1)
-- Get the ID of the class you want them to teach (likely 1)

-- Link them
UPDATE SchoolClasses SET TeacherId = 1 WHERE ClassId = 1;

-- 1. Create the AppUser (The Login)
INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
VALUES ('John Staff', 'teacher@michaelhouse.org', '7v6S/u/Jq6iH7vXoM/XG8S8A0I7z8=', 'Teacher');
-- Teacher@123

Update AppUsers 
set PasswordHash = '7v6S/u/Jq6iH7vXoM/XG8S8A0I7z8='
where UserId = 1002;

-- 2. Get the New UserId and the Existing TeacherId
-- Let's assume the new UserId is 10 and your TeacherId is 1.

-- 3. Link them together
UPDATE Teachers 
SET UserId = (SELECT TOP 1 UserId FROM AppUsers WHERE Email = 'teacher@michaelhouse.org')
WHERE TeacherId = 1;

Delete from AppUsers where UserId = 1002;

Select * from Teachers