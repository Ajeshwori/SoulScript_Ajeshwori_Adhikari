using System;
using System.Collections.Generic;
using System.Text;

namespace SoulScript.Model;

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
