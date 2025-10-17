using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class UserRole
{
    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}