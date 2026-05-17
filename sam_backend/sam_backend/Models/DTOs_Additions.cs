// sam/backend/Models/DTOs_Additions.cs
// Replace the existing DTOs_Additions.cs with this file.

namespace SamErpBackend.Models
{
    // ── Status / Product (already existed) ───────────────────────────────────

    public class StatusRequest
    {
        public string StatusName { get; set; } = string.Empty;
    }

    public class ProductRequest
    {
        public string ProductName { get; set; } = string.Empty;
    }

    // ── Language Master ───────────────────────────────────────────────────────

    public class LanguageRequest
    {
        public string LanguageName { get; set; } = string.Empty;
    }

    // ── Lead Master ───────────────────────────────────────────────────────────

    public class LeadRequest
    {
        public string? LeadDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? LanguageId { get; set; }
        public string? ContactNo { get; set; }
        public string? Location { get; set; }
        public int? StatusId { get; set; }
        public int? ProductId { get; set; }
        public string? State { get; set; }
        public string? Machine { get; set; }
        public string? Moc { get; set; }
        public string? LeadRemarks { get; set; }
        public string? ContactPerson { get; set; }
        public string? TlName { get; set; }
    }

    // ── Event Master ──────────────────────────────────────────────────────────

    public class EventRequest
    {
        public string? EventDate { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Outcome { get; set; }
        public string? NextFollowUpDate { get; set; }
        public string? EventRemarks { get; set; }
    }

    // ── Change Password ───────────────────────────────────────────────────────

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}