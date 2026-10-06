using Michaelhouse.Models;
using Michaelhouse.Models.Cafeteria;
using Michaelhouse.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;

namespace Michaelhouse.Services
{
    // ============================================================
    // UC18 — RSVP lifecycle for cafeteria events
    //
    // The event's Status stays the record of what happened
    // (Confirmed → RsvpOpen → RsvpClosed …). This service adds the
    // scheduled opening on top:
    //
    //   Not Open          no opening time, not opened yet
    //   Scheduled to Open RsvpOpensAt is in the future
    //   Open              opened (by schedule or manually), and the
    //                     deadline hasn't passed
    //   Closed            closed, deadline passed, cancelled or over
    //
    // Opening — scheduled or manual — always goes through OpenRsvp,
    // so the status, RsvpOpenedAt and the invitations are the same
    // either way. Scheduled openings are applied by OpenDueRsvps,
    // which runs every minute (EventRsvpScheduler) and also at the
    // start of every event / RSVP page, so a page never shows a
    // stale status.
    //
    // Invited students, parents and staff RSVP with their login; the
    // attendee type comes from their role. Students' dietary needs
    // come from their StudentProfile; parents and staff give theirs
    // on the RSVP. When the event offers a meal choice, the meal is
    // checked against those needs. Parents and staff may bring
    // guests when the event allows it.
    //
    // Closing — manual, or automatically once the deadline day has
    // ended — fixes the headcount from the accepted RSVPs and hands
    // the plan to the kitchen (CloseRsvp). Nothing reaches the
    // kitchen before that.
    //
    // All times are school time (SchoolClock) except RsvpOpenedAt /
    // RsvpClosedAt, which stay UTC as before.
    // ============================================================

    public enum RsvpState
    {
        NotOpen = 1,
        ScheduledToOpen = 2,
        Open = 3,
        Closed = 4
    }

    public class RsvpStatusInfo
    {
        public RsvpState State { get; set; }

        // "Not Open" / "Scheduled to Open" / "Open" / "Closed"
        public string Label { get; set; }

        // e.g. "Opens Sat 01 Nov at 08:00" / "Deadline passed"
        public string Detail { get; set; }

        public bool AcceptingResponses { get { return State == RsvpState.Open; } }

        // The manager can open RSVPs now (early or without a schedule)
        public bool CanOpenNow { get; set; }

        // The manager can close RSVPs and freeze the headcount
        public bool CanClose { get; set; }
    }

    public class EventRsvpService
    {
        public const string ResponseAttending = "Attending";
        public const string ResponseDeclined = "Declined";
        public const string GroupStudent = "Student";

        // All non-parent, non-student roles that count as "staff"
        private static readonly string[] StaffRoles = new[]
        {
            "Admin", "Teacher", "HouseMaster", "Housemaster",
            "CafeteriaManager", "Chef", "Dietitian", "Coach",
            "InventoryManager", "TransportManager",
            "MaintenanceManager", "MaintenanceWorker", "Driver"
        };

        private readonly DBContextClass _db;
        private readonly Func<DateTime> _now;

        public EventRsvpService(DBContextClass db)
            : this(db, null)
        {
        }

        // now can be replaced in tests; defaults to school time
        public EventRsvpService(DBContextClass db, Func<DateTime> now)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _now = now ?? (() => SchoolClock.Now);
        }

        // ============================================================
        // STATUS
        // ============================================================

        public RsvpStatusInfo GetStatus(CafeteriaEvent evt)
        {
            var now = _now();
            var info = new RsvpStatusInfo();

            switch (evt.Status)
            {
                case EventStatus.Cancelled:
                    return Closed(info, "Event cancelled");

                case EventStatus.RsvpClosed:
                case EventStatus.FeastPlanGenerated:
                case EventStatus.InProgress:
                case EventStatus.Completed:
                    return Closed(info, "Headcount confirmed");

                case EventStatus.RsvpOpen:
                    if (DeadlinePassed(evt, now))
                    {
                        info.CanClose = true;
                        return Closed(info, "Deadline passed — closing and sending the plan to the kitchen");
                    }

                    info.State = RsvpState.Open;
                    info.Label = "Open";
                    info.Detail = evt.RsvpDeadline.HasValue
                        ? "Until " + evt.RsvpDeadline.Value.ToString("ddd dd MMM")
                        : "No deadline set";
                    info.CanClose = true;
                    return info;
            }

            // Draft / Confirmed: not opened yet
            if (evt.EventDate.Date < now.Date)
            {
                return Closed(info, "Event has passed");
            }

            if (evt.RsvpOpensAt.HasValue && evt.RsvpOpensAt.Value <= now)
            {
                // Due — OpenDueRsvps records it; until then it's open
                info.State = RsvpState.Open;
                info.Label = "Open";
                info.Detail = "Opened " + evt.RsvpOpensAt.Value.ToString("ddd dd MMM 'at' HH:mm");
                info.CanClose = true;
                return info;
            }

            info.CanOpenNow = true;

            if (evt.RsvpOpensAt.HasValue)
            {
                info.State = RsvpState.ScheduledToOpen;
                info.Label = "Scheduled to Open";
                info.Detail = "Opens " + evt.RsvpOpensAt.Value.ToString("ddd dd MMM 'at' HH:mm");
            }
            else
            {
                info.State = RsvpState.NotOpen;
                info.Label = "Not Open";
                info.Detail = "No opening time set";
            }

            return info;
        }

        private static RsvpStatusInfo Closed(RsvpStatusInfo info, string detail)
        {
            info.State = RsvpState.Closed;
            info.Label = "Closed";
            info.Detail = detail;
            return info;
        }

        // Responses are accepted until the end of the deadline day
        private static bool DeadlinePassed(CafeteriaEvent evt, DateTime now)
        {
            return evt.RsvpDeadline.HasValue && now.Date > evt.RsvpDeadline.Value.Date;
        }

        // ============================================================
        // OPENING
        // ============================================================

        // Opens RSVPs now — the manager's "Open now" and the scheduled
        // opening both come here. Returns false if it was already
        // opened (e.g. by the scheduler a moment earlier).
        // rsvpLink builds the link put in the invitation; null leaves
        // it out (the notification still links to the form).
        public bool OpenRsvp(CafeteriaEvent evt, DateTime? deadline, Func<int, string> rsvpLink, out int notified)
        {
            notified = 0;

            if (evt.Status != EventStatus.Confirmed && evt.Status != EventStatus.Draft)
            {
                return false;
            }

            var openedAt = DateTime.UtcNow;

            // Only one caller wins, so invitations are sent once
            int changed = _db.Database.ExecuteSqlCommand(
                "UPDATE dbo.CafeteriaEvents SET Status = @status, RsvpOpenedAt = @openedAt, UpdatedAt = @openedAt, "
                + "RsvpDeadline = COALESCE(@deadline, RsvpDeadline) "
                + "WHERE Id = @id AND Status IN (@confirmed, @draft)",
                new SqlParameter("@status", SqlDbType.Int) { Value = (int)EventStatus.RsvpOpen },
                new SqlParameter("@openedAt", SqlDbType.DateTime) { Value = openedAt },
                new SqlParameter("@deadline", SqlDbType.DateTime) { Value = deadline.HasValue ? (object)deadline.Value.Date : DBNull.Value },
                new SqlParameter("@id", SqlDbType.Int) { Value = evt.Id },
                new SqlParameter("@confirmed", SqlDbType.Int) { Value = (int)EventStatus.Confirmed },
                new SqlParameter("@draft", SqlDbType.Int) { Value = (int)EventStatus.Draft });

            _db.Entry(evt).Reload();

            if (changed == 0)
            {
                return false;
            }

            notified = NotifyAudience(evt, rsvpLink);
            return true;
        }

        // Opens every event whose scheduled opening time has arrived
        public int OpenDueRsvps(Func<int, string> rsvpLink)
        {
            var now = _now();
            var today = now.Date;

            var due = _db.CafeteriaEvents
                .Where(e => (e.Status == EventStatus.Confirmed || e.Status == EventStatus.Draft)
                            && e.RsvpOpensAt.HasValue && e.RsvpOpensAt <= now
                            && e.EventDate >= today)
                .ToList();

            int opened = 0;
            foreach (var evt in due)
            {
                int notified;
                if (OpenRsvp(evt, null, rsvpLink, out notified)) opened++;
            }

            return opened;
        }

        // ============================================================
        // AUDIENCE
        // ============================================================

        public static List<int> ParseIds(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<int>();

            return csv
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    int i;
                    return int.TryParse(x.Trim(), out i) ? (int?)i : null;
                })
                .Where(i => i.HasValue)
                .Select(i => i.Value)
                .Distinct()
                .ToList();
        }

        // Students invited: all registered (active) students, or only
        // those living in the chosen houses. Empty if students aren't
        // invited.
        public List<int> EligibleStudentIds(bool inviteStudents, string studentHouseIds)
        {
            if (!inviteStudents) return new List<int>();

            var houses = ParseIds(studentHouseIds);
            var query = _db.Students.Where(s => s.IsActive);

            if (houses.Count > 0)
            {
                var inHouses = StudentIdsInHouses(houses);
                query = query.Where(s => inHouses.Contains(s.StudentId));
            }

            return query.Select(s => s.StudentId).ToList();
        }

        public List<int> EligibleStudentIds(CafeteriaEvent evt)
        {
            return EligibleStudentIds(evt.InviteStudents, evt.InviteStudentResidenceIds);
        }

        // Students who RSVP'd as attending (through their own login)
        public List<int> AttendingStudentIds(int eventId)
        {
            var userIds = _db.EventRsvps
                .Where(r => r.EventId == eventId
                            && r.ResponseStatus == ResponseAttending
                            && r.ResponderGroup == GroupStudent
                            && r.RespondedByUserId.HasValue)
                .Select(r => r.RespondedByUserId.Value)
                .ToList();

            return _db.Students
                .Where(s => s.UserId.HasValue && userIds.Contains(s.UserId.Value))
                .Select(s => s.StudentId)
                .ToList();
        }

        public bool IsStudentInvited(CafeteriaEvent evt, Student student)
        {
            if (student == null || !student.IsActive || !evt.InviteStudents) return false;

            var houses = ParseIds(evt.InviteStudentResidenceIds);
            if (houses.Count == 0) return true;

            var house = HouseOf(student);
            return house.HasValue && houses.Contains(house.Value);
        }

        // ============================================================
        // HOUSES — where a student lives (StudentHouseService: active
        // ResidenceAllocation, else Student.ResidenceId)
        // ============================================================

        public int? HouseOf(Student student)
        {
            return new StudentHouseService(_db).HouseOf(student);
        }

        public List<int> StudentIdsInHouses(ICollection<int> houses)
        {
            return new StudentHouseService(_db).StudentIdsInHouses(houses);
        }

        private int NotifyAudience(CafeteriaEvent evt, Func<int, string> rsvpLink)
        {
            var recipients = new HashSet<int>();

            if (evt.InviteParents)
            {
                var parentQuery = _db.Parents.Where(p => p.UserId != null);
                var houseIds = ParseIds(evt.InviteParentResidenceIds);

                if (houseIds.Count > 0)
                {
                    var inHouses = StudentIdsInHouses(houseIds);
                    var parentIdsInHouses = _db.Students
                        .Where(s => inHouses.Contains(s.StudentId))
                        .Select(s => s.ParentId)
                        .Distinct()
                        .ToList();

                    parentQuery = parentQuery.Where(p => parentIdsInHouses.Contains(p.ParentId));
                }

                foreach (var uid in parentQuery.Select(p => p.UserId.Value).ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (evt.InviteStudents)
            {
                var studentIds = EligibleStudentIds(evt);
                foreach (var uid in _db.Students
                                       .Where(s => studentIds.Contains(s.StudentId) && s.UserId != null)
                                       .Select(s => s.UserId.Value)
                                       .ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (evt.InviteStaff)
            {
                foreach (var uid in _db.Users
                                       .Where(u => StaffRoles.Contains(u.Role))
                                       .Select(u => u.UserId)
                                       .ToList())
                {
                    recipients.Add(uid);
                }
            }

            if (recipients.Count == 0) return 0;

            var deadlineText = evt.RsvpDeadline.HasValue
                ? evt.RsvpDeadline.Value.ToString("ddd dd MMM")
                : "the deadline";

            var link = rsvpLink != null ? rsvpLink(evt.Id) : null;

            var message = string.Format(
                "You're invited to {0} on {1}. RSVP by {2}{3}",
                evt.EventName,
                evt.EventDate.ToString("ddd dd MMM"),
                deadlineText,
                string.IsNullOrEmpty(link) ? "." : " — " + link);

            var now = DateTime.Now;
            foreach (var uid in recipients)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    RelatedEntityType = "CafeteriaEvent",
                    RelatedEntityId = evt.Id,
                    IsRead = false,
                    CreatedAt = now
                });
            }

            _db.SaveChanges();
            return recipients.Count;
        }

        // ============================================================
        // INVITEES — students, parents and staff RSVP'ing with their
        // own login. Their type comes from their login role, never
        // from the form.
        // ============================================================

        public static bool IsStaffRole(string role)
        {
            return !string.IsNullOrWhiteSpace(role)
                && StaffRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        // The invitee for this login, or null if the role isn't one
        // that RSVPs as an invitee (they use the public guest form)
        public Invitee ResolveInvitee(CafeteriaEvent evt, int userId, string role)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(role)) return null;

            var user = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return null;

            var invitee = new Invitee { UserId = userId, Email = user.Email, Name = user.Name };

            if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                var student = _db.Students
                    .Include(s => s.StudentProfile)
                    .FirstOrDefault(s => s.UserId == userId);

                invitee.Type = AttendeeType.Student;
                invitee.StudentId = student != null ? (int?)student.StudentId : null;
                if (student != null) invitee.Name = (student.FirstName + " " + student.LastName).Trim();
                invitee.ProfileDietary = DietaryProfileService.FromStudentProfile(student != null ? student.StudentProfile : null);
                invitee.IsInvited = IsStudentInvited(evt, student);
                invitee.NotInvitedReason = "Students from your house are not invited to this event.";
                return invitee;
            }

            if (string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase))
            {
                var parent = _db.Parents.FirstOrDefault(p => p.UserId == userId);

                invitee.Type = AttendeeType.Parent;
                if (parent != null && !string.IsNullOrWhiteSpace(parent.Name)) invitee.Name = parent.Name;
                invitee.IsInvited = IsParentInvited(evt, parent);
                invitee.NotInvitedReason = "Parents of your children's house are not invited to this event.";
                invitee.CanBringGuests = evt.AllowGuests && evt.MaxGuestsPerInvitee > 0;
                return invitee;
            }

            if (IsStaffRole(role))
            {
                invitee.Type = AttendeeType.Staff;
                invitee.IsInvited = evt.InviteStaff;
                invitee.NotInvitedReason = "Staff are not invited to this event.";
                invitee.CanBringGuests = evt.AllowGuests && evt.MaxGuestsPerInvitee > 0;
                return invitee;
            }

            return null;
        }

        public bool IsParentInvited(CafeteriaEvent evt, Parent parent)
        {
            if (parent == null || !evt.InviteParents) return false;

            var houses = ParseIds(evt.InviteParentResidenceIds);
            if (houses.Count == 0) return true;

            int parentId = parent.ParentId;
            var inHouses = StudentIdsInHouses(houses);
            return _db.Students.Any(s => s.ParentId == parentId && inHouses.Contains(s.StudentId));
        }

        // Events this login is invited to, newest first: RSVP open or
        // scheduled, plus closed ones they answered (for their record)
        public List<EventInvitation> GetInvitations(int userId, string role)
        {
            var today = _now().Date;

            var events = _db.CafeteriaEvents
                .Include(e => e.Venue)
                .Where(e => e.EventDate >= today
                            && e.Status != EventStatus.Cancelled
                            && e.Status != EventStatus.Draft)
                .OrderBy(e => e.EventDate)
                .ToList();

            var result = new List<EventInvitation>();

            foreach (var evt in events)
            {
                var invitee = ResolveInvitee(evt, userId, role);
                if (invitee == null || !invitee.IsInvited) continue;

                var response = FindInviteeResponse(evt.Id, userId);
                var status = GetStatus(evt);

                // Not yet announced: only show once a date to open is set
                if (status.State == RsvpState.NotOpen && response == null) continue;
                if (status.State == RsvpState.Closed && response == null) continue;

                result.Add(new EventInvitation { Event = evt, Status = status, Response = response });
            }

            return result;
        }

        // How many invited people have a login, i.e. can be notified
        // and can RSVP with their profile
        public InvitedSummary GetInvitedSummary(CafeteriaEvent evt)
        {
            var summary = new InvitedSummary();

            if (evt.InviteStudents)
            {
                var ids = EligibleStudentIds(evt);
                summary.Students = ids.Count;
                summary.StudentsWithLogin = _db.Students.Count(s => ids.Contains(s.StudentId) && s.UserId != null);
            }

            if (evt.InviteParents)
            {
                var parents = _db.Parents.ToList().Where(p => IsParentInvited(evt, p)).ToList();
                summary.Parents = parents.Count;
                summary.ParentsWithLogin = parents.Count(p => p.UserId.HasValue);
            }

            if (evt.InviteStaff)
            {
                summary.StaffWithLogin = _db.Users.Count(u => StaffRoles.Contains(u.Role));
            }

            return summary;
        }

        // The invitee's own RSVP (one per login per event)
        public EventRsvp FindInviteeResponse(int eventId, int userId)
        {
            return _db.EventRsvps
                .Include(r => r.Guests)
                .Where(r => r.EventId == eventId && r.RespondedByUserId == userId
                            && (r.ResponderGroup == AttendeeType.Student
                                || r.ResponderGroup == AttendeeType.Parent
                                || r.ResponderGroup == AttendeeType.Staff))
                .OrderByDescending(r => r.Id)
                .FirstOrDefault();
        }

        public EventRsvp FindStudentResponse(int eventId, int userId)
        {
            return FindInviteeResponse(eventId, userId);
        }

        // Dietary needs a parent / staff member gave on their RSVP,
        // read with the same rules as a student profile
        public static StudentDietaryProfile RsvpDietary(EventRsvp rsvp)
        {
            if (rsvp == null) return new StudentDietaryProfile();

            return DietaryProfileService.FromStudentProfile(new StudentProfile
            {
                DietaryPreference = rsvp.DietaryPreference,
                DietaryPreferenceOther = rsvp.DietaryPreferenceOther,
                Allergies = rsvp.Allergies,
                MedicalDietaryRestrictions = rsvp.MedicalDietaryRestrictions,
                MedicalDietaryRestrictionOther = rsvp.MedicalDietaryRestrictionOther,
                DietaryNotes = rsvp.DietaryNotes
            });
        }

        // A guest's dietary group as a profile
        public static StudentDietaryProfile GuestDietary(string preference, string notes)
        {
            var pref = DietaryProfileService.PreferenceOptions.FirstOrDefault(o =>
                string.Equals(o.Code, (preference ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

            return new StudentDietaryProfile
            {
                Preference = pref != null ? pref.Code : DietaryProfileService.PreferenceNone,
                PreferenceOther = pref != null && pref.Code == DietaryProfileService.OtherCode ? notes : null,
                Notes = notes
            };
        }

        // The event's meals, each marked suitable or not for a profile
        public List<RsvpMealOption> MealOptionsFor(CafeteriaEvent evt, StudentDietaryProfile profile)
        {
            var dietary = new DietaryProfileService(_db);
            var buffet = new EventBuffetService(_db);

            return buffet.GetBuffetLines(evt)
                .Where(l => l.MenuItem.IsActive)
                .Select(l =>
                {
                    var conflicts = dietary.GetConflicts(profile, l.MenuItem);
                    var described = buffet.Describe(l.MenuItem);
                    return new RsvpMealOption
                    {
                        MenuItemId = l.MenuItem.Id,
                        Name = l.MenuItem.Name,
                        Description = l.MenuItem.Description,
                        Allergens = described.Allergens,
                        SuitableFor = described.SuitableFor,
                        IsSuitable = conflicts.Count == 0,
                        UnsuitableReason = string.Join("; ", conflicts.Select(c => c.Message))
                    };
                })
                .ToList();
        }

        // ============================================================
        // SUBMIT — accept / decline, dietary needs, meal, guests.
        // Everything is re-checked here; nothing from the form is
        // trusted (who they are, their type, the meals available).
        // ============================================================

        public RsvpResult SubmitInviteeResponse(int eventId, int userId, string role, InviteeRsvpInput input)
        {
            var evt = _db.CafeteriaEvents.FirstOrDefault(e => e.Id == eventId);
            if (evt == null) return RsvpResult.Fail("Event not found.");

            if (!GetStatus(evt).AcceptingResponses)
                return RsvpResult.Fail("This event is not accepting RSVPs.");

            var invitee = ResolveInvitee(evt, userId, role);
            if (invitee == null)
                return RsvpResult.Fail("Please use the guest RSVP form.");

            if (!invitee.IsInvited)
                return RsvpResult.Fail(invitee.NotInvitedReason);

            input = input ?? new InviteeRsvpInput();
            var guests = input.Guests ?? new List<GuestInput>();
            var errors = new List<string>();

            // ── Dietary needs ──
            StudentDietaryProfile profile;
            StudentProfile given = null;

            if (invitee.Type == AttendeeType.Student)
            {
                profile = invitee.ProfileDietary;          // from their profile
            }
            else
            {
                var dietaryForm = input.Dietary ?? new DietaryProfileViewModel();
                var dietaryErrors = new DietaryProfileService(_db).Validate(dietaryForm);
                if (input.Attending) errors.AddRange(dietaryErrors);

                given = new StudentProfile();                // not saved — format only
                if (dietaryErrors.Count == 0) DietaryProfileService.ApplyTo(given, dietaryForm);
                profile = DietaryProfileService.FromStudentProfile(given);
            }

            // ── Meals: only the event's own meals, safe for the person ──
            var mealOptions = evt.OffersMealChoice ? MealOptionsFor(evt, profile) : new List<RsvpMealOption>();
            var mealIds = mealOptions.Select(m => m.MenuItemId).ToList();
            int? ownMeal = null;

            if (input.Attending && evt.OffersMealChoice && mealOptions.Count > 0)
            {
                var chosen = mealOptions.FirstOrDefault(m => input.MenuItemId.HasValue && m.MenuItemId == input.MenuItemId.Value);

                if (chosen == null)
                    errors.Add("Please choose your meal from this event's meals.");
                else if (!chosen.IsSuitable)
                    errors.Add(string.Format("{0} isn't suitable for you: {1}.", chosen.Name, chosen.UnsuitableReason));
                else
                    ownMeal = chosen.MenuItemId;
            }

            // ── Guests: parents and staff only, within the event's limit ──
            if (!input.Attending || !invitee.CanBringGuests)
            {
                if (input.Attending && guests.Count > 0)
                    errors.Add(invitee.Type == AttendeeType.Student
                        ? "Students can't bring guests to this event."
                        : "This event doesn't allow guests.");
                guests = new List<GuestInput>();
            }
            else if (guests.Count > evt.MaxGuestsPerInvitee)
            {
                errors.Add(string.Format("You can bring up to {0} guest{1}.", evt.MaxGuestsPerInvitee, evt.MaxGuestsPerInvitee == 1 ? "" : "s"));
            }

            var dietaryService = new DietaryProfileService(_db);
            var buffetMeals = evt.OffersMealChoice
                ? new EventBuffetService(_db).GetBuffetLines(evt).Where(l => l.MenuItem.IsActive).ToDictionary(l => l.MenuItem.Id, l => l.MenuItem)
                : new Dictionary<int, MenuItem>();

            for (int i = 0; i < guests.Count; i++)
            {
                var g = guests[i];
                string label = "Guest " + (i + 1);

                if (!DietaryProfileService.PreferenceOptions.Any(o => string.Equals(o.Code, (g.DietaryPreference ?? DietaryProfileService.PreferenceNone).Trim(), StringComparison.OrdinalIgnoreCase)))
                    errors.Add(label + ": choose a dietary group from the list.");

                if (string.Equals((g.DietaryPreference ?? "").Trim(), DietaryProfileService.OtherCode, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(g.DietaryNotes))
                    errors.Add(label + ": please describe their dietary requirement.");

                if ((g.DietaryNotes ?? "").Trim().Length > 200)
                    errors.Add(label + ": the dietary note must be 200 characters or fewer.");

                if ((g.CulturalFavoriteDish ?? "").Trim().Length > 150)
                    errors.Add(label + ": the cultural favourite dish must be 150 characters or fewer.");

                if (evt.OffersMealChoice && buffetMeals.Count > 0)
                {
                    MenuItem meal;
                    if (!g.MenuItemId.HasValue || !buffetMeals.TryGetValue(g.MenuItemId.Value, out meal))
                    {
                        errors.Add(label + ": choose a meal from this event's meals.");
                    }
                    else
                    {
                        var conflicts = dietaryService.GetConflicts(GuestDietary(g.DietaryPreference, g.DietaryNotes), meal);
                        if (conflicts.Count > 0)
                            errors.Add(string.Format("{0}: {1} isn't suitable ({2}).", label, meal.Name, string.Join("; ", conflicts.Select(c => c.Message))));
                    }
                }
            }

            if ((input.Comments ?? "").Trim().Length > 500)
                errors.Add("Comments must be 500 characters or fewer.");

            if (errors.Count > 0)
                return RsvpResult.Fail(errors);

            // ── Save (one RSVP per login; guests replaced) ──
            var rsvp = FindInviteeResponse(eventId, userId);
            if (rsvp == null)
            {
                rsvp = new EventRsvp
                {
                    EventId = eventId,
                    RespondedByUserId = userId,
                    RespondedAt = DateTime.UtcNow
                };
                _db.EventRsvps.Add(rsvp);
            }
            else
            {
                rsvp.UpdatedAt = DateTime.UtcNow;
                foreach (var old in rsvp.Guests.ToList()) _db.EventRsvpGuests.Remove(old);
            }

            rsvp.ResponderGroup = invitee.Type;
            rsvp.ResponderName = invitee.Name ?? "";
            rsvp.ResponderEmail = invitee.Email ?? "";
            rsvp.ResponseStatus = input.Attending ? ResponseAttending : ResponseDeclined;
            rsvp.Comments = (input.Comments ?? "").Trim();
            rsvp.MenuItemId = input.Attending ? ownMeal : null;

            // Parents / staff: what they told us; students: their profile is used
            if (given != null && input.Attending)
            {
                rsvp.DietaryPreference = given.DietaryPreference;
                rsvp.DietaryPreferenceOther = given.DietaryPreferenceOther;
                rsvp.Allergies = given.Allergies;
                rsvp.MedicalDietaryRestrictions = given.MedicalDietaryRestrictions;
                rsvp.MedicalDietaryRestrictionOther = given.MedicalDietaryRestrictionOther;
                rsvp.DietaryNotes = given.DietaryNotes ?? "";
            }
            else if (invitee.Type == AttendeeType.Student)
            {
                rsvp.DietaryNotes = "";
            }

            rsvp.GuestCount = guests.Count;
            foreach (var g in guests)
            {
                rsvp.Guests.Add(new EventRsvpGuest
                {
                    DietaryPreference = GuestDietary(g.DietaryPreference, null).Preference,
                    DietaryNotes = string.IsNullOrWhiteSpace(g.DietaryNotes) ? null : g.DietaryNotes.Trim(),
                    MenuItemId = evt.OffersMealChoice ? g.MenuItemId : null,
                    CulturalFavoriteDish = string.IsNullOrWhiteSpace(g.CulturalFavoriteDish) ? null : g.CulturalFavoriteDish.Trim()
                });
            }

            // Keep the existing headcount fields meaningful
            rsvp.TotalAttendees = input.Attending ? 1 + guests.Count : 0;
            rsvp.StandardCount = 0;
            rsvp.VegetarianCount = 0;
            rsvp.OtherDietCount = 0;

            if (input.Attending)
            {
                CountDiet(rsvp, profile);
                foreach (var g in guests) CountDiet(rsvp, GuestDietary(g.DietaryPreference, g.DietaryNotes));
            }

            _db.SaveChanges();

            return new RsvpResult
            {
                Success = true,
                Attending = input.Attending,
                Message = input.Attending
                    ? (guests.Count > 0
                        ? string.Format("You're attending with {0} guest{1}.", guests.Count, guests.Count == 1 ? "" : "s")
                        : "You're attending.")
                    : "You've said you won't attend."
            };
        }

        // Students answer attending / not attending only
        public RsvpResult SubmitStudentResponse(int eventId, int userId, bool attending, string comments)
        {
            return SubmitInviteeResponse(eventId, userId, "Student", new InviteeRsvpInput { Attending = attending, Comments = comments });
        }

        private static void CountDiet(EventRsvp rsvp, StudentDietaryProfile profile)
        {
            if (profile.Preference == DietaryProfileService.PreferenceVegetarian
                || profile.Preference == DietaryProfileService.PreferenceVegan)
            {
                rsvp.VegetarianCount++;
            }
            else if (profile.Preference == DietaryProfileService.OtherCode
                     || MenuCoverageService.FilteringRequirements(profile).Count > 0)
            {
                rsvp.OtherDietCount++;
            }
            else
            {
                rsvp.StandardCount++;
            }
        }

        // ============================================================
        // ATTENDEES — everyone who accepted, one entry per person,
        // with their attendee type, dietary needs and meal.
        // ============================================================

        public List<EventAttendee> GetAttendees(int eventId)
        {
            var rsvps = _db.EventRsvps
                .Include(r => r.Guests)
                .Where(r => r.EventId == eventId && r.ResponseStatus == ResponseAttending)
                .ToList();

            var studentUserIds = rsvps
                .Where(r => r.ResponderGroup == AttendeeType.Student && r.RespondedByUserId.HasValue)
                .Select(r => r.RespondedByUserId.Value)
                .ToList();

            var students = _db.Students
                .Include(s => s.StudentProfile)
                .Where(s => s.UserId.HasValue && studentUserIds.Contains(s.UserId.Value))
                .ToList()
                .GroupBy(s => s.UserId.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var result = new List<EventAttendee>();

            foreach (var r in rsvps)
            {
                bool invitee = r.RespondedByUserId.HasValue
                    && (r.ResponderGroup == AttendeeType.Student || r.ResponderGroup == AttendeeType.Parent || r.ResponderGroup == AttendeeType.Staff);

                if (invitee)
                {
                    StudentDietaryProfile profile;
                    if (r.ResponderGroup == AttendeeType.Student)
                    {
                        Student st;
                        profile = DietaryProfileService.FromStudentProfile(
                            students.TryGetValue(r.RespondedByUserId.Value, out st) ? st.StudentProfile : null);
                    }
                    else
                    {
                        profile = RsvpDietary(r);
                    }

                    result.Add(new EventAttendee { Type = r.ResponderGroup, Name = r.ResponderName, Profile = profile, MenuItemId = r.MenuItemId, RsvpId = r.Id });

                    int n = 1;
                    foreach (var g in r.Guests.OrderBy(x => x.Id))
                    {
                        result.Add(new EventAttendee
                        {
                            Type = AttendeeType.Guest,
                            Name = r.ResponderName + " — guest " + n++,
                            Profile = GuestDietary(g.DietaryPreference, g.DietaryNotes),
                            MenuItemId = g.MenuItemId,
                            RsvpId = r.Id,
                            IsGuest = true
                        });
                    }
                    continue;
                }

                // Public form: the person responding (by their stated
                // group) plus the rest of their party as guests. Diets
                // are only known as counts.
                int total = Math.Max(1, r.TotalAttendees);
                var diets = new List<StudentDietaryProfile>();
                for (int i = 0; i < r.VegetarianCount; i++) diets.Add(new StudentDietaryProfile { Preference = DietaryProfileService.PreferenceVegetarian });
                for (int i = 0; i < r.OtherDietCount; i++) diets.Add(new StudentDietaryProfile { Preference = DietaryProfileService.OtherCode, PreferenceOther = r.DietaryNotes, Notes = r.DietaryNotes });
                while (diets.Count < total) diets.Add(new StudentDietaryProfile());

                for (int i = 0; i < total; i++)
                {
                    result.Add(new EventAttendee
                    {
                        Type = i == 0 ? AttendeeType.Of(r.ResponderGroup) : AttendeeType.Guest,
                        Name = i == 0 ? r.ResponderName : r.ResponderName + " — guest " + i,
                        Profile = diets[i],
                        RsvpId = r.Id,
                        IsGuest = i > 0
                    });
                }
            }

            return result;
        }

        public EventAttendance GetAttendance(int eventId)
        {
            var attendees = GetAttendees(eventId);
            var responses = _db.EventRsvps.Where(r => r.EventId == eventId).Select(r => r.ResponseStatus).ToList();

            return new EventAttendance
            {
                Students = attendees.Count(a => a.Type == AttendeeType.Student),
                Parents = attendees.Count(a => a.Type == AttendeeType.Parent),
                Staff = attendees.Count(a => a.Type == AttendeeType.Staff),
                Guests = attendees.Count(a => a.Type == AttendeeType.Guest),
                Responses = responses.Count,
                Accepted = responses.Count(s => s == ResponseAttending),
                Declined = responses.Count(s => s == ResponseDeclined)
            };
        }

        // ============================================================
        // CLOSING + KITCHEN HANDOFF
        // Closing (manual, or automatically after the deadline) fixes
        // the headcount from the actual RSVPs and hands the plan to
        // the kitchen — never earlier.
        // ============================================================

        public bool CloseRsvp(CafeteriaEvent evt)
        {
            if (evt.Status != EventStatus.RsvpOpen) return false;

            int total = GetAttendance(evt.Id).Total;

            int changed = _db.Database.ExecuteSqlCommand(
                "UPDATE dbo.CafeteriaEvents SET Status = @status, GuaranteedHeadcount = @total, RsvpClosedAt = @closedAt, UpdatedAt = @closedAt "
                + "WHERE Id = @id AND Status = @open",
                new SqlParameter("@status", SqlDbType.Int) { Value = (int)EventStatus.FeastPlanGenerated },
                new SqlParameter("@total", SqlDbType.Int) { Value = total },
                new SqlParameter("@closedAt", SqlDbType.DateTime) { Value = DateTime.UtcNow },
                new SqlParameter("@id", SqlDbType.Int) { Value = evt.Id },
                new SqlParameter("@open", SqlDbType.Int) { Value = (int)EventStatus.RsvpOpen });

            _db.Entry(evt).Reload();

            if (changed == 0) return false;

            NotifyKitchen(evt);
            return true;
        }

        // Events whose deadline day has ended
        public int CloseDueRsvps()
        {
            var today = _now().Date;

            var due = _db.CafeteriaEvents
                .Where(e => e.Status == EventStatus.RsvpOpen && e.RsvpDeadline.HasValue && e.RsvpDeadline < today)
                .ToList();

            int closed = 0;
            foreach (var evt in due)
            {
                if (CloseRsvp(evt)) closed++;
            }
            return closed;
        }

        public void NotifyKitchen(CafeteriaEvent evt)
        {
            var chefUserIds = _db.Users
                .Where(u => u.Role == "Chef")
                .Select(u => u.UserId)
                .ToList();

            if (chefUserIds.Count == 0) return;

            var message = string.Format(
                "Kitchen plan ready — {0} on {1}. {2} attending (from RSVPs).",
                evt.EventName,
                evt.EventDate.ToString("ddd dd MMM"),
                evt.GuaranteedHeadcount ?? 0);

            AddNotifications(chefUserIds, message, "FeastPlan", evt.Id);
        }

        // Tells everyone who accepted (with a login) about a change,
        // e.g. new time or cancellation, so they can update the event
        // in their own calendar
        public int NotifyAttendees(CafeteriaEvent evt, string message)
        {
            var userIds = _db.EventRsvps
                .Where(r => r.EventId == evt.Id && r.ResponseStatus == ResponseAttending && r.RespondedByUserId.HasValue)
                .Select(r => r.RespondedByUserId.Value)
                .Distinct()
                .ToList();

            AddNotifications(userIds, message, "CafeteriaEvent", evt.Id);
            return userIds.Count;
        }

        private void AddNotifications(IEnumerable<int> userIds, string message, string entityType, int entityId)
        {
            var now = DateTime.Now;
            foreach (var uid in userIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Message = message,
                    RelatedEntityType = entityType,
                    RelatedEntityId = entityId,
                    IsRead = false,
                    CreatedAt = now
                });
            }
            _db.SaveChanges();
        }

        // ============================================================
        // GOOGLE CALENDAR — "Add to Google Calendar" link with the
        // event's current details (no account connection; the
        // attendee adds it themselves)
        // ============================================================

        public static string GoogleCalendarUrl(CafeteriaEvent evt, string rsvpLink)
        {
            var start = evt.EventDate.Date + evt.StartTime;
            var end = evt.EventDate.Date + evt.EndTime;

            var details = (evt.Description ?? "").Trim();
            if (!string.IsNullOrEmpty(rsvpLink))
            {
                details = (details.Length > 0 ? details + "\n\n" : "") + "Your RSVP: " + rsvpLink;
            }

            string location = "";
            if (evt.Venue != null)
            {
                location = evt.Venue.Name + (string.IsNullOrWhiteSpace(evt.Venue.Location) ? "" : ", " + evt.Venue.Location);
            }
            location = (location.Length > 0 ? location + ", " : "") + "Michaelhouse";

            return "https://calendar.google.com/calendar/render?action=TEMPLATE"
                + "&text=" + Uri.EscapeDataString(evt.EventName)
                + "&dates=" + start.ToString("yyyyMMdd'T'HHmmss") + "/" + end.ToString("yyyyMMdd'T'HHmmss")
                + "&ctz=Africa/Johannesburg"
                + "&details=" + Uri.EscapeDataString(details)
                + "&location=" + Uri.EscapeDataString(location);
        }
    }

    // ============================================================
    // Attendee types (stored in EventRsvp.ResponderGroup)
    // ============================================================

    public static class AttendeeType
    {
        public const string Student = "Student";
        public const string Parent = "Parent";
        public const string Staff = "Staff";
        public const string Guest = "Guest";

        // Public-form groups (Board, Old Boy, Guest…) count as guests
        public static string Of(string responderGroup)
        {
            if (string.Equals(responderGroup, Student, StringComparison.OrdinalIgnoreCase)) return Student;
            if (string.Equals(responderGroup, Parent, StringComparison.OrdinalIgnoreCase)) return Parent;
            if (string.Equals(responderGroup, Staff, StringComparison.OrdinalIgnoreCase)) return Staff;
            return Guest;
        }
    }

    public class EventInvitation
    {
        public CafeteriaEvent Event { get; set; }
        public RsvpStatusInfo Status { get; set; }
        public EventRsvp Response { get; set; }
    }

    public class InvitedSummary
    {
        public int Students { get; set; }
        public int StudentsWithLogin { get; set; }
        public int Parents { get; set; }
        public int ParentsWithLogin { get; set; }
        public int StaffWithLogin { get; set; }

        public int CanBeNotified { get { return StudentsWithLogin + ParentsWithLogin + StaffWithLogin; } }
    }

    public class Invitee
    {
        public string Type { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int? StudentId { get; set; }
        public bool IsInvited { get; set; }
        public string NotInvitedReason { get; set; }
        public bool CanBringGuests { get; set; }

        // Students only — from their StudentProfile
        public StudentDietaryProfile ProfileDietary { get; set; }
    }

    public class InviteeRsvpInput
    {
        public bool Attending { get; set; }
        public DietaryProfileViewModel Dietary { get; set; }   // parents / staff
        public int? MenuItemId { get; set; }
        public List<GuestInput> Guests { get; set; }
        public string Comments { get; set; }
    }

    public class GuestInput
    {
        public string DietaryPreference { get; set; }
        public string DietaryNotes { get; set; }
        public int? MenuItemId { get; set; }

        // Optional cultural favourite dish (UC18 step 6), checked against
        // the meal library and stock when the feast plan is generated (UC19 step 3)
        public string CulturalFavoriteDish { get; set; }
    }

    public class RsvpMealOption
    {
        public RsvpMealOption()
        {
            SuitableFor = new List<string>();
        }

        public int MenuItemId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Allergens { get; set; }
        public List<string> SuitableFor { get; set; }
        public bool IsSuitable { get; set; }
        public string UnsuitableReason { get; set; }
    }

    public class RsvpResult
    {
        public RsvpResult()
        {
            Errors = new List<string>();
        }

        public bool Success { get; set; }
        public bool Attending { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }

        public static RsvpResult Fail(string message)
        {
            return new RsvpResult { Message = message, Errors = new List<string> { message } };
        }

        public static RsvpResult Fail(List<string> errors)
        {
            return new RsvpResult { Message = errors[0], Errors = errors };
        }
    }

    public class EventAttendee
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public StudentDietaryProfile Profile { get; set; }
        public int? MenuItemId { get; set; }
        public int RsvpId { get; set; }
        public bool IsGuest { get; set; }
    }

    public class EventAttendance
    {
        public int Students { get; set; }
        public int Parents { get; set; }
        public int Staff { get; set; }
        public int Guests { get; set; }   // additional guests + public-form respondents
        public int Total { get { return Students + Parents + Staff + Guests; } }

        public int Responses { get; set; }
        public int Accepted { get; set; }
        public int Declined { get; set; }
    }

    // ============================================================
    // Opens scheduled RSVPs and closes them after the deadline, on
    // time. Runs every minute inside the web app; if the app was
    // asleep, the next page that shows an event catches up (both are
    // also called there).
    // ============================================================

    public static class EventRsvpScheduler
    {
        private static Timer _timer;
        private static int _running;

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(Tick, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
        }

        public static void Stop()
        {
            var timer = _timer;
            _timer = null;
            if (timer != null) timer.Dispose();
        }

        private static void Tick(object state)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;

            try
            {
                using (var db = new DBContextClass())
                {
                    var rsvp = new EventRsvpService(db);
                    rsvp.OpenDueRsvps(null);
                    rsvp.CloseDueRsvps();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Scheduled RSVP opening failed: " + ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }
}
