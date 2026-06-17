using System.ComponentModel.DataAnnotations;

namespace interview_dotnet_api.DTOs
{
    public class CreateNotificationDto
    {
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
    }
}
