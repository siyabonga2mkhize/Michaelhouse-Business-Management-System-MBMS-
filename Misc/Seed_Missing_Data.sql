-- ============================================================
-- 1. Insert Residences (if not exist) – unchanged
-- ============================================================
MERGE INTO Residences AS target
USING (VALUES 
    ('Founders House', 'Boys', 50, 0, 'Grade 8-10', 1, 1, 0),
    ('Baines House',   'Boys', 50, 0, 'Grade 8-10', 1, 1, 0),
    ('Tatham House',   'Boys', 50, 0, 'Grade 8-10', 1, 1, 0),
    ('Churchill House','Boys', 50, 0, 'Grade 8-10', 1, 1, 0)
) AS source (Name, Gender, Capacity, OccupiedBeds, GradeCategory, NearMedicalFacility, NearHouseMasterOffice, IsArchived)
ON target.Name = source.Name
WHEN NOT MATCHED THEN
    INSERT (Name, Gender, Capacity, OccupiedBeds, GradeCategory, NearMedicalFacility, NearHouseMasterOffice, IsArchived)
    VALUES (source.Name, source.Gender, source.Capacity, source.OccupiedBeds, source.GradeCategory, 
            source.NearMedicalFacility, source.NearHouseMasterOffice, source.IsArchived);

-- ============================================================
-- 2. Insert Rooms – NOW 4 ROOMS PER RESIDENCE (instead of 2)
-- ============================================================
DECLARE @ResidenceId int;
DECLARE @ResidenceName nvarchar(200);
DECLARE @RoomNumber nvarchar(50);
DECLARE @RoomCounter int = 1;

DECLARE residence_cursor CURSOR FOR 
    SELECT ResidenceId, Name FROM Residences WHERE IsArchived = 0;

OPEN residence_cursor;
FETCH NEXT FROM residence_cursor INTO @ResidenceId, @ResidenceName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @RoomCounter = 1;
    WHILE @RoomCounter <= 4   -- <-- changed from 2 to 4
    BEGIN
        SET @RoomNumber = LEFT(@ResidenceName, 1) + CAST(@RoomCounter AS nvarchar(10));
        IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = @RoomNumber AND ResidenceId = @ResidenceId)
        BEGIN
            INSERT INTO Rooms (RoomNumber, Capacity, OccupiedBeds, IsFull, Floor, IsGroundFloor, IsWheelchairAccessible, 
                               NearBathroom, IsQuietStudyRoom, NeedsMaintenance, ResidenceId, IsArchived)
            VALUES (@RoomNumber, 2, 0, 0, 1, 1, 1, 1, 1, 0, @ResidenceId, 0);
        END
        SET @RoomCounter = @RoomCounter + 1;
    END
    FETCH NEXT FROM residence_cursor INTO @ResidenceId, @ResidenceName;
END
CLOSE residence_cursor;
DEALLOCATE residence_cursor;

-- ============================================================
-- 3. Insert Beds (2 per room) – unchanged
-- ============================================================
DECLARE @RoomId int;
DECLARE @BedLabel char(1);
DECLARE @BedCounter int = 1;

DECLARE room_cursor CURSOR FOR 
    SELECT RoomId FROM Rooms WHERE IsArchived = 0;

OPEN room_cursor;
FETCH NEXT FROM room_cursor INTO @RoomId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @BedCounter = 1;
    WHILE @BedCounter <= 2
    BEGIN
        SET @BedLabel = CHAR(64 + @BedCounter); -- A, B
        IF NOT EXISTS (SELECT 1 FROM Beds WHERE BedNumber = CAST(@RoomId AS nvarchar(10)) + '-' + @BedLabel AND RoomId = @RoomId)
        BEGIN
            INSERT INTO Beds (RoomId, BedNumber, IsOccupied, Status, IsArchived)
            VALUES (@RoomId, CAST(@RoomId AS nvarchar(10)) + '-' + @BedLabel, 0, 'Available', 0);
        END
        SET @BedCounter = @BedCounter + 1;
    END
    FETCH NEXT FROM room_cursor INTO @RoomId;
END
CLOSE room_cursor;
DEALLOCATE room_cursor;

-- ============================================================
-- 4. Insert HouseMasters – unchanged
-- ============================================================
MERGE INTO HouseMasters AS target
USING (VALUES 
    ('Mr James Harrington', 'founders.hm@michaelhouse.co.za', '0331234001', (SELECT ResidenceId FROM Residences WHERE Name = 'Founders House'), 0),
    ('Ms Nomvula Dlamini', 'baines.hm@michaelhouse.co.za', '0331234002', (SELECT ResidenceId FROM Residences WHERE Name = 'Baines House'), 0),
    ('Mr Andrew Naidoo', 'tatham.hm@michaelhouse.co.za', '0331234003', (SELECT ResidenceId FROM Residences WHERE Name = 'Tatham House'), 0),
    ('Ms Sarah Mokoena', 'churchill.hm@michaelhouse.co.za', '0331234004', (SELECT ResidenceId FROM Residences WHERE Name = 'Churchill House'), 0)
) AS source (FullName, ContactEmail, ContactPhone, ResidenceId, IsArchived)
ON target.ContactEmail = source.ContactEmail
WHEN NOT MATCHED THEN
    INSERT (FullName, ContactEmail, ContactPhone, ResidenceId, IsArchived)
    VALUES (source.FullName, source.ContactEmail, source.ContactPhone, source.ResidenceId, source.IsArchived);

UPDATE r
SET r.HouseMasterId = hm.HouseMasterId
FROM Residences r
INNER JOIN HouseMasters hm ON r.ResidenceId = hm.ResidenceId
WHERE r.HouseMasterId IS NULL;

-- ============================================================
-- 5. Assign boarding students to beds – improved to fill all beds
-- ============================================================
WITH BoardingStudents AS (
    SELECT 
        s.StudentId,
        ROW_NUMBER() OVER (ORDER BY s.StudentId) AS RowNum
    FROM Students s
    WHERE s.IsBoarding = 1
      AND NOT EXISTS (SELECT 1 FROM ResidenceAllocations ra WHERE ra.StudentId = s.StudentId AND ra.IsActive = 1)
),
AvailableBeds AS (
    SELECT 
        b.BedId,
        b.RoomId,
        r.ResidenceId,
        ROW_NUMBER() OVER (ORDER BY r.ResidenceId, b.BedId) AS BedSeq
    FROM Beds b
    INNER JOIN Rooms ro ON b.RoomId = ro.RoomId
    INNER JOIN Residences r ON ro.ResidenceId = r.ResidenceId
    WHERE b.IsOccupied = 0 AND b.IsArchived = 0
)
INSERT INTO ResidenceAllocations (StudentId, ResidenceId, RoomId, BedId, IsActive, AllocatedAt, CreatedBy, CreatedAt, IsArchived)
SELECT 
    bs.StudentId,
    ab.ResidenceId,
    ab.RoomId,
    ab.BedId,
    1,
    GETDATE(),
    'Seeder',
    GETDATE(),
    0
FROM BoardingStudents bs
INNER JOIN AvailableBeds ab ON bs.RowNum = ab.BedSeq   -- simple 1-to-1 mapping
WHERE ab.BedSeq <= (SELECT COUNT(*) FROM BoardingStudents);  -- only assign if bed exists

-- Mark beds as occupied
UPDATE b
SET b.IsOccupied = 1,
    b.OccupiedByStudentId = ra.StudentId
FROM Beds b
INNER JOIN ResidenceAllocations ra ON b.BedId = ra.BedId
WHERE ra.IsActive = 1;

-- Update occupancy counts
UPDATE r SET r.OccupiedBeds = (SELECT COUNT(*) FROM Beds b WHERE b.RoomId = r.RoomId AND b.IsOccupied = 1)
FROM Rooms r;

UPDATE res SET res.OccupiedBeds = (SELECT COUNT(*) FROM Rooms ro WHERE ro.ResidenceId = res.ResidenceId AND ro.OccupiedBeds > 0)
FROM Residences res;

-- ============================================================
-- 6. Generate QR Codes for all students
-- ============================================================
INSERT INTO StudentQRCodes (StudentId, QRCodeValue, QRImage, DateGenerated, IsActive)
SELECT 
    s.StudentId,
    CONCAT('QR-', s.StudentId, '-', FORMAT(GETDATE(), 'yyyyMMddHHmmss')),
    NULL,
    GETDATE(),
    1
FROM Students s
WHERE NOT EXISTS (SELECT 1 FROM StudentQRCodes sq WHERE sq.StudentId = s.StudentId);

-- ============================================================
-- 7. Seed TermCalendars
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM TermCalendars WHERE EventTitle LIKE '%Closed Weekend%')
BEGIN
    INSERT INTO TermCalendars (EventTitle, StartDate, EndDate, IsClosedWeekend)
    VALUES 
        ('Closed Weekend - Aug', '2026-08-22', '2026-08-23', 1),
        ('Closed Weekend - Sep', '2026-09-12', '2026-09-13', 1);
END

SELECT '✅ Missing data seeded successfully.' AS Status;

-- Students with active allocations
SELECT COUNT(*) AS AllocatedStudents FROM ResidenceAllocations WHERE IsActive = 1;

-- Total available beds (should be > students)
SELECT COUNT(*) AS TotalBeds FROM Beds WHERE IsOccupied = 0 AND IsArchived = 0;

-- QR codes generated
SELECT COUNT(*) AS QRCodes FROM StudentQRCodes;







-- ============================================================
-- SEED APPLICATIONS FOR EXISTING STUDENTS (LINK TO PARENTS)
-- ============================================================

INSERT INTO Applications (
    ParentId,
    StudentId,
    Date,
    ApplicationYear,
    GradeApplying,
    Status,
    AiReviewSummary,
    AiRecommendation,
    AdditionalNotes
)
SELECT 
    s.ParentId,
    s.StudentId,
    GETDATE(),
    YEAR(GETDATE()),          -- current year
    s.GradeLevel,             -- applying for current grade
    CASE 
        WHEN s.StudentId % 3 = 0 THEN 2   -- Approved
        WHEN s.StudentId % 3 = 1 THEN 1   -- Pending
        ELSE 3                            -- Rejected
    END AS Status,
    'AI review summary placeholder',
    'AI recommendation placeholder',
    'Additional notes placeholder'
FROM Students s
WHERE s.ParentId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 
      FROM Applications a 
      WHERE a.StudentId = s.StudentId
  );


 

select * from Applications;

select * from Invoices;
