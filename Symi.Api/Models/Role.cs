using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class Role
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(30)]
    public string Name { get; set; } = string.Empty;
}