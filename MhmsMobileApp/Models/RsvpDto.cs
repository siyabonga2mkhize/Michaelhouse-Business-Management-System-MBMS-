using System.Collections.Generic;

namespace MhmsMobileApp.Models
{
    // ============================================================
    // UC18 — logged-in RSVP ("My Events")
    // Matches the JSON from the web app's MyEventsApiController:
    //   GET  /api/my-events
    //   GET  /api/my-events/{id}
    //   POST /api/my-events/{id}/meal-options
    //   POST /api/my-events/{id}
    // ============================================================

    public class MyEventsDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public List<MyEventSummaryDto> Events { get; set; } = new List<MyEventSummaryDto>();
    }

    public class MyEventSummaryDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = "";
        public string DateLabel { get; set; } = "";
        public string TimeLabel { get; set; } = "";
        public string VenueName { get; set; } = "";
        public bool IsOpen { get; set; }
        public bool IsScheduled { get; set; }
        public string StatusLabel { get; set; } = "";
        public bool HasResponded { get; set; }
        public string? Answer { get; set; }
    }

    public class EventRsvpDetailDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";

        public int EventId { get; set; }
        public string EventName { get; set; } = "";
        public string DateLabel { get; set; } = "";
        public string TimeLabel { get; set; } = "";
        public string VenueName { get; set; } = "";
        public string? RsvpDeadline { get; set; }

        public bool IsOpen { get; set; }
        public string? ClosedReason { get; set; }

        public InviteeDto Invitee { get; set; } = new InviteeDto();
        public int MaxGuests { get; set; }
        public bool OffersMealChoice { get; set; }

        public RsvpResponseDto? Response { get; set; }
        public ProfileSummaryDto ProfileSummary { get; set; } = new ProfileSummaryDto();
        public DietaryFormDto? Dietary { get; set; }
        public DietaryOptionsDto Options { get; set; } = new DietaryOptionsDto();
        public List<RsvpMealDto> Meals { get; set; } = new List<RsvpMealDto>();

        public bool IsStudent => Invitee.Type == "Student";
    }

    public class InviteeDto
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";          // Student / Parent / Staff
        public bool IsInvited { get; set; }
        public string? NotInvitedReason { get; set; }
        public bool CanBringGuests { get; set; }
    }

    public class RsvpResponseDto
    {
        public bool Attending { get; set; }
        public int? MenuItemId { get; set; }
        public string? Comments { get; set; }
        public List<GuestDto> Guests { get; set; } = new List<GuestDto>();
    }

    public class ProfileSummaryDto
    {
        public string? Preference { get; set; }
        public string? Allergies { get; set; }
        public string? Medical { get; set; }
    }

    // Same fields as the web's DietaryProfileViewModel
    public class DietaryFormDto
    {
        public string DietaryPreference { get; set; } = "None";
        public string? DietaryPreferenceOther { get; set; }
        public List<string> SelectedAllergies { get; set; } = new List<string>();
        public string? OtherAllergies { get; set; }
        public List<string> SelectedMedicalRestrictions { get; set; } = new List<string>();
        public string? MedicalRestrictionOther { get; set; }
        public string? DietaryNotes { get; set; }
    }

    public class DietaryOptionsDto
    {
        public List<OptionDto> Preferences { get; set; } = new List<OptionDto>();
        public List<OptionDto> Allergies { get; set; } = new List<OptionDto>();
        public List<OptionDto> MedicalRestrictions { get; set; } = new List<OptionDto>();
    }

    public class OptionDto
    {
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public class RsvpMealDto
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Allergens { get; set; }
        public List<string> SuitableFor { get; set; } = new List<string>();
        public bool IsSuitable { get; set; }
        public string? UnsuitableReason { get; set; }
    }

    public class GuestDto
    {
        public string DietaryPreference { get; set; } = "None";
        public string? DietaryNotes { get; set; }
        public int? MenuItemId { get; set; }
    }

    // POST /api/my-events/{id}/meal-options
    public class MealOptionsRequestDto
    {
        public DietaryFormDto Dietary { get; set; } = new DietaryFormDto();
    }

    public class MealOptionsDto
    {
        public bool Ok { get; set; }
        public string Error { get; set; } = "";
        public List<RsvpMealDto> Meals { get; set; } = new List<RsvpMealDto>();
    }

    // POST /api/my-events/{id} — same fields as the web's InviteeRsvpInput
    public class InviteeRsvpRequestDto
    {
        public bool Attending { get; set; }
        public int? MenuItemId { get; set; }
        public string? Comments { get; set; }
        public DietaryFormDto? Dietary { get; set; }
        public List<GuestDto> Guests { get; set; } = new List<GuestDto>();
    }

    public class RsvpSubmitResultDto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public bool Attending { get; set; }
        public string? Message { get; set; }
    }
}
