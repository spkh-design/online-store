using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

public class User : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    
    public void UpdateProfile(string fullName, string? phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
    }
    
    public void Deactivate() => IsActive = false;
}