-- ============================================
-- Test Data for School Management System
-- With IDENTITY_INSERT handling
-- ============================================

SET DATEFORMAT ymd;
GO

-- 1. AppUsers
SET IDENTITY_INSERT AppUsers ON;
INSERT INTO AppUsers (UserId, Name, Email, PasswordHash, Role)
VALUES
(1, 'Admin User', 'admin@school.edu', 'hash_admin123', 'Admin'),
(2, 'John Smith', 'john.smith@school.edu', 'hash_teacher456', 'Teacher'),
(3, 'Mary Johnson', 'mary.johnson@school.edu', 'hash_teacher789', 'Teacher'),
(4, 'Peter Parent', 'peter.parent@email.com', 'hash_parent123', 'Parent'),
(5, 'Susan Parent', 'susan.parent@email.com', 'hash_parent456', 'Parent'),
(6, 'Lisa Student', 'lisa.student@email.com', 'hash_student123', 'Student'),
(7, 'Mark Student', 'mark.student@email.com', 'hash_student456', 'Student'),
(8, 'Driver One', 'driver1@school.edu', 'hash_driver123', 'Driver'),
(9, 'Driver Two', 'driver2@school.edu', 'hash_driver456', 'Driver');
SET IDENTITY_INSERT AppUsers OFF;
GO

-- 2. Parents
SET IDENTITY_INSERT Parents ON;
INSERT INTO Parents (ParentId, UserId, Name, Contact, CellPhone, WorkPhone, HomePhone, PhysicalAddress, PostalAddress, Relationship, Occupation, Employer, EmergencyContactName, EmergencyContactPhone)
VALUES
(1, 4, 'Peter Parent', 'peter.parent@email.com', '+1234567890', '+1234567891', '+1234567892', '123 Main St, City', 'PO Box 123', 'Father', 'Engineer', 'Tech Corp', 'Susan Parent', '+1234567893'),
(2, 5, 'Susan Parent', 'susan.parent@email.com', '+1234567894', '+1234567895', '+1234567896', '456 Oak Ave, City', 'PO Box 456', 'Mother', 'Doctor', 'City Hospital', 'Peter Parent', '+1234567897');
SET IDENTITY_INSERT Parents OFF;
GO

-- 3. Students
SET IDENTITY_INSERT Students ON;
INSERT INTO Students (StudentId, FirstName, LastName, DOB, HomeLanguage, IdNumber, PreviousSchool, CurrentGrade, MedicalConditions, ParentId, UserId, StudentNumber, Gender, GradeLevel, EnrollmentDate, ClassId)
VALUES
(1, 'Lisa', 'Student', '2012-05-15', 'English', 'ID123456', 'Sunrise Elementary', 'Grade 6', 'None', 1, 6, 'S20240001', 'Female', 6, '2024-01-15', NULL),
(2, 'Mark', 'Student', '2011-09-22', 'English', 'ID123457', 'Sunrise Elementary', 'Grade 7', 'Asthma', 2, 7, 'S20240002', 'Male', 7, '2024-01-15', NULL);
SET IDENTITY_INSERT Students OFF;
GO

-- 4. Teachers
SET IDENTITY_INSERT Teachers ON;
INSERT INTO Teachers (TeacherId, FirstName, LastName, Email, Phone, Specialization, HireDate, UserId)
VALUES
(1, 'John', 'Smith', 'john.smith@school.edu', '+1234567801', 'Mathematics', '2020-08-01', 2),
(2, 'Mary', 'Johnson', 'mary.johnson@school.edu', '+1234567802', 'Science', '2019-08-01', 3);
SET IDENTITY_INSERT Teachers OFF;
GO

-- 5. Drivers
SET IDENTITY_INSERT Drivers ON;
INSERT INTO Drivers (Id, FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, IsActive, DateCreated, PasswordHash, UserId, ImageUrl)
VALUES
(1, 'Driver One', 'DRV123456', '+1234567810', 'driver1@school.edu', 'LIC123456', '2027-12-31', 1, 1, '2024-01-01', 'hash_driver123', 8, NULL),
(2, 'Driver Two', 'DRV123457', '+1234567811', 'driver2@school.edu', 'LIC123457', '2026-10-20', 0, 1, '2024-01-01', 'hash_driver456', 9, NULL);
SET IDENTITY_INSERT Drivers OFF;
GO

-- 6. Subjects
SET IDENTITY_INSERT Subjects ON;
INSERT INTO Subjects (SubjectId, Name, Code, Stream, IsCompulsory, IsLanguage, IsMathsOption, RequiresMaths, ApplicableGrades, SortOrder, GradeLevel, TeacherId)
VALUES
(1, 'Mathematics', 'MATH101', 1, 1, 0, 1, 0, '6,7', 1, 6, 1),
(2, 'English', 'ENG101', 1, 1, 1, 0, 0, '6,7', 2, 6, NULL),
(3, 'General Science', 'SCI101', 1, 1, 0, 0, 0, '6,7', 3, 6, 2);
SET IDENTITY_INSERT Subjects OFF;
GO

-- 7. Applications
SET IDENTITY_INSERT Applications ON;
INSERT INTO Applications (AppId, ParentId, StudentId, Date, ApplicationYear, GradeApplying, Status, AiReviewSummary, AiRecommendation, AdditionalNotes)
VALUES
(1, 1, 1, '2024-01-10', 2024, 6, 1, 'Strong candidate', 'Accept', 'Excels in math'),
(2, 2, 2, '2024-01-12', 2024, 7, 2, 'Good but needs support', 'Conditional', 'Medical condition noted');
SET IDENTITY_INSERT Applications OFF;
GO

-- 8. AdminReviews
SET IDENTITY_INSERT AdminReviews ON;
INSERT INTO AdminReviews (ReviewId, AppId, DriverAppId, AdminId, Date, Decision, AdminNotes, AgreedWithAi)
VALUES
(1, 1, NULL, 'admin@school.edu', '2024-01-15', 'Approved', 'Proceed with enrollment', 1),
(2, 2, NULL, 'admin@school.edu', '2024-01-16', 'Conditional', 'Request additional medical info', 0);
SET IDENTITY_INSERT AdminReviews OFF;
GO

-- 9. DriverApplications
SET IDENTITY_INSERT DriverApplications ON;
INSERT INTO DriverApplications (Id, FullName, IDNumber, PhoneNumber, Email, LicenceNumber, LicenceExpiryDate, HasPDP, DocumentPath, Status, AdminNotes, DateSubmitted, ReviewedDate, UserId, PublicTokenHash, PublicTokenExpiry, InterviewDateTime, InterviewMeetingLink, InterviewEmailSent, InterviewEmailSentAt)
VALUES
(1, 'Driver One', 'DRV123456', '+1234567810', 'driver1@school.edu', 'LIC123456', '2027-12-31', 1, '/docs/driver1.pdf', 'Approved', 'Good driving record', '2024-01-05', '2024-01-10', 8, NULL, NULL, NULL, NULL, 0, NULL);
SET IDENTITY_INSERT DriverApplications OFF;
GO

-- 10. DriverAvailabilities
SET IDENTITY_INSERT DriverAvailabilities ON;
INSERT INTO DriverAvailabilities (Id, DriverId, StartDate, EndDate, Reason, DateCreated)
VALUES
(1, 1, '2024-05-20', '2024-05-25', 'Vacation', GETDATE());
SET IDENTITY_INSERT DriverAvailabilities OFF;
GO

-- 11. DriverDocuments
SET IDENTITY_INSERT DriverDocuments ON;
INSERT INTO DriverDocuments (Id, DriverApplicationId, FilePath, DocumentType, OtherDocumentType)
VALUES
(1, 1, '/docs/license_driver1.pdf', 'License', NULL);
SET IDENTITY_INSERT DriverDocuments OFF;
GO

-- 12. Assessments
SET IDENTITY_INSERT Assessments ON;
INSERT INTO Assessments (AssessmentId, TeacherId, SubjectId, Grade, Stream, Title, AssessmentType, Term, AcademicYear, ScheduledDate, TotalMarks, WeightingPercent, Notes, MarksCaptureClosed, CreatedAt)
VALUES
(1, 1, 1, 6, 1, 'Math Quiz 1', 'Quiz', 1, 2024, '2024-02-10', 50, 10.00, 'Basic algebra', 0, GETDATE()),
(2, 2, 3, 6, 1, 'Science Lab Report', 'Assignment', 1, 2024, '2024-02-15', 100, 20.00, 'Physics experiment', 0, GETDATE());
SET IDENTITY_INSERT Assessments OFF;
GO

-- 13. Attendances
SET IDENTITY_INSERT Attendances ON;
INSERT INTO Attendances (AttendanceId, Date, Status, StudentId, SubjectId, RecordedBy)
VALUES
(1, '2024-02-01', 1, 1, 1, 'John Smith'),
(2, '2024-02-01', 0, 2, 2, 'Mary Johnson');
SET IDENTITY_INSERT Attendances OFF;
GO

-- 14. Categories
SET IDENTITY_INSERT Categories ON;
INSERT INTO Categories (Id, Name, Description)
VALUES
(1, 'Uniform', 'School uniforms and apparel'),
(2, 'Books', 'Textbooks and workbooks');
SET IDENTITY_INSERT Categories OFF;
GO

-- 15. Products
SET IDENTITY_INSERT Products ON;
INSERT INTO Products (Id, Name, Description, Price, QuantityInStock, ReorderLevel, ImageUrl, IsActive, CategoryId)
VALUES
(1, 'School T-Shirt', 'Cotton t-shirt with logo', 15.99, 200, 50, '/images/tshirt.jpg', 1, 1),
(2, 'Math Grade 6 Textbook', 'Official textbook', 45.00, 100, 20, '/images/math6.jpg', 1, 2);
SET IDENTITY_INSERT Products OFF;
GO

-- 16. Orders
SET IDENTITY_INSERT Orders ON;
INSERT INTO Orders (Id, OrderNumber, CustomerEmail, CustomerName, OrderDate, TotalAmount, Status, Notes)
VALUES
(1, 'ORD-001', 'peter.parent@email.com', 'Peter Parent', '2024-01-20', 61.98, 'Completed', 'Order for Lisa');
SET IDENTITY_INSERT Orders OFF;
GO

-- OrderItems
SET IDENTITY_INSERT OrderItems ON;
INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice)
VALUES
(1, 1, 1, 2, 15.99),
(2, 1, 2, 1, 45.00);
SET IDENTITY_INSERT OrderItems OFF;
GO

-- 17. Registrations
SET IDENTITY_INSERT Registrations ON;
INSERT INTO Registrations (RegistrationId, AppId, StudentId, GradeEnrolling, Status, CreatedAt, CompletedAt, Notes)
VALUES
(1, 1, 1, 6, 1, '2024-01-15', '2024-01-20', 'Fully enrolled');
SET IDENTITY_INSERT Registrations OFF;
GO

-- Invoices
SET IDENTITY_INSERT Invoices ON;
INSERT INTO Invoices (InvoiceId, InvoiceNumber, RegistrationId, StudentId, ParentId, InvoiceType, Amount, Description, CreatedDate, DueDate, Status)
VALUES
(1, 'INV-2024-001', 1, 1, 1, 'Tuition', 2500.00, 'Term 1 tuition', '2024-01-20', '2024-02-10', 'Sent');
SET IDENTITY_INSERT Invoices OFF;
GO

-- 18. Payments
SET IDENTITY_INSERT Payments ON;
INSERT INTO Payments (PaymentId, InvoiceId, AmountPaid, PaymentDate, StripeChargeId, StripePaymentIntentId, Status, PaymentReference, ProofEmailSent)
VALUES
(1, 1, 2500.00, '2024-01-25', 'ch_123', 'pi_123', 'Completed', 'REF-001', 1);
SET IDENTITY_INSERT Payments OFF;
GO

-- 19. Periods
SET IDENTITY_INSERT Periods ON;
INSERT INTO Periods (PeriodId, PeriodNumber, StartTime, EndTime, Label, IsBreak)
VALUES
(1, 1, '08:00:00', '08:45:00', 'Period 1', 0),
(2, 2, '08:50:00', '09:35:00', 'Period 2', 0);
SET IDENTITY_INSERT Periods OFF;
GO

-- 20. TimetableSlots
SET IDENTITY_INSERT TimetableSlots ON;
INSERT INTO TimetableSlots (SlotId, AcademicYear, DayOfWeek, PeriodId, TeacherId, SubjectId, Grade, Stream, Subject_SubjectId)
VALUES
(1, 2024, 1, 1, 1, 1, 6, 1, NULL);
SET IDENTITY_INSERT TimetableSlots OFF;
GO

-- 21. StreamEnrolments
SET IDENTITY_INSERT StreamEnrolments ON;
INSERT INTO StreamEnrolments (StreamEnrolmentId, StudentId, TeacherId, RegistrationId, Grade, Stream, TakesMathematics, EnrolledAt)
VALUES
(1, 1, 1, 1, 6, 1, 1, GETDATE());
SET IDENTITY_INSERT StreamEnrolments OFF;
GO

-- 22. StudentMarks
SET IDENTITY_INSERT StudentMarks ON;
INSERT INTO StudentMarks (StudentMarkId, AssessmentmentId, StudentId, MarksObtained, IsAbsent, TeacherComment, CapturedAt, Subject_SubjectId)
VALUES
(1, 1, 1, 42.5, 0, 'Good work', GETDATE(), 1);
SET IDENTITY_INSERT StudentMarks OFF;
GO

-- 23. StudentSubjects
SET IDENTITY_INSERT StudentSubjects ON;
INSERT INTO StudentSubjects (StudentSubjectId, StudentId, SubjectId, Stream, IsCompulsory)
VALUES
(1, 1, 1, 1, 1),
(2, 1, 2, 1, 1);
SET IDENTITY_INSERT StudentSubjects OFF;
GO

-- 24. Suppliers
SET IDENTITY_INSERT Suppliers ON;
INSERT INTO Suppliers (SupplierId, Name, ContactPerson, Email, Phone, Address, Website, PaymentTermsDays, IsActive, CreatedAt)
VALUES
(1, 'Book Suppliers Inc.', 'John Books', 'orders@booksuppliers.com', '+1234567899', '123 Publisher Lane', 'www.booksuppliers.com', 30, 1, GETDATE());
SET IDENTITY_INSERT Suppliers OFF;
GO

-- SupplierProducts
SET IDENTITY_INSERT SupplierProducts ON;
INSERT INTO SupplierProducts (SupplierProductId, SupplierId, ProductId, UnitCost, SupplierSku, IsPreferred, MinOrderQty, LeadTimeDays)
VALUES
(1, 1, 2, 35.00, 'MATH6-001', 1, 10, 5);
SET IDENTITY_INSERT SupplierProducts OFF;
GO

-- PurchaseOrders
SET IDENTITY_INSERT PurchaseOrders ON;
INSERT INTO PurchaseOrders (PurchaseOrderId, PoNumber, SupplierId, Status, CreatedAt, SentAt, ReceivedAt, ExpectedDelivery, Notes, EmailSent)
VALUES
(1, 'PO-001', 1, 1, GETDATE(), NULL, NULL, '2024-03-01', 'Back to school order', 0);
SET IDENTITY_INSERT PurchaseOrders OFF;
GO

-- PurchaseOrderLines
SET IDENTITY_INSERT PurchaseOrderLines ON;
INSERT INTO PurchaseOrderLines (PurchaseOrderLineId, PurchaseOrderId, ProductId, QuantityOrdered, UnitCost, QuantityReceived)
VALUES
(1, 1, 2, 50, 35.00, 0);
SET IDENTITY_INSERT PurchaseOrderLines OFF;
GO

-- 25. StockMovements
SET IDENTITY_INSERT StockMovements ON;
INSERT INTO StockMovements (StockMovementId, ProductId, MovementType, Quantity, StockAfter, Reference, Notes, CreatedAt)
VALUES
(1, 2, 1, 100, 200, 'Initial stock', 'Received from supplier', GETDATE());
SET IDENTITY_INSERT StockMovements OFF;
GO

-- 26. TeacherAttendances
SET IDENTITY_INSERT TeacherAttendances ON;
INSERT INTO TeacherAttendances (TeacherAttendanceId, Date, SignInTime, SignOutTime, Status, IsVerified, TeacherId)
VALUES
(1, '2024-02-01', '2024-02-01 07:55:00', '2024-02-01 15:05:00', 1, 1, 1);
SET IDENTITY_INSERT TeacherAttendances OFF;
GO

-- 27. TeacherSubjectGrades
SET IDENTITY_INSERT TeacherSubjectGrades ON;
INSERT INTO TeacherSubjectGrades (TeacherSubjectGradeId, TeacherId, SubjectId, Grade, Stream)
VALUES
(1, 1, 1, 6, 1);
SET IDENTITY_INSERT TeacherSubjectGrades OFF;
GO

-- 28. TermResults
SET IDENTITY_INSERT TermResults ON;
INSERT INTO TermResults (TermResultId, StudentId, SubjectId, Grade, Term, AcademicYear, TermMarkPercent, Symbol, Passed, CalculatedAt)
VALUES
(1, 1, 1, 6, 1, 2024, 85.5, 'B+', 1, GETDATE());
SET IDENTITY_INSERT TermResults OFF;
GO

-- YearResults
SET IDENTITY_INSERT YearResults ON;
INSERT INTO YearResults (YearResultId, StudentId, Grade, AcademicYear, OverallPercent, PromotionStatus, AdminNotes, CalculatedAt)
VALUES
(1, 1, 6, 2024, 85.5, 'Promoted', 'Good performance', GETDATE());
SET IDENTITY_INSERT YearResults OFF;
GO

-- 29. Vehicles
SET IDENTITY_INSERT Vehicles ON;
INSERT INTO Vehicles (Id, VehicleNumber, Model, Type, Capacity, IsActive, DateAdded, ImageUrl)
VALUES
(1, 'BUS-001', 'Toyota Hiace', 'Bus', 30, 1, '2024-01-01', NULL);
SET IDENTITY_INSERT Vehicles OFF;
GO

-- TripRequests
SET IDENTITY_INSERT TripRequests ON;
INSERT INTO TripRequests (Id, TeacherId, Title, Description, DepartureTime, ReturnTime, Destination, MaxStudents, Status, RejectionReason, RequestedAt, ApprovedByAdminId, ApprovedAt, DestinationLat, DestinationLng, RebookedFromId)
VALUES
(1, 1, 'Maths Olympiad', 'Trip to city maths contest', '2024-03-10 08:00:00', '2024-03-10 16:00:00', 'City Convention Centre', 30, 'Approved', NULL, GETDATE(), 1, GETDATE(), -1.2921, 36.8219, NULL);
SET IDENTITY_INSERT TripRequests OFF;
GO

-- TripSchedules
SET IDENTITY_INSERT TripSchedules ON;
INSERT INTO TripSchedules (Id, TripRequestId, TeacherId, ScheduledDate, Status, DriverId, VehicleId, Notes)
VALUES
(1, 1, 1, '2024-03-10', 'Scheduled', 1, 1, 'Leave at 7:30 AM');
SET IDENTITY_INSERT TripSchedules OFF;
GO

-- TripStudents
SET IDENTITY_INSERT TripStudents ON;
INSERT INTO TripStudents (Id, TripScheduleId, StudentId, IsPresentBefore, IsPresentAfter, MarkedBeforeBy, MarkedAfterBy, MarkedBeforeAt, MarkedAfterAt)
VALUES
(1, 1, 1, 1, NULL, 'John Smith', NULL, '2024-03-10 07:45:00', NULL);
SET IDENTITY_INSERT TripStudents OFF;
GO

-- TripVehicleAssignments
SET IDENTITY_INSERT TripVehicleAssignments ON;
INSERT INTO TripVehicleAssignments (Id, TripScheduleId, VehicleId, DriverId, AllocatedSeats, CreatedAt)
VALUES
(1, 1, 1, 1, 25, GETDATE());
SET IDENTITY_INSERT TripVehicleAssignments OFF;
GO

-- 30. VehicleIssues
SET IDENTITY_INSERT VehicleIssues ON;
INSERT INTO VehicleIssues (Id, DriverId, VehicleId, Description, DateReported, Status)
VALUES
(1, 1, 1, 'Check engine light', '2024-02-01', 'Pending');
SET IDENTITY_INSERT VehicleIssues OFF;
GO

-- 31. Notifications
SET IDENTITY_INSERT Notifications ON;
INSERT INTO Notifications (Id, UserId, Message, IsRead, CreatedAt, RelatedEntityType, RelatedEntityId)
VALUES
(1, 1, 'New trip request from John Smith', 0, GETDATE(), 'TripRequest', 1);
SET IDENTITY_INSERT Notifications OFF;
GO

-- 32. StudentAttendanceTokens
SET IDENTITY_INSERT StudentAttendanceTokens ON;
INSERT INTO StudentAttendanceTokens (Id, StudentId, TripScheduleId, Token, Type, IsUsed, CreatedAt, ExpiryDate)
VALUES
(1, 1, 1, 'TOKEN-ABC123', 'Boarding', 0, GETDATE(), DATEADD(day, 1, GETDATE()));
SET IDENTITY_INSERT StudentAttendanceTokens OFF;
GO

-- End of Test Data Script