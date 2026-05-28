namespace tramsac99.Areas.Admin.Models.Dto
{
    public class CreateChargingPoleRequest
    {
        public int StationId { get; set; }
        public string? PoleCode { get; set; }
        public string? ChargerType { get; set; }
        public string? MaxPower { get; set; }
        public string? Status { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }
        public int PortCount { get; set; } // Legacy field kept for compatibility, no longer used.
    }
}
