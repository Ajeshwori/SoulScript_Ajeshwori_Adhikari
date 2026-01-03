using System;

namespace SoulScript.Model;

public class User
{
    public int UserID { get; set; } = 1; // single user
    public string Username { get; set; } = "Owner";
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAppLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
