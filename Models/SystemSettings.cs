using System.ComponentModel.DataAnnotations.Schema;

namespace SimpleApiReseller.Models
{
    public class SystemSettings
    {
        public int Id { get; set; }
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        // Change this line:
        [Column(TypeName = "timestamp")]
        public DateTime UpdatedAt { get; set; }

    }
}
