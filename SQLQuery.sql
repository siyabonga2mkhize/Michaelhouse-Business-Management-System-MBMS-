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


  Select * from TeacherAttendances;

  -- Delete all teacher attendance records (careful!)
DELETE FROM TeacherAttendances where TeacherAttendanceId = 1006;


Select * from Students;


Select * from StudentSubjects;

Select  * from Subjects;

SELECT TOP 1 PasswordHash FROM AppUsers;

INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
VALUES 
('Sipho Nkosi', 'sipho.nkosi@student.michaelhouse.org', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Student'),
('Thabo Nkosi', 'thabo.nkosi@student.michaelhouse.org', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Student'),
('Lerato Molefe', 'lerato.molefe@student.michaelhouse.org', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Student'),
('Sipho Dlamini', 'sipho.dlamini@student.michaelhouse.org', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Student'),
('Zanele Khumalo', 'zanele.khumalo@student.michaelhouse.org', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Student');

-- Then link them (assuming the AppUser identities are in the same order as student IDs)
UPDATE Students SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'sipho.nkosi@student.michaelhouse.org') WHERE StudentId = 2;
UPDATE Students SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'thabo.nkosi@student.michaelhouse.org') WHERE StudentId = 3;
UPDATE Students SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'lerato.molefe@student.michaelhouse.org') WHERE StudentId = 4;
UPDATE Students SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'sipho.dlamini@student.michaelhouse.org') WHERE StudentId = 5;
UPDATE Students SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'zanele.khumalo@student.michaelhouse.org') WHERE StudentId = 6;

-- Get the ParentId for the parent with email notprobmx@gmail.com
DECLARE @ParentId INT = (SELECT ParentId FROM Parents WHERE Contact = 'notprobmx@gmail.com');

-- If the parent doesn't exist in Parents table, create it
IF @ParentId IS NULL
BEGIN
    INSERT INTO Parents (Name, Contact, CellPhone, UserId)
    VALUES ('Parent Name', 'notprobmx@gmail.com', NULL, (SELECT UserId FROM AppUsers WHERE Email = 'notprobmx@gmail.com'));
    SET @ParentId = SCOPE_IDENTITY();
END

-- Update all students that don't have a ParentId or specifically the ones linked to UserIds 5-9
-- Assuming StudentId corresponds to UserId (from your screenshot, StudentId 2-6 are the students)
UPDATE Students 
SET ParentId = @ParentId 
WHERE StudentId IN (2, 3, 4, 5, 6);  -- Adjust these IDs based on actual StudentId values

SELECT s.StudentId, s.FirstName, s.LastName, s.ParentId, p.Name AS ParentName
FROM Students s
LEFT JOIN Parents p ON s.ParentId = p.ParentId
WHERE s.StudentId IN (2, 3, 4, 5, 6);

INSERT INTO Parents (Name, Contact, CellPhone, UserId)
SELECT 'Parent Name', Email, NULL, UserId
FROM AppUsers 
WHERE Email = 'notprobmx@gmail.com' AND Role = 'Parent';

Select * from Parents;


SELECT UserId, Email, Role FROM AppUsers WHERE Email = 'notprobmx@gmail.com';

Select * from Students;

UPDATE Students 
SET ParentId = 1
WHERE StudentId IN (2, 3, 4, 5);

SELECT StudentId, FirstName, LastName, ParentId 
FROM Students 
WHERE StudentId IN (1,2,3,4,5);



SELECT u.UserId, u.Email, u.Role, s.StudentId
FROM Users u
LEFT JOIN Students s ON u.UserId = s.UserId
WHERE u.Role = 'Student';


SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME LIKE '%user%' OR TABLE_NAME LIKE '%Student%';



    SELECT 
    fk.name AS FK_Name,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
FROM 
    sys.foreign_keys fk
INNER JOIN 
    sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
WHERE 
    OBJECT_NAME(fk.referenced_object_id) = 'Students';


    SELECT u.UserId, u.Email, u.Name
FROM AppUsers u
LEFT JOIN Students s ON u.UserId = s.UserId
WHERE u.Role = 'Student' AND s.StudentId IS NULL;


INSERT INTO Students (UserId, FirstName, LastName, DOB, ParentId, GradeLevel, EnrollmentDate, Gender)
SELECT 
    u.UserId,
    CASE 
        WHEN CHARINDEX(' ', u.Name) > 0 
        THEN LEFT(u.Name, CHARINDEX(' ', u.Name) - 1)
        ELSE u.Name 
    END AS FirstName,
    CASE 
        WHEN CHARINDEX(' ', u.Name) > 0 
        THEN RIGHT(u.Name, LEN(u.Name) - CHARINDEX(' ', u.Name))
        ELSE '' 
    END AS LastName,
    '2005-01-01',          -- default DOB (adjust as needed)
    2,                  -- ParentId (can be linked later)
    8,                     -- GradeLevel
    GETDATE(),             -- EnrollmentDate
    'Male'                 -- Gender (change to 'Female' or 'Other' if required)
FROM AppUsers u
LEFT JOIN Students s ON u.UserId = s.UserId
WHERE u.Role = 'Student' AND s.StudentId IS NULL;


-- 1. Check if the email already exists (optional, but good practice)
IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email = 'parent@demo.com')
BEGIN
    -- 2. Insert new user
    --    Default password: 'Password123' hashed with SHA256 (same as your HashPassword method)
    --    You can change the password and role as needed.
    DECLARE @NewUserId INT;
    
    INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
    VALUES (
        (SELECT Name FROM Parents WHERE ParentId = 2),   -- Name from Parents table
        'parent@demo.com',
        -- Hash of 'Password123' (use your actual hash logic; below is a SHA256 base64 example)
        'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=',   -- Replace with actual hash if different
        'Parent'   -- Role
    );
    
    SET @NewUserId = SCOPE_IDENTITY();
    
    -- 3. Update the parent record with the new UserId
    UPDATE Parents
    SET UserId = @NewUserId
    WHERE ParentId = 2;
    
    -- Optional: verify
    SELECT 'Parent user created and linked.' AS Result;
END
ELSE
BEGIN
    SELECT 'Email already exists in AppUsers.' AS Result;
END

UPDATE AppUsers
SET PasswordHash = 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U='
WHERE Email = 'parent@demo.com';


DELETE FROM Students
WHERE UserId IS NULL;

SELECT s.*, u.Email
FROM Students s
LEFT JOIN AppUsers u ON s.UserId = u.UserId
WHERE s.UserId IS NULL;   -- or any other condition


-- List of subject IDs to enroll every student in
DECLARE @SubjectIds TABLE (SubjectId INT);
INSERT INTO @SubjectIds VALUES 
(1002), (1003), (1004), (1005), (1006),
(1007), (1008), (1009), (1010), (1011);

-- Insert for each student and each subject, if not already enrolled
INSERT INTO StudentSubjects (StudentId, SubjectId, Stream, IsCompulsory)
SELECT s.StudentId, sub.SubjectId, '', 1   -- Stream = '' (empty string) instead of NULL
FROM Students s
CROSS JOIN @SubjectIds sub
WHERE NOT EXISTS (
    SELECT 1 FROM StudentSubjects ss
    WHERE ss.StudentId = s.StudentId AND ss.SubjectId = sub.SubjectId
);

SELECT s.StudentId, s.FirstName, s.LastName, sub.SubjectId
FROM Students s
CROSS JOIN (VALUES (1002),(1003),(1004),(1005),(1006),(1007),(1008),(1009),(1010),(1011)) AS sub(SubjectId)
WHERE NOT EXISTS (
    SELECT 1 FROM StudentSubjects ss
    WHERE ss.StudentId = s.StudentId AND ss.SubjectId = sub.SubjectId
)
ORDER BY s.StudentId, sub.SubjectId;

ALTER DATABASE MichaelHouse SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE MichaelHouse;


select * from DriverApplications;

select * from StreamEnrolments;


SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'StreamEnrolments' 
  AND COLUMN_NAME = 'TeacherId';


  ALTER TABLE dbo.Applications ADD DriverAppId INT NULL;

Select * from Applications;

-- Transport 
-- 1. Check if a Transport Manager already exists
IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Role = 'TransportManager')
BEGIN
    -- 2. Insert the Transport Manager
    INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
    VALUES (
        'Transport Manager',                    -- Name
        'transport@michaelhouse.co.za',        -- Email
        'vZ4cL5K0yRr2xW8nF3qA9bH7jM1pU6tY2eC4dG5sW7=',  -- Hash of "Transport@123"
        'TransportManager'                     -- Role (exact spelling)
    );
    
    -- Optional: get the new UserId
    DECLARE @NewUserId INT = SCOPE_IDENTITY();
    
    -- 3. If you have a Drivers table and want to link this manager to a Driver record (optional), uncomment:
    -- INSERT INTO Drivers (UserId, FullName, Email, IsActive, DateCreated, HasPDP, LicenceNumber)
    -- VALUES (@NewUserId, 'Transport Manager', 'transport@michaelhouse.co.za', 1, GETDATE(), 1, 'MGR000');
    
    SELECT 'Transport Manager added. Login: transport@michaelhouse.co.za / Password: Transport@123' AS Result;
END
ELSE
BEGIN
    SELECT 'Transport Manager already exists. No action taken.' AS Result;
END


UPDATE AppUsers
SET PasswordHash = 'Nt5wp81dThJvZ2gsftCIr4v5RBsK76mnokNklLWFy70='
WHERE Email = 'transport@michaelhouse.co.za';


ALTER TABLE dbo.Applications ADD DriverAppId INT NULL;


SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId';


IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId')
BEGIN
    ALTER TABLE dbo.Applications DROP COLUMN DriverAppId;
    PRINT 'Column dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Column does not exist – no action taken.';
END


IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId')
    ALTER TABLE dbo.Applications DROP COLUMN DriverAppId;



    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId')
BEGIN
    ALTER TABLE dbo.Applications ADD DriverAppId INT NULL;
    PRINT 'Column added – error bypassed.';
END
ELSE
BEGIN
    PRINT 'Column already exists – nothing to do.';
END

-- 1. Add missing column to Applications (bypass current error)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Applications' AND COLUMN_NAME = 'DriverAppId')
    ALTER TABLE dbo.Applications ADD DriverAppId INT NULL;

-- 2. Add missing column to AdminReviews (prevent future error)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AdminReviews' AND COLUMN_NAME = 'DriverAppId')
    ALTER TABLE dbo.AdminReviews ADD DriverAppId INT NULL;

select * from AdminReviews;


select * from TripStudents;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TripStudents') AND name = 'TripSchedule_Id')
    ALTER TABLE dbo.TripStudents DROP COLUMN TripSchedule_Id;



   -- Delete duplicate schedules that have no students, but keep the first one per TripRequestId
WITH Duplicates AS (
    SELECT Id, TripRequestId, Status,
           ROW_NUMBER() OVER (PARTITION BY TripRequestId ORDER BY 
               CASE WHEN (SELECT COUNT(*) FROM TripStudents WHERE TripScheduleId = TripSchedules.Id) > 0 THEN 0 ELSE 1 END, Id) AS rn
    FROM TripSchedules
)
DELETE FROM TripSchedules WHERE Id IN (SELECT Id FROM Duplicates WHERE rn > 1);


Select * from Drivers;
-- Insert drivers
INSERT INTO Drivers (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, IsActive, DateCreated, PasswordHash, UserId, ImageUrl)
VALUES 
('Themba Nkosi', '9001015009087', '0711111111', 'themba@michaelhouse.co.za', 'LIC1001', '2028-12-31', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', NULL, '/Content/Images/Drivers/themba.jpg'),
('Thabo Mkhize',  '8805056009088', '0722222222', 'thabo@michaelhouse.co.za',   'LIC1002', '2027-10-15', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', NULL, '/Content/Images/Drivers/thabo.jpg'),
('Andile Zulu',   '9202027009089', '0733333333', 'andile@michaelhouse.co.za',  'LIC1003', '2029-05-20', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', NULL, '/Content/Images/Drivers/andile.jpg'),
('Nkosi Khumalo', '8703038009090', '0744444444', 'nkosi@michaelhouse.co.za',   'LIC1004', '2026-11-30', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', NULL, '/Content/Images/Drivers/nkosi.jpg');

-- Create AppUser accounts for each driver (same email, role 'Driver')
INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
SELECT FullName, Email, 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', 'Driver'
FROM Drivers WHERE UserId IS NULL;

-- Link UserId to Drivers
UPDATE d
SET UserId = u.UserId
FROM Drivers d
INNER JOIN AppUsers u ON d.Email = u.Email
WHERE d.UserId IS NULL;


---
INSERT INTO Vehicles (VehicleNumber, Model, Type, Capacity, IsActive, DateAdded, ImageUrl)
VALUES 
('GP 123 456', 'Toyota Quantum', 'Bus', 14, 1, GETDATE(), '/Content/Images/Vehicles/quantum1.jpg'),
('GP 789 012', 'Hyundai H1',    'Bus', 11, 1, GETDATE(), '/Content/Images/Vehicles/hyundai1.jpg'),
('GP 345 678', 'Ford Transit',  'Bus', 12, 1, GETDATE(), '/Content/Images/Vehicles/ford1.jpg'),
('GP 901 234', 'Mercedes Sprinter', 'Bus', 10, 1, GETDATE(), '/Content/Images/Vehicles/mercedes1.jpg'),
('GP 456 789', 'Nissan NP200',  'Van', 3,  1, GETDATE(), '/Content/Images/Vehicles/nissan1.jpg');


Select * from TripRequests;