using System.ComponentModel.DataAnnotations;

namespace tramsac99.Areas.User.ViewModels
{
    public class BulkAddPoleRequestViewModel
    {
        [Required]
        public int StationId { get; set; }

        public List<PoleDraftItemViewModel> Poles { get; set; } = new();
    }

    public class PoleDraftItemViewModel
    {
        public string? PoleCode { get; set; }
        public string? MaxPower { get; set; }
        public string? Note { get; set; }
    }
}