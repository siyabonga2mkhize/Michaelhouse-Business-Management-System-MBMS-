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


Select * from  TeacherSubjectGrades

UPDATE Teachers SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'j.staff@michaelhouse.org')
WHERE Email = 'j.staff@michaelhouse.org';


-- Assign three grade 12 subjects (replace SubjectIds with actual IDs)
INSERT INTO TeacherSubjectGrades (TeacherId, SubjectId, Grade, Stream)
VALUES 
((SELECT TeacherId FROM Teachers WHERE Email = 'j.staff@michaelhouse.org'), 5, 12, 0),
((SELECT TeacherId FROM Teachers WHERE Email = 'j.staff@michaelhouse.org'), 6, 12, 0),
((SELECT TeacherId FROM Teachers WHERE Email = 'j.staff@michaelhouse.org'), 7, 12, 0);

SELECT SubjectId, Name, GradeLevel FROM Subjects WHERE GradeLevel = 12;

-- Update subjects with meaningful names (adjust as needed)
UPDATE Subjects SET Name = 'Mathematics' WHERE SubjectId = 2;
UPDATE Subjects SET Name = 'English Home Language' WHERE SubjectId = 3;
UPDATE Subjects SET Name = 'Physical Sciences' WHERE SubjectId = 4;
UPDATE Subjects SET Name = 'Life Orientation' WHERE SubjectId = 5;
UPDATE Subjects SET Name = 'History' WHERE SubjectId = 6;
UPDATE Subjects SET Name = 'Accounting' WHERE SubjectId = 7;


-- Then link it to the teacher
UPDATE Teachers SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'j.staff@michaelhouse.org')
WHERE Email = 'j.staff@michaelhouse.org';



--
SELECT StudentId, FirstName, LastName, GradeLevel FROM Students WHERE GradeLevel = 8;

INSERT INTO Students (FirstName, LastName, GradeLevel, DOB)
VALUES 
('Thabo', 'Nkosi', 8, '2012-05-10'),
('Lerato', 'Molefe', 8, '2012-08-22'),
('Sipho', 'Dlamini', 8, '2012-02-15'),
('Zanele', 'Khumalo', 8, '2012-11-30');


-- Insert multiple parents
INSERT INTO Parents (Name, Email, Phone) VALUES 
('Thabo Parent', 'thabo.parent@demo.com', '0811111111'),
('Lerato Parent', 'lerato.parent@demo.com', '0822222222'),
('Sipho Parent', 'sipho.parent@demo.com', '0833333333'),
('Zanele Parent', 'zanele.parent@demo.com', '0844444444');

-- Now insert students, each with a different ParentId (assuming IDs are sequential starting from some value)
-- Use actual IDs from the insert above. If you just inserted these four, they will likely be e.g. 1,2,3,4.
INSERT INTO Students (FirstName, LastName, GradeLevel, DOB, ParentId)
VALUES 
('Thabo', 'Nkosi', 8, '2012-05-10', (SELECT ParentId FROM Parents WHERE Name = 'Thabo Parent')),
('Lerato', 'Molefe', 8, '2012-08-22', (SELECT ParentId FROM Parents WHERE Name = 'Lerato Parent')),
('Sipho', 'Dlamini', 8, '2012-02-15', (SELECT ParentId FROM Parents WHERE Name = 'Sipho Parent')),
('Zanele', 'Khumalo', 8, '2012-11-30', (SELECT ParentId FROM Parents WHERE Name = 'Zanele Parent'));

-- Insert a dummy parent (adjust Name, Email, Phone as needed)
INSERT INTO Parents (Name, Contact, CellPhone)
VALUES ('Demo Parent', 'parent@demo.com', '1234566789');

-- Get the generated ParentId
DECLARE @ParentId INT = SCOPE_IDENTITY();

-- Insert students with Gender (and any other required fields)
INSERT INTO Students (FirstName, LastName, GradeLevel, DOB, ParentId, Gender)
VALUES 
('Thabo', 'Nkosi', 8, '2012-05-10', @ParentId, 'Male'),
('Lerato', 'Molefe', 8, '2012-08-22', @ParentId, 'Female'),
('Sipho', 'Dlamini', 8, '2012-02-15', @ParentId, 'Male'),
('Zanele', 'Khumalo', 8, '2012-11-30', @ParentId, 'Female');

-- Update existing students with Gender and StudentNumber
UPDATE Students 
SET Gender = 'Male', StudentNumber = 1001
WHERE FirstName = 'Thabo' AND LastName = 'Nkosi' AND GradeLevel = 8;

UPDATE Students 
SET Gender = 'Female', StudentNumber = 1002
WHERE FirstName = 'Lerato' AND LastName = 'Molefe' AND GradeLevel = 8;

UPDATE Students 
SET Gender = 'Male', StudentNumber = 1003
WHERE FirstName = 'Sipho' AND LastName = 'Dlamini' AND GradeLevel = 8;

UPDATE Students 
SET Gender = 'Female', StudentNumber = 1004
WHERE FirstName = 'Zanele' AND LastName = 'Khumalo' AND GradeLevel = 8;




SELECT COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Parents';

Select * from Parents

SELECT COLUMN_NAME, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Students'
ORDER BY ORDINAL_POSITION;


INSERT INTO StudentSubjects (StudentId, SubjectId, Stream, IsCompulsory)
SELECT s.StudentId, sub.SubjectId, sub.Stream, sub.IsCompulsory
FROM Students s
CROSS JOIN Subjects sub
WHERE s.GradeLevel = 8 
  AND sub.GradeLevel = 8
  AND NOT EXISTS (
      SELECT 1 FROM StudentSubjects ss 
      WHERE ss.StudentId = s.StudentId AND ss.SubjectId = sub.SubjectId
  );

  SELECT COUNT(*) AS EnrollmentsAdded FROM StudentSubjects
WHERE StudentId IN (SELECT StudentId FROM Students WHERE GradeLevel = 8)
  AND SubjectId IN (SELECT SubjectId FROM Subjects WHERE GradeLevel = 8);