using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace backend.Models
{
    public class Credentials
    {
        [RegularExpression(@"^\d{6}-?\d{3}$", ErrorMessage = "Fel format på golfId")]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Password missing")]
        public string Password { get; set; }
    }
}
