-- ============================================================
-- 0. ADMIN USER
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email = 'admin@michaelhouse.co.za')
BEGIN
    INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
    VALUES ('System Admin', 'admin@michaelhouse.co.za', 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=', 'Admin');
END
ELSE
BEGIN
    -- Update password hash if admin already exists
    UPDATE AppUsers 
    SET PasswordHash = 'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U='
    WHERE Email = 'admin@michaelhouse.co.za';
END

-- ============================================================
-- 1. REFERENCE DATA: SUBJECTS (all non-null columns)
-- ============================================================
MERGE INTO Subjects AS target
USING (VALUES 
    ('Information Technology', 'ICT301', 12, 0, 0, 0, 0, 0, 0),
    ('Computer Science',        'CS102',   12, 0, 0, 0, 0, 0, 1),
    ('Mathematics',             'MATH401', 12, 0, 0, 0, 1, 0, 2),
    ('Physical Science',        'PHYS202', 12, 0, 0, 0, 0, 1, 3),
    ('English First Language',  'ENG101',  12, 0, 0, 1, 0, 0, 4),
    ('Business Studies',        'BUS301',  12, 0, 0, 0, 0, 0, 5)
) AS source (Name, Code, GradeLevel, Stream, IsCompulsory, IsLanguage, IsMathsOption, RequiresMaths, SortOrder)
ON target.Name = source.Name AND target.GradeLevel = source.GradeLevel
WHEN NOT MATCHED THEN
    INSERT (Name, Code, GradeLevel, Stream, IsCompulsory, IsLanguage, IsMathsOption, RequiresMaths, SortOrder)
    VALUES (source.Name, source.Code, source.GradeLevel, source.Stream,
            source.IsCompulsory, source.IsLanguage, source.IsMathsOption,
            source.RequiresMaths, source.SortOrder);

-- ============================================================
-- 2. TEACHERS & APPUSERS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Teachers WHERE Email = 'j.staff@michaelhouse.org')
BEGIN
    INSERT INTO Teachers (FirstName, LastName, Email, Phone, Specialization, HireDate)
    VALUES ('John', 'Staff', 'j.staff@michaelhouse.org', '0331234567', 'App Dev', GETDATE());

    IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email = 'j.staff@michaelhouse.org')
    BEGIN
        INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
        VALUES ('John Staff', 'j.staff@michaelhouse.org', '7v6S/u/Jq6iH7vXoM/XG8S8A0I7z8=', 'Teacher');
    END

    UPDATE Teachers SET UserId = (SELECT UserId FROM AppUsers WHERE Email = 'j.staff@michaelhouse.org')
    WHERE Email = 'j.staff@michaelhouse.org' AND UserId IS NULL;
END

-- Assign grade 12 subjects to this teacher
INSERT INTO TeacherSubjectGrades (TeacherId, SubjectId, Grade, Stream)
SELECT t.TeacherId, s.SubjectId, 12, 0
FROM Teachers t
CROSS JOIN Subjects s
WHERE t.Email = 'j.staff@michaelhouse.org'
  AND s.GradeLevel = 12
  AND NOT EXISTS (
      SELECT 1 FROM TeacherSubjectGrades tsg
      WHERE tsg.TeacherId = t.TeacherId AND tsg.SubjectId = s.SubjectId
  );

-- ============================================================
-- 3. PARENTS & PARENT APPUSER ACCOUNTS
-- ============================================================
MERGE INTO Parents AS target
USING (VALUES 
    ('Thabo Parent', 'thabo.parent@demo.com', '0811111111'),
    ('Lerato Parent', 'lerato.parent@demo.com', '0822222222'),
    ('Sipho Parent', 'sipho.parent@demo.com', '0833333333'),
    ('Zanele Parent', 'zanele.parent@demo.com', '0844444444'),
    ('Demo Parent', 'parent@demo.com', '1234566789'),
    ('Busi Naidoo', 'busi.n@gmail.com', '0825551234'),
    ('Johannes Steyn', 'jsteyn@mweb.co.za', '0714449876'),
    ('Lindiwe Mazibuko', 'lindi.mazi@outlook.com', '0831112233'),
    ('David Mokwena', 'dmokwena@work.co.za', '0849994455'),
    ('Sarah Pillay', 'spillay@fnb.co.za', '0728886677'),
    ('Nomvula Zuma', 'nomvula.z@webmail.co.za', '0732228899'),
    ('Kevin Naidoo', 'k.naidoo@telkomsa.net', '0817773344')
) AS source (Name, Contact, CellPhone)
ON target.Contact = source.Contact
WHEN NOT MATCHED THEN
    INSERT (Name, Contact, CellPhone) VALUES (source.Name, source.Contact, source.CellPhone);

-- Update emergency contacts for existing parents
UPDATE Parents SET EmergencyContactName = Name WHERE EmergencyContactName IS NULL;

-- Create AppUser for each parent (if not exists)
INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
SELECT 
    p.Name,
    p.Contact,
    'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=',  -- default password: Password123
    'Parent'
FROM Parents p
WHERE NOT EXISTS (
    SELECT 1 FROM AppUsers u WHERE u.Email = p.Contact
);

-- Link Parents to UserId
UPDATE p
SET p.UserId = u.UserId
FROM Parents p
INNER JOIN AppUsers u ON u.Email = p.Contact
WHERE p.UserId IS NULL;

-- ============================================================
-- 4. STUDENTS (with EnrollmentDate = GETDATE())
-- ============================================================
INSERT INTO Students (
    FirstName,
    LastName,
    GradeLevel,
    DOB,
    ParentId,
    StudentNumber,
    EnrollmentDate,
    IsBoarding,
    IsActive,
    HomeLanguage          -- optional, but you can set a default
)
SELECT
    s.FirstName,
    s.LastName,
    s.GradeLevel,
    s.DOB,
    p.ParentId,
    s.StudentNumber,
    GETDATE(),
    1,                    -- IsBoarding = 1 (boarding student)
    1,                    -- IsActive = 1 (active)
    'English'             -- default home language, adjust as needed
FROM (
    VALUES
        ('Thabo', 'Nkosi', 8, '2012-05-10', 'Thabo Parent', 1001),
        ('Lerato', 'Molefe', 8, '2012-08-22', 'Lerato Parent', 1002),
        ('Sipho', 'Dlamini', 8, '2012-02-15', 'Sipho Parent', 1003),
        ('Zanele', 'Khumalo', 8, '2012-11-30', 'Zanele Parent', 1004),
        ('Bongani', 'Ndlovu', 8, '2012-04-05', 'Busi Naidoo', NULL),
        ('Nandi', 'Mokoena', 8, '2012-09-12', 'Busi Naidoo', NULL),
        ('Sibusiso', 'Gumede', 8, '2012-01-20', 'Busi Naidoo', NULL),
        ('Thandi', 'Mbatha', 8, '2012-06-18', 'Johannes Steyn', NULL),
        ('Lethabo', 'Baloyi', 8, '2012-03-25', 'Johannes Steyn', NULL),
        ('Melokuhle', 'Mabuza', 8, '2012-07-08', 'Johannes Steyn', NULL),
        ('Bandile', 'Jacobs', 8, '2012-10-14', 'Lindiwe Mazibuko', NULL),
        ('Palesa', 'Van Wyk', 8, '2012-12-05', 'Lindiwe Mazibuko', NULL),
        ('Lubanzi', 'Mokoena', 9, '2011-04-12', 'Lindiwe Mazibuko', NULL),
        ('Onalerona', 'Sibiya', 9, '2011-09-05', 'David Mokwena', NULL),
        ('Nkazimulo', 'Zuma', 9, '2011-01-22', 'David Mokwena', NULL),
        ('Zanokuhle', 'Buthelezi', 9, '2011-11-14', 'David Mokwena', NULL),
        ('Enzokuhle', 'Mbewe', 10, '2010-06-30', 'Sarah Pillay', NULL),
        ('Iminathi', 'Tshabalala', 10, '2010-02-18', 'Sarah Pillay', NULL),
        ('Bandile', 'Mabaso', 10, '2010-08-09', 'Sarah Pillay', NULL),
        ('Minenhle', 'Zwane', 10, '2010-12-25', 'Sarah Pillay', NULL),
        ('Kungawo', 'Ndlovu', 8, '2012-03-15', 'Busi Naidoo', NULL),
        ('Siyabonga', 'Mokoena', 8, '2012-07-22', 'Johannes Steyn', NULL),
        ('Amahle', 'Dlamini', 8, '2012-11-05', 'Lindiwe Mazibuko', NULL),
        ('Thandolwethu', 'Nkosi', 8, '2012-01-30', 'David Mokwena', NULL),
        ('Bokamoso', 'Molefe', 8, '2012-05-14', 'Sarah Pillay', NULL),
        ('Lethabo', 'Zuma', 8, '2012-09-02', 'Nomvula Zuma', NULL),
        ('Onalerona', 'Zuma', 8, '2012-02-18', 'Nomvula Zuma', NULL),
        ('Nkazimulo', 'Naidoo', 8, '2012-06-25', 'Kevin Naidoo', NULL),
        ('Melokuhle', 'Naidoo', 8, '2012-10-10', 'Kevin Naidoo', NULL),
        ('Zanokuhle', 'Naidoo', 8, '2012-04-12', 'Kevin Naidoo', NULL)
    ) AS s(FirstName, LastName, GradeLevel, DOB, ParentName, StudentNumber)
    INNER JOIN Parents p ON p.Name = s.ParentName
WHERE NOT EXISTS (
    SELECT 1 FROM Students st
    WHERE st.FirstName = s.FirstName AND st.LastName = s.LastName AND st.DOB = s.DOB
);
-- Set ClassId for Grade 8 students to 1 (adjust if needed)
UPDATE Students SET ClassId = 1 WHERE GradeLevel = 8 AND ClassId IS NULL;

-- ============================================================
-- 5. STUDENT APPUSER ACCOUNTS
-- ============================================================
INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
SELECT 
    CONCAT(FirstName, ' ', LastName),
    LOWER(CONCAT(FirstName, '.', LastName, '@student.michaelhouse.org')),
    'ZehL4zUy+3hMSBKWdfnv86aCsnFowOp0Syz1juAjN8U=',
    'Student'
FROM Students s
WHERE NOT EXISTS (
    SELECT 1 FROM AppUsers u WHERE u.Email = LOWER(CONCAT(s.FirstName, '.', s.LastName, '@student.michaelhouse.org'))
);

UPDATE s
SET s.UserId = u.UserId
FROM Students s
INNER JOIN AppUsers u ON u.Email = LOWER(CONCAT(s.FirstName, '.', s.LastName, '@student.michaelhouse.org'))
WHERE s.UserId IS NULL;

-- ============================================================
-- 6. STUDENT SUBJECT ENROLLMENTS (Grade 8)
-- ============================================================
INSERT INTO StudentSubjects (StudentId, SubjectId, Stream, IsCompulsory)
SELECT s.StudentId, sub.SubjectId, 0, 1
FROM Students s
CROSS JOIN Subjects sub
WHERE s.GradeLevel = 8 
  AND sub.GradeLevel = 8
  AND NOT EXISTS (
      SELECT 1 FROM StudentSubjects ss 
      WHERE ss.StudentId = s.StudentId AND ss.SubjectId = sub.SubjectId
  );

-- ============================================================
-- 7. TRANSPORT DATA
-- ============================================================
MERGE INTO Drivers AS target
USING (VALUES 
    ('Themba Nkosi', '9001015009087', '0711111111', 'themba@michaelhouse.co.za', 'LIC1001', '2028-12-31', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', '/Content/Images/Drivers/themba.jpg'),
    ('Thabo Mkhize', '8805056009088', '0722222222', 'thabo@michaelhouse.co.za', 'LIC1002', '2027-10-15', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', '/Content/Images/Drivers/thabo.jpg'),
    ('Andile Zulu', '9202027009089', '0733333333', 'andile@michaelhouse.co.za', 'LIC1003', '2029-05-20', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', '/Content/Images/Drivers/andile.jpg'),
    ('Nkosi Khumalo', '8703038009090', '0744444444', 'nkosi@michaelhouse.co.za', 'LIC1004', '2026-11-30', 1, 1, GETDATE(), 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', '/Content/Images/Drivers/nkosi.jpg')
) AS source (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, IsActive, DateCreated, PasswordHash, ImageUrl)
ON target.Email = source.Email
WHEN NOT MATCHED THEN
    INSERT (FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, IsActive, DateCreated, PasswordHash, ImageUrl)
    VALUES (source.FullName, source.IDNumber, source.PhoneNumber, source.Email, source.LicenceNumber, source.LicenceExpiryDate, source.HasPDP, source.IsActive, source.DateCreated, source.PasswordHash, source.ImageUrl);

INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
SELECT FullName, Email, 'uPP4iORv7L0HNZ5o6+q6FpL3mW2y9TdD9ZcXmFvKbQY=', 'Driver'
FROM Drivers d
WHERE NOT EXISTS (SELECT 1 FROM AppUsers u WHERE u.Email = d.Email);

UPDATE d
SET d.UserId = u.UserId
FROM Drivers d
INNER JOIN AppUsers u ON d.Email = u.Email
WHERE d.UserId IS NULL;

-- FIXED Vehicles MERGE (removed duplicate "AS source")
MERGE INTO Vehicles AS target
USING (VALUES 
    ('GP 123 456', 'Toyota Quantum', 'Bus', 14, 1, GETDATE(), '/Content/Images/Vehicles/quantum1.jpg'),
    ('GP 345 678', 'Ford Transit', 'Bus', 12, 1, GETDATE(), '/Content/Images/Vehicles/ford1.jpg'),
    ('GP 901 234', 'Mercedes Sprinter', 'Bus', 10, 1, GETDATE(), '/Content/Images/Vehicles/mercedes1.jpg'),
    ('GP 456 789', 'Nissan NP200', 'Van', 3, 1, GETDATE(), '/Content/Images/Vehicles/nissan1.jpg')
) AS source (VehicleNumber, Model, Type, Capacity, IsActive, DateAdded, ImageUrl)
ON target.VehicleNumber = source.VehicleNumber
WHEN NOT MATCHED THEN
    INSERT (VehicleNumber, Model, Type, Capacity, IsActive, DateAdded, ImageUrl)
    VALUES (source.VehicleNumber, source.Model, source.Type, source.Capacity, source.IsActive, source.DateAdded, source.ImageUrl);
IF NOT EXISTS (SELECT 1 FROM AppUsers WHERE Email = 'transport@michaelhouse.co.za')
BEGIN
    INSERT INTO AppUsers (Name, Email, PasswordHash, Role)
    VALUES ('Transport Manager', 'transport@michaelhouse.co.za', 'vZ4cL5K0yRr2xW8nF3qA9bH7jM1pU6tY2eC4dG5sW7=', 'TransportManager');
END

SELECT 'Script completed successfully.' AS Status;


-- ============================================================
-- APPLICATION DATA (with Registrations & Invoices)
-- ============================================================

-- ------------------------------------------------------------
-- 1. Ensure we have at least one parent and one student to link to
--    (if they don't exist, create minimal test ones)
-- ------------------------------------------------------------
-- (Skip if you already have parents/students from earlier scripts)

IF NOT EXISTS (SELECT 1 FROM Parents)
BEGIN
    INSERT INTO Parents (Name, Contact, CellPhone, EmergencyContactName)
    VALUES ('Test Parent', 'test@example.com', '0810000000', 'Test Parent');
END

IF NOT EXISTS (SELECT 1 FROM Students)
BEGIN
    -- Insert a minimal student for testing
    DECLARE @ParentId INT = (SELECT TOP 1 ParentId FROM Parents);
    INSERT INTO Students (FirstName, LastName, DOB, GradeLevel, ParentId, IsBoarding, IsActive, EnrollmentDate)
    VALUES ('Test', 'Student', '2008-01-01', 8, @ParentId, 1, 1, GETDATE());
END

-- ------------------------------------------------------------
-- 2. Insert Applications for existing students (limit to 3)
-- ------------------------------------------------------------
DECLARE @AppYear INT = YEAR(GETDATE());
DECLARE @Status INT = 1;  -- 1 = Submitted (adjust as per your enum)

-- For each student that doesn't already have an application
INSERT INTO Applications (ParentId, StudentId, Date, ApplicationYear, GradeApplying, Status, AdditionalNotes)
SELECT 
    s.ParentId,
    s.StudentId,
    GETDATE(),
    @AppYear,
    s.GradeLevel,
    @Status,
    'Test application for ' + s.FirstName + ' ' + s.LastName
FROM Students s
WHERE s.StudentId IN (
    SELECT TOP 3 StudentId FROM Students ORDER BY StudentId
)
AND NOT EXISTS (
    SELECT 1 FROM Applications a WHERE a.StudentId = s.StudentId
);

-- ------------------------------------------------------------
-- 3. Insert Registrations for each new Application
-- ------------------------------------------------------------
INSERT INTO Registrations (AppId, StudentId, GradeEnrolling, Status, CreatedAt, Notes)
SELECT 
    a.AppId,
    a.StudentId,
    a.GradeApplying,
    1,  -- status: 1 = Pending/New
    GETDATE(),
    'Registration from application'
FROM Applications a
WHERE NOT EXISTS (
    SELECT 1 FROM Registrations r WHERE r.AppId = a.AppId
);

-- ------------------------------------------------------------
-- 4. Insert Invoices for each Registration
-- ------------------------------------------------------------
-- Generate invoice numbers
INSERT INTO Invoices (InvoiceNumber, RegistrationId, StudentId, ParentId, InvoiceType, Amount, Description, CreatedDate, DueDate, Status)
SELECT 
    'INV-' + CAST(r.RegistrationId AS VARCHAR) + '-' + FORMAT(GETDATE(), 'yyyyMMdd'),
    r.RegistrationId,
    r.StudentId,
    a.ParentId,
    'Registration',
    5000.00,   -- example amount
    'Registration fee for student ' + s.FirstName + ' ' + s.LastName,
    GETDATE(),
    DATEADD(month, 1, GETDATE()),
    'Pending'
FROM Registrations r
INNER JOIN Applications a ON r.AppId = a.AppId
INNER JOIN Students s ON r.StudentId = s.StudentId
WHERE NOT EXISTS (
    SELECT 1 FROM Invoices i WHERE i.RegistrationId = r.RegistrationId
);

SELECT '? Application data seeded.' AS Status;





select * from VisitorAccessRequests;