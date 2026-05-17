namespace SamErpBackend.Models
{
    // ── Request DTOs ──────────────────────────────────────────────────────────

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "user";
        public string Email { get; set; } = string.Empty;
        public string ActiveStatus { get; set; } = "active";
    }

    public class CreateLeadRequest
    {
        public string? LeadDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? LanguageId { get; set; }
        public string? ContactNo { get; set; }
        public string? Location { get; set; }
        public int? StatusId { get; set; }
        public int? ProductId { get; set; }

        // ✅ FIX: state and moc are plain VARCHAR in lead_master — NOT foreign keys.
        //         Changed from int? StateId / int? MocId to string? State / string? Moc.
        public string? State { get; set; }
        public string? Moc { get; set; }

        public string? LeadRemarks { get; set; }
        public string? ContactPerson { get; set; }
    }

    public class UpdateLeadRequest : CreateLeadRequest { }

    public class CreateEventRequest
    {
        public int LeadId { get; set; }
        public string? EventDate { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? Outcome { get; set; }
        public string? NextFollowUpDate { get; set; }
        public string? EventRemarks { get; set; }
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
    }

    public class MeResponse
    {
        public bool IsAuthenticated { get; set; }
        public string? Username { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
    }
}