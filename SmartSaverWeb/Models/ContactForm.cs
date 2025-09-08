using System.ComponentModel.DataAnnotations;

namespace PaynlesWeb.Models
{
    public class ContactForm
    {
        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(200)]
        public string Email { get; set; }

        [Required, StringLength(150)]
        public string Subject { get; set; }

        [Required, StringLength(4000)]
        public string Message { get; set; }

        [Display(Name = "I agree to be contacted about my request"), Range(typeof(bool), "true", "true", ErrorMessage = "Please confirm you agree to be contacted.")]
        public bool AgreeToContact { get; set; }
    }
}
