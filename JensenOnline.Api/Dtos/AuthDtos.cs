using System.ComponentModel.DataAnnotations;

namespace JensenOnline.Api.Dtos;

public class RegisterDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}

public record UserInfoDto(string Id, string Email, IList<string> Roles);