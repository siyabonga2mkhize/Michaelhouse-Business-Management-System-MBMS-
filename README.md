# [Michaelhouse Business Management System (MBMS)](https://mbms-ewbjhzh7fzdnd2d0.southafricanorth-01.azurewebsites.net/)


Welcome to the **Michaelhouse MBMS** – a comprehensive school management system built with ASP.NET MVC 5, Entity Framework, and Azure SQL.

---

## 📦 Quick Start

1. **Clone or download** the repository.
2. **Open** the solution in Visual Studio 2019/2022.
3. **Build** the solution (NuGet packages will be restored automatically).
4. **Update the database** (if needed) – see *Database Setup* below.
5. **Run** the application (F5 or Ctrl+F5).

> ⚠️ **Important**: The application connects to an Azure SQL database. Ensure your IP is whitelisted in the Azure SQL firewall (see *Troubleshooting*).

---

## 🗄️ Database Setup

The connection string is configured in `Web.config` to point to an Azure SQL database:

```xml
<add name="MichaelHouseDb" 
     connectionString="Server=tcp:mbms.database.windows.net,1433;Initial Catalog=schooldb;Persist Security Info=False;User ID=schooladmin;Password=admin@123;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;" 
     providerName="System.Data.SqlClient" />
```

If you need to run locally against a different database, update the connection string accordingly.

**Seed Data**: The application automatically seeds some default data on first run (drivers, residences, rooms, beds, and sample term calendars). Additional seed scripts (`SQLQuery.sql`) can be run manually to populate parents, students, teachers, and more.

---

## 🔐 Default Login Credentials

All seeded accounts use a **default password** unless noted otherwise. **You are strongly advised to change these passwords after the first login.**

Below is a complete list of seeded users and their credentials.

---

### 👑 Administrator

| Name            | Email                           | Password    |
|-----------------|---------------------------------|-------------|
| System Admin    | `admin@michaelhouse.co.za`      | `qwerty` |

---

### 👨‍🏫 Teachers

| Name          | Email                           | Password    |
|---------------|---------------------------------|-------------|
| John Staff    | `j.staff@michaelhouse.org`      | `??` |

---

### 👨‍👩‍👧‍👦 Parents

All parents use their contact email as the login. The password for **all parents** is `qwerty`.

| Name               | Email                         |
|--------------------|-------------------------------|
| Thabo Parent       | `thabo.parent@demo.com`       |
| Lerato Parent      | `lerato.parent@demo.com`      |
| Sipho Parent       | `sipho.parent@demo.com`       |
| Zanele Parent      | `zanele.parent@demo.com`      |
| Demo Parent        | `parent@demo.com`             |
| Busi Naidoo        | `busi.n@gmail.com`            |
| Johannes Steyn     | `jsteyn@mweb.co.za`           |
| Lindiwe Mazibuko   | `lindi.mazi@outlook.com`      |
| David Mokwena      | `dmokwena@work.co.za`         |
| Sarah Pillay       | `spillay@fnb.co.za`           |
| Nomvula Zuma       | `nomvula.z@webmail.co.za`     |
| Kevin Naidoo       | `k.naidoo@telkomsa.net`       |

---

### 🎓 Students

Students can log in using the email pattern: `firstname.lastname@student.michaelhouse.org` – password is `qwerty` for **all students**.

**Examples:**
- Thabo Nkosi → `thabo.nkosi@student.michaelhouse.org`
- Lerato Molefe → `lerato.molefe@student.michaelhouse.org`
- … (all 30+ seeded students follow the same pattern)

---

### 🚗 Drivers

All drivers have the password `123456` (this differs from the default).

| Name               | Email                           |
|--------------------|---------------------------------|
| Sibusiso Mthembu   | `sibusiso@michaelhouse.com`     |
| Thabo Khumalo      | `thabo@michaelhouse.com`        |
| Mandla Dlamini     | `mandla@michaelhouse.com`       |
| Sipho Zulu         | `sipho@michaelhouse.com`        |
| Themba Nkosi       | `themba@michaelhouse.co.za`     |
| Thabo Mkhize       | `thabo@michaelhouse.co.za`      |
| Andile Zulu        | `andile@michaelhouse.co.za`     |
| Nkosi Khumalo      | `nkosi@michaelhouse.co.za`      |

---

### 🛠️ Managers

| Role                | Name              | Email                           | Password    |
|---------------------|-------------------|---------------------------------|-------------|
| Transport Manager   | Greg Johnson      | `transport@michaelhouse.co.za`  | `Transport@123` |
| Inventory Manager   | Andre Van Kok     | `inventory@michaelhouse.co.za`  | `Stock@123` |
| Maintenance Manager | Mr. James Mokoena | `j.mokoena@michaelhouse.org`    | `Manager@123` |

---

### 🧹 Maintenance Workers

All workers have the password `Worker@123`.

| Name               | Email                           |
|--------------------|---------------------------------|
| Sipho Dlamini      | `s.dlamini@michaelhouse.org`    |
| Bongani Nkosi      | `b.nkosi@michaelhouse.org`      |
| Eric Mthembu       | `e.mthembu@michaelhouse.org`    |
| Thabo Zulu         | `t.zulu@michaelhouse.org`       |
| Lungelo Mbatha     | `l.mbatha@michaelhouse.org`     |
| Sifiso Khumalo     | `s.khumalo@michaelhouse.org`    |
| Nhlanhla Mokoena   | `n.mokoena@michaelhouse.org`    |
| Mandla Cele        | `m.cele@michaelhouse.org`       |
| Sandile Ntanzi     | `s.ntanzi@michaelhouse.org`     |
| Phumzile Mhlongo   | `p.mhlongo@michaelhouse.org`    |

---

### 🚨 Fault Reporters

All reporters have the password `Report@123`.

| Name                    | Email                                 |
|-------------------------|---------------------------------------|
| Mr. David Hutchinson    | `d.hutchinson@michaelhouse.org`       |
| Mr. Peter van der Merwe | `p.vandermerwe@michaelhouse.org`      |
| Mr. Simon Ndlovu        | `s.ndlovu@michaelhouse.org`           |
| Ms. Nompumelelo Dube    | `n.dube@michaelhouse.org`             |

---

### 🏠 House Masters

All house masters have the password `HouseMaster@123`.

| Name                     | Email                                 |
|--------------------------|---------------------------------------|
| Mr James Harrington      | `founders.hm@michaelhouse.co.za`      |
| Ms Nomvula Dlamini       | `baines.hm@michaelhouse.co.za`        |
| Mr Andrew Naidoo         | `tatham.hm@michaelhouse.co.za`        |
| Ms Sarah Mokoena         | `churchill.hm@michaelhouse.co.za`     |

---

## 🧪 Testing & Development

- **Admin Panel**: Accessible after login with admin credentials.
- **Student/Parent/Teacher** dashboards are role‑based.
- **QR code scanning** and **emergency roll‑call** features are available for House Masters.
- **Transport** module allows trip scheduling, driver management, and student check‑in/out.

---

## ❗ Troubleshooting

### SQL Connection Error (localhost)
If you see `A network-related or instance-specific error...`:
- Your local IP must be added to the Azure SQL Server firewall.
- Go to Azure Portal → SQL Server → Firewalls → Add your current client IP.
- If behind a VPN, add the VPN’s egress IP.
- Verify you can connect via SSMS using the same credentials.

### NuGet Package Restore Fails
- Close Visual Studio, delete the `packages` folder, then reopen and rebuild.
- Or run `nuget restore Michaelhouse.sln` from the command line.

### Debugging Release Build
- Switch to `Debug` configuration before debugging to hit breakpoints.

---

## 📱 Mobile App (MhmsMobileApp)

The `MhmsMobileApp` folder is a **.NET MAUI** app (Android + Windows) for the cafeteria. It talks to this website through the APIs in `ApiControllers` and uses the **same logins and rules** as the website. After login it shows only the screens for the person's role:

| Role | Screens |
|------|---------|
| Student | My Meal Plan, My Events (RSVP) |
| Parent / Staff | My Events (RSVP) |
| Chef | Meal Collection (face scan), Kitchen Plan |
| Cafeteria Manager / Admin | Meal Collection, Record Delivery, Kitchen Plan, Feast Plan |

### What you need

- **Visual Studio 2022** with the **.NET Multi-platform App UI (MAUI)** workload (.NET 9) and an **Android emulator** (Tools → Android → Android Device Manager).
- The website running on `https://localhost:44321` (the app uses `https://10.0.2.2:44321` on the Android emulator, which is your PC's `localhost`).

### 1. Database (LocalDB)

The website uses LocalDB (`Web.config` → `MichaelHouse`). Only `Migrations/Configuration.cs` (the seed data) is in git — the migration files are not.

1. If the website won't build because `Migrations\2026….cs` files are missing, remove those missing entries in Solution Explorer (they show a yellow warning ⚠) — don't commit that change.
2. **Tools → NuGet Package Manager → Package Manager Console**, *Default project*: `Michaelhouse`, then run:
   ```
   Add-Migration InitialLocal
   Update-Database
   ```
   `Update-Database` creates the database and runs the seed in `Migrations/Configuration.cs` (test accounts and sample data).

### 2. Face recognition (once per computer)

Meal Collection uses real face recognition (FaceRecognitionDotNet / dlib, runs locally). Its model files (~132 MB) are not in git:

1. In the project folder, right-click **`Download-FaceModels.ps1`** → **Run with PowerShell** (downloads into `App_Data\FaceModels`).
2. Restart IIS Express: system tray IIS Express icon → **Exit** (the project runs **64-bit IIS Express**, which face recognition needs).
3. Students must **enrol their face** on the website (log in as the student → *Face Enrollment*). A face can only be enrolled for one student.
4. Check: log in as Admin → `https://localhost:44321/DemoData/Index` should say **Face recognition: Ready**.

### 3. Emulator camera (for face scans and invoice photos)

Android Studio → **Device Manager** → your emulator → ✏️ **Edit** → **Show Advanced Settings** → **Camera**: set **Front** and **Back** to **Webcam0** → Finish, then restart the emulator.

### 4. Run both projects

1. Right-click the solution → **Configure Startup Projects…** → **Multiple startup projects** → set **Michaelhouse** and **MhmsMobileApp** to **Start**.
2. In the toolbar, set MhmsMobileApp's target to **Android Emulators → (your emulator)** (or *Windows Machine*).
3. Press **F5**. The app's login screen says "Connecting to the Michaelhouse server…" until the website has started.

### Test accounts for the app

Passwords are set in `Migrations/Configuration.cs` (seed) and `Controllers/AccountController.cs` (`SeedAdmin`).

| Role | Email |
|------|-------|
| Student | `amina.khan@michaelhouse.co.za` (also `thabo.ntuli@…`, `lindiwe.mthembu@…`) |
| Staff (Teacher) | `teacher@michaelhouse.co.za` |
| Chef | `chef@michaelhouse.co.za` |
| Cafeteria Manager | `cafeteria@michaelhouse.co.za` |
| Admin | `admin@michaelhouse.co.za` — create it once at `https://localhost:44321/Account/SeedAdmin` |
| Parent | register one at `https://localhost:44321/Account/Register` |

### Demo data (Admin only)

`https://localhost:44321/DemoData/Index` — see and remove face enrollments, and give a student a **submitted meal plan for today and the next few days** (from the published menu) so a meal can be collected at any time. Run it again before each demo.

---

## 📄 License

This project is proprietary to Michaelhouse and is intended for internal use only.

---

*Last updated: August 2026*
