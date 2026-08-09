CREATE TABLE dbo.TripVehicleAssignments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TripScheduleId INT NOT NULL,
    VehicleId INT NOT NULL,
    DriverId INT NOT NULL,
    AllocatedSeats INT NOT NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_TripVehicleAssignments_CreatedAt DEFAULT GETDATE(),
    
    CONSTRAINT FK_TripVehicleAssignments_TripSchedule 
        FOREIGN KEY (TripScheduleId) REFERENCES dbo.TripSchedules(Id),
    CONSTRAINT FK_TripVehicleAssignments_Vehicle 
        FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(Id),
    CONSTRAINT FK_TripVehicleAssignments_Driver 
        FOREIGN KEY (DriverId) REFERENCES dbo.Drivers(Id)
);
SELECT COUNT(*) AS Drivers FROM Drivers;
SELECT COUNT(*) AS Vehicles FROM Vehicles;
SELECT COUNT(*) AS TripRequests FROM TripRequests;
SELECT COUNT(*) AS TripSchedules FROM TripSchedules;
SELECT COUNT(*) AS TripStudents FROM TripStudents;
SELECT COUNT(*) AS TripVehicleAssignments FROM TripVehicleAssignments;

SELECT UserId, Name, Email, Role FROM AppUsers WHERE Role LIKE '%Reporter%';

UPDATE AppUsers SET Role = 'FaultReporter' WHERE Role = 'MaintenanceReporter';