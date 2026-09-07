using Microsoft.AspNetCore.Identity;

namespace CloudMVCApplication.Models
{
    public class ApplicationRole : IdentityRole
    {
        public string Description { get; set; } = string.Empty;
    }
}