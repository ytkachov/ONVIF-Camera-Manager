using System.ComponentModel.DataAnnotations;

namespace OnvifManager.Web.Contracts;

public sealed class UpdateCameraRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(45, MinimumLength = 1)]
    public string Ip { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int? Port { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    // Null/empty preserves the stored password.
    [StringLength(200)]
    public string? Password { get; set; }
}
