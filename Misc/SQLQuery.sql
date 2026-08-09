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
BEGIN TRANSACTION;

-- 1. Assign a UserID to teacher 3 (example: based on email match)
--    Replace 'desired.email@example.com' with the actual email of the user in AppUsers
UPDATE Teachers 
SET UserID = (SELECT UserID FROM AppUsers WHERE Email = 'teacher.lower@michaelhouse.org')
WHERE TeacherID = 3;

-- 2. Remove all subject‑grade assignments for teacher 4
DELETE FROM TeacherSubjectGrades WHERE TeacherID = 4;

-- 3. Delete teacher 4 from Teachers table
DELETE FROM Teachers WHERE TeacherID = 4;

COMMIT;




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

INSERT INTO Parents (Name, Contact, CellPhone)
VALUES 
('Nomvula Zuma', 'nomvula.z@webmail.co.za', '0732228899'), -- Will be ParentId 12
('Kevin Naidoo', 'k.naidoo@telkomsa.net', '0817773344');   -- Will be ParentId 13

Select * from Parents;

-- Update for Siya
UPDATE Parents SET EmergencyContactName = 'Siya', EmergencyContactPhone = '1234567890' WHERE ParentId = 1;

-- Update for Demo Parent
UPDATE Parents SET EmergencyContactName = 'Demo Parent', EmergencyContactPhone = '123456789' WHERE ParentId = 2;

-- Update for Parent
UPDATE Parents SET EmergencyContactName = 'Parent', EmergencyContactPhone = '1234567890' WHERE ParentId = 6;

-- Update for Busi Naidoo
UPDATE Parents SET EmergencyContactName = 'Busi Naidoo', EmergencyContactPhone = '0825551234' WHERE ParentId = 7;

-- Update for Johannes Steyn
UPDATE Parents SET EmergencyContactName = 'Johannes Steyn', EmergencyContactPhone = '0714449876' WHERE ParentId = 8;

-- Update for Lindiwe Mazibuko
UPDATE Parents SET EmergencyContactName = 'Lindiwe Mazibuko', EmergencyContactPhone = '0831112233' WHERE ParentId = 9;

-- Update for David Mokwena
UPDATE Parents SET EmergencyContactName = 'David Mokwena', EmergencyContactPhone = '0849994455' WHERE ParentId = 10;

-- Update for Sarah Pillay
UPDATE Parents SET EmergencyContactName = 'Sarah Pillay', EmergencyContactPhone = '0728886677' WHERE ParentId = 11;

-- Update for Nomvula Zuma
UPDATE Parents SET EmergencyContactName = 'Nomvula Zuma', EmergencyContactPhone = '0732228899' WHERE ParentId = 12;

-- Update for Kevin Naidoo
UPDATE Parents SET EmergencyContactName = 'Kevin Naidoo', EmergencyContactPhone = '0817773344' WHERE ParentId = 13;




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

INSERT INTO Parents (Name, Contact, CellPhone)
VALUES 
('Busi Naidoo', 'busi.n@gmail.com', '0825551234'),
('Johannes Steyn', 'jsteyn@mweb.co.za', '0714449876'),
('Lindiwe Mazibuko', 'lindi.mazi@outlook.com', '0831112233'),
('David Mokwena', 'dmokwena@work.co.za', '0849994455'),
('Sarah Pillay', 'spillay@fnb.co.za', '0728886677');

-- Get the generated ParentId
DECLARE @ParentId INT = SCOPE_IDENTITY();

-- Insert students with Gender (and any other required fields)
INSERT INTO Students (FirstName, LastName, GradeLevel, DOB, ParentId, Gender)
VALUES 
('Thabo', 'Nkosi', 8, '2012-05-10', @ParentId, 'Male'),
('Lerato', 'Molefe', 8, '2012-08-22', @ParentId, 'Female'),
('Sipho', 'Dlamini', 8, '2012-02-15', @ParentId, 'Male'),
('Zanele', 'Khumalo', 8, '2012-11-30', @ParentId, 'Female');


INSERT INTO Students (FirstName, LastName, GradeLevel, DOB, ParentId, Gender)
VALUES 
-- Assigned to Busi Naidoo (ParentId 7)
('Bongani', 'Ndlovu', 8, '2012-04-05', 7, 'Male'),
('Nandi', 'Mokoena', 8, '2012-09-12', 7, 'Female'),
('Sibusiso', 'Gumede', 8, '2012-01-20', 7, 'Male'),

-- Assigned to Johannes Steyn (ParentId 8)
('Thandi', 'Mbatha', 8, '2012-06-18', 8, 'Female'),
('Lethabo', 'Baloyi', 8, '2012-03-25', 8, 'Male'),
('Melokuhle', 'Mabuza', 8, '2012-07-08', 8, 'Female'),

-- Assigned to Lindiwe Mazibuko (ParentId 9)
('Bandile', 'Jacobs', 8, '2012-10-14', 9, 'Male'),
('Palesa', 'Van Wyk', 8, '2012-12-05', 9, 'Female'),
('Lubanzi', 'Mokoena', 9, '2011-04-12', 9, 'Male'),

-- Assigned to David Mokwena (ParentId 10)
('Onalerona', 'Sibiya', 9, '2011-09-05', 10, 'Female'),
('Nkazimulo', 'Zuma', 9, '2011-01-22', 10, 'Male'),
('Zanokuhle', 'Buthelezi', 9, '2011-11-14', 10, 'Female'),

-- Assigned to Sarah Pillay (ParentId 11)
('Enzokuhle', 'Mbewe', 10, '2010-06-30', 11, 'Male'),
('Iminathi', 'Tshabalala', 10, '2010-02-18', 11, 'Female'),
('Bandile', 'Mabaso', 10, '2010-08-09', 11, 'Male'),
('Minenhle', 'Zwane', 10, '2010-12-25', 11, 'Female');



INSERT INTO Students (FirstName, LastName, GradeLevel, DOB, ParentId, Gender)
VALUES 
-- Mix with Existing Parent IDs (7 - 11)
('Kungawo', 'Ndlovu', 8, '2012-03-15', 7, 'Male'),
('Siyabonga', 'Mokoena', 8, '2012-07-22', 8, 'Male'),
('Amahle', 'Dlamini', 8, '2012-11-05', 9, 'Female'),
('Thandolwethu', 'Nkosi', 8, '2012-01-30', 10, 'Female'),
('Bokamoso', 'Molefe', 8, '2012-05-14', 11, 'Male'),

-- Assign to New Parent IDs (12 - 13)
('Lethabo', 'Zuma', 8, '2012-09-02', 12, 'Male'),
('Onalerona', 'Zuma', 8, '2012-02-18', 12, 'Female'),
('Nkazimulo', 'Naidoo', 8, '2012-06-25', 13, 'Male'),
('Melokuhle', 'Naidoo', 8, '2012-10-10', 13, 'Female'),
('Zanokuhle', 'Naidoo', 8, '2012-04-12', 13, 'Female');


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
WHERE Email = 'notprobmx@gmail.com' AND Role = 'Parent';;

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


IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Role = 'TransportManager')
BEGIN
    -- 2. Insert the Transport Manager
    INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
    VALUES (
        'Siya',                    -- Name
        'Siya@michaelhouse.co.za',        -- Email
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

UPDATE AppUsers 
SET PasswordHash = (SELECT PasswordHash FROM AppUsers WHERE Email = 'admin@michaelhouse.co.za')
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

Delete from TripRequests where Id = 5;

Select * from TripSchedules;


DECLARE @fkName NVARCHAR(128)



SELECT @fkName = name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.TripStudents') AND referenced_object_id = OBJECT_ID('dbo.TripSchedules')
IF @fkName IS NOT NULL
    EXEC('ALTER TABLE dbo.TripStudents DROP CONSTRAINT ' + @fkName)


    -- Drop all foreign keys from TripStudents to TripSchedules
DECLARE @sql NVARCHAR(MAX) = ''
SELECT @sql = @sql + 'ALTER TABLE dbo.TripStudents DROP CONSTRAINT ' + QUOTENAME(name) + ';'
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('dbo.TripStudents')
  AND referenced_object_id = OBJECT_ID('dbo.TripSchedules')
EXEC sp_executesql @sql

-- Also drop any default constraints on TripScheduleId (if any)
DECLARE @defaultSql NVARCHAR(MAX) = ''
SELECT @defaultSql = @defaultSql + 'ALTER TABLE dbo.TripStudents DROP CONSTRAINT ' + QUOTENAME(name) + ';'
FROM sys.default_constraints
WHERE parent_object_id = OBJECT_ID('dbo.TripStudents')
  AND col_name(parent_object_id, parent_column_id) = 'TripScheduleId'
EXEC sp_executesql @defaultSql


-- Drop all foreign keys from TripStudents to TripSchedules
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql = @sql + 'ALTER TABLE dbo.TripStudents DROP CONSTRAINT ' + QUOTENAME(name) + ';'
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID('dbo.TripStudents')
  AND referenced_object_id = OBJECT_ID('dbo.TripSchedules');
EXEC sp_executesql @sql;

-- Also drop any default constraint on TripScheduleId
DECLARE @defaultSql NVARCHAR(MAX) = '';
SELECT @defaultSql = @defaultSql + 'ALTER TABLE dbo.TripStudents DROP CONSTRAINT ' + QUOTENAME(name) + ';'
FROM sys.default_constraints
WHERE parent_object_id = OBJECT_ID('dbo.TripStudents')
  AND col_name(parent_object_id, parent_column_id) = 'TripScheduleId';
EXEC sp_executesql @defaultSql;

Select * from TripStudents;

SELECT * FROM AppUsers WHERE Role = 'Driver';
SELECT d.Id, d.FullName, d.Email AS DriverEmail, d.UserId, u.UserId AS AppUserId, u.Email AS AppUserEmail, u.PasswordHash
FROM Drivers d
LEFT JOIN AppUsers u ON d.UserId = u.UserId;

UPDATE AppUsers
SET PasswordHash = 'OI/CLGhlBc6ItZ4qmx3u+T3JuFVYBXjqcEqjg9Y+4f0='
WHERE Role = 'Driver';

SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DriverAvailabilities'


--Manallu add avaliablites table 
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DriverAvailabilities')
BEGIN
    CREATE TABLE dbo.DriverAvailabilities (
        Id INT IDENTITY(1,1) NOT NULL,
        DriverId INT NOT NULL,
        StartDate DATETIME NOT NULL,
        EndDate DATETIME NOT NULL,
        Reason NVARCHAR(500) NULL,
        DateCreated DATETIME NOT NULL,
        CONSTRAINT PK_DriverAvailabilities PRIMARY KEY (Id)
    )
END

IF NOT EXISTS (SELECT * FROM sys.foreBign_keys WHERE name = 'FK_DriverAvailabilities_Drivers')
BEGIN
    ALTER TABLE dbo.DriverAvailabilities ADD CONSTRAINT FK_DriverAvailabilities_Drivers FOREIGN KEY (DriverId) REFERENCES dbo.Drivers(Id)
END




---
Select * from DriverAvailabilities;
INSERT INTO DriverAvailabilities (DriverId, StartDate, EndDate, Reason, DateCreated)
VALUES (1, '2025-01-01', '2025-01-05', 'Test', GETDATE());

SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'


CREATE TABLE dbo.TripRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TeacherId INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    DepartureTime DATETIME2 NOT NULL,
    ReturnTime DATETIME2 NOT NULL,
    Destination NVARCHAR(200),
    MaxStudents INT NOT NULL,
    Status NVARCHAR(20),
    RejectionReason NVARCHAR(500),
    RequestedAt DATETIME2 NOT NULL,
    ApprovedByAdminId INT NULL,
    ApprovedAt DATETIME2 NULL,
    DestinationLat FLOAT NULL,
    DestinationLng FLOAT NULL,
    RebookedFromId INT NULL,
);


SELECT COUNT(*) FROM AppUsers WHERE Email = 'transport@michaelhouse.co.za';


-- Update the hash to the correct value for "Transport@123"
UPDATE AppUsers 
SET PasswordHash = 'vZ4cL5K0yRr2xW8nF3qA9bH7jM1pU6tY2eC4dG5sW7='
WHERE Email = 'transport@michaelhouse.co.za';

-- Also ensure the role is exactly "TransportManager" (no spaces)
UPDATE AppUsers 
SET Role = 'TransportManager'
WHERE Email = 'transport@michaelhouse.co.za';


select * from DriverApplications;

delete from DriverApplications where id = 2;

CREATE TABLE dbo.DriverApplications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(MAX) NOT NULL,
    IDNumber NVARCHAR(20) NOT NULL,
    PhoneNumber NVARCHAR(20) NOT NULL,
    Email NVARCHAR(200) NOT NULL,
    LicenceNumber NVARCHAR(50) NOT NULL,
    LicenceExpiryDate DATETIME NOT NULL,
    HasPDP BIT NOT NULL,
    DocumentPath NVARCHAR(500),
    Status NVARCHAR(50),
    AdminNotes NVARCHAR(MAX),
    DateSubmitted DATETIME,
    ReviewedDate DATETIME,
    UserId INT NULL,
    PublicTokenHash NVARCHAR(200),
    PublicTokenExpiry DATETIME
);

CREATE TABLE dbo.DriverDocuments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DriverApplicationId INT NOT NULL,
    FilePath NVARCHAR(500),
    DocumentType NVARCHAR(50),
    OtherDocumentType NVARCHAR(200),
    CONSTRAINT FK_DriverDocuments_DriverApplication FOREIGN KEY (DriverApplicationId) REFERENCES dbo.DriverApplications(Id)
);


-- ============================================
-- TEST DATA FOR DriverApplications
-- ============================================
-- Add a default value (0 = false) for InterviewEmailSent

-- Application 1: Pending, with documents, linked to existing AppUser (UserId 1015)
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Themba Nkosi', '9001015009087', '0711111111', 'themba.nkosi@example.com', 'LIC1001', '2028-12-31', 1, NULL, 'Pending', NULL, GETDATE(), NULL, 1015, NULL, NULL, 0);

-- Application 2: Approved, waiting for interview, linked to existing AppUser
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Andile Zulu', '9202027009089', '0733333333', 'andile.zulu@example.com', 'LIC1003', '2029-05-20', 1, NULL, 'Approved', 'ID and licence verified. AI score 85.', DATEADD(day, -5, GETDATE()), DATEADD(day, -2, GETDATE()), 1017, NULL, NULL, 0);

-- Application 3: Rejected, with rejection reason
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Nkosi Khumalo', '8703038009090', '0744444444', 'nkosi.khumalo@example.com', 'LIC1004', '2026-11-30', 0, NULL, 'Rejected', 'PDP not held and licence expires soon.', DATEADD(day, -10, GETDATE()), DATEADD(day, -8, GETDATE()), 1018, NULL, NULL, 0);

-- Application 4: Pending, no user linked (anonymous with public token)
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Sibusiso Dlamini', '9001015009087', '0710000000', 'sibusiso.dlamini@example.com', 'LIC1001', '2028-12-31', 1, NULL, 'Pending', NULL, GETDATE(), NULL, NULL, 'e0d123e5f3169a7f3d5b1c2a4f6e8d9c7b5a3e1f2d4c6b8a0e2f4c6d8a0e1f', DATEADD(day, 14, GETDATE()), 0);

-- Application 5: Approved, not yet interviewed, anonymous with public token
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Thabo Mkhize', '8805056009088', '0722222222', 'thabo.mkhize@example.com', 'LIC1002', '2027-10-15', 1, NULL, 'Approved', 'Interview scheduled for next week.', DATEADD(day, -3, GETDATE()), GETDATE(), NULL, 'a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3', DATEADD(day, 7, GETDATE()), 0);

-- Application 6: Rejected, anonymous, with reason
INSERT INTO DriverApplications (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewEmailSent)
VALUES 
('Bongani Ndlovu', '9801015009087', '0799999999', 'bongani.ndlovu@example.com', 'LIC2001', '2025-01-01', 0, NULL, 'Rejected', 'Failed background check.', DATEADD(day, -15, GETDATE()), DATEADD(day, -12, GETDATE()), NULL, NULL, NULL, 0);

-- ============================================
-- TEST DATA FOR DriverDocuments
-- ============================================
-- These will now work because the parent rows exist

-- Documents for Application 1 (Pending, Themba Nkosi)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(1, '/Uploads/id_themba.pdf', 'ID', NULL),
(1, '/Uploads/licence_themba.pdf', 'Licence', NULL),
(1, '/Uploads/pdp_themba.pdf', 'Other', 'PDP Certificate');

-- Documents for Application 2 (Approved, Andile Zulu)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(2, '/Uploads/id_andile.pdf', 'ID', NULL),
(2, '/Uploads/licence_andile.pdf', 'Licence', NULL);

-- Documents for Application 3 (Rejected, Nkosi Khumalo)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(3, '/Uploads/id_nkosi.pdf', 'ID', NULL),
(3, '/Uploads/licence_nkosi.pdf', 'Licence', NULL);

-- Documents for Application 4 (Pending, anonymous Sibusiso Dlamini)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(4, '/Uploads/id_sibusiso.pdf', 'ID', NULL),
(4, '/Uploads/licence_sibusiso.pdf', 'Licence', NULL),
(4, '/Uploads/other_sibusiso.pdf', 'Other', 'Medical Certificate');

-- Documents for Application 5 (Approved, anonymous Thabo Mkhize)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(5, '/Uploads/id_thabo.pdf', 'ID', NULL),
(5, '/Uploads/licence_thabo.pdf', 'Licence', NULL);

-- Documents for Application 6 (Rejected, anonymous Bongani Ndlovu)
INSERT INTO DriverDocuments (DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES 
(6, '/Uploads/id_bongani.pdf', 'ID', NULL),
(6, '/Uploads/licence_bongani.pdf', 'Licence', NULL);

-- Final query (fixed: use a proper condition or remove the WHERE clause)
SELECT Id, FullName, LicenceExpiryDate FROM DriverApplications;
-- If you want only rows where LicenceExpiryDate IS NULL, use:
-- SELECT Id, FullName, LicenceExpiryDate FROM DriverApplications WHERE LicenceExpiryDate IS NULL;
--
SELECT Id, FullName, Email, InterviewDateTime, InterviewMeetingLink 
FROM DriverApplications 
WHERE Id = 2


Select * from man ;

Select * from Drivers;

Select * from Driverapplications;

SELECT Id,FullName , ImageUrl FROM Drivers;

UPDATE Drivers SET ImageUrl = '/Content/Images/Drivers/thabo.jpg' WHERE FullName = 'Thabo Mkhize';

DELETE FROM Drivers
WHERE Id IN (1, 5, 6);

BEGIN TRANSACTION;

-- 1. Reassign trips to driver 2
UPDATE dbo.TripSchedules
SET DriverId = 2 
WHERE DriverId IN (1, 5, 6);

-- 2. Delete the original drivers
DELETE FROM Drivers
WHERE Id IN (1, 5, 6);

COMMIT TRANSACTION;


BEGIN TRANSACTION;

-- 1. Clear references in TripVehicleAssignments table
DELETE FROM dbo.TripVehicleAssignments
WHERE DriverId IN (1, 5, 6);

-- 2. Clear references in TripSchedules table
DELETE FROM dbo.TripSchedules
WHERE DriverId IN (1, 5, 6);

-- 3. Delete the records from Drivers table
DELETE FROM dbo.Drivers
WHERE Id IN (1, 5, 6);

-- Commit changes if everything executes without errors
COMMIT TRANSACTION;


SELECT Id, TripRequestId, DriverId, ScheduledDate, Status
FROM TripSchedules
WHERE DriverId = 4;

Select * from TripSchedules


UPDATE ts
SET ts.DriverId = tva.DriverId,
    ts.VehicleId = tva.VehicleId
FROM TripSchedules ts
INNER JOIN TripVehicleAssignments tva ON tva.TripScheduleId = ts.Id
WHERE ts.DriverId IS NULL AND tva.Id IN (SELECT MIN(Id) FROM TripVehicleAssignments GROUP BY TripScheduleId)




------

SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
ORDER BY t.name, c.column_id;


SELECT Id, VehicleNumber, ImageUrl FROM Vehicles;
Select * from Vehicles;
delete from Vehicles where Id in (2,3);

-- Toyota Quantum (GP 123 456) – already done, but here for completeness
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/quantum1.jpg' WHERE VehicleNumber = 'GP 123 456';

-- Ford Transit (GP 345 678)
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/ford1.jpg' WHERE VehicleNumber = 'GP 345 678';

-- Hyundai H1 (GP 789 012)
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/hyundai1.jpg' WHERE VehicleNumber = 'GP 789 012';

-- Mercedes Sprinter (GP 901 234)
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/mercedes1.jpg' WHERE VehicleNumber = 'GP 901 234';

-- Nissan NP200 (GP 456 789)
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/nissan1.jpg' WHERE VehicleNumber = 'GP 456 789';

-- Mercedes Sprinter for MH-TRIP-001, MH-TRIP-002, MH-TRIP-003
UPDATE Vehicles SET ImageUrl = '/Content/Images/Vehicles/mercedes1.jpg' WHERE VehicleNumber IN ('MH-TRIP-001', 'MH-TRIP-002', 'MH-TRIP-003');





select * from EmergencyAlerts;

SELECT TOP 10
    s.StudentId,
    s.StudentNumber,
    s.FirstName + ' ' + s.LastName AS StudentName,
    sp.BoardingStatus,
    r.Name          AS Residence,
    rm.RoomNumber,
    b.BedNumber,
    qr.QRCodeValue
FROM StudentQRCodes qr
JOIN Students s               ON s.StudentId = qr.StudentId
JOIN StudentProfiles sp       ON sp.StudentId = s.StudentId
LEFT JOIN ResidenceAllocations ra ON ra.StudentId = s.StudentId AND ra.IsActive = 1
LEFT JOIN Residences r        ON r.ResidenceId = ra.ResidenceId
LEFT JOIN Rooms rm            ON rm.RoomId = ra.RoomId
LEFT JOIN Beds b              ON b.BedId = ra.BedId
WHERE qr.IsActive = 1
  AND s.StudentNumber LIKE 'BDS%'
ORDER BY s.StudentId;
SELECT TOP 10
    QRCodeId,
    StudentId,
    QRCodeValue,
    LEN(QRImage) AS ImageSize,
    IsActive
FROM StudentQRCodes;
