using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SoulScript.Data;
using SoulScript.Model;

namespace SoulScript.Services;

public class SecurityService
{
    private readonly ApplicationDbContext _db;

    // Current session authenticated/unlocked?
    public bool IsAuthenticated { get; private set; } = false;

    public SecurityService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Has user set a PIN?
    public async Task<bool> HasPinAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync();
        
        // Check for null or empty hash
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return false;

        // CRITICAL FIX: If the hash is the hash of an empty string, treat as NO PIN.
        // This fixes the state where the user "removed" the PIN but it stored a hash of "".
        var emptyHash = ComputeHash("");
        if (user.PasswordHash == emptyHash)
            return false;

        return true;
    }

    // Is app-lock enabled?
    public async Task<bool> IsLockEnabledAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync();
        return user?.IsAppLocked ?? false;
    }

    // Verify PIN and unlock session
    public async Task<bool> VerifyPinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;

        var user = await _db.Users.FirstOrDefaultAsync();
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash)) return false;

        var inputHash = ComputeHash(pin);

        if (user.PasswordHash == inputHash)
        {
            IsAuthenticated = true;
            return true;
        }

        return false;
    }

    // Set new PIN and (optionally) enable lock
    public async Task SetPinAsync(string pin, bool enableLock)
    {
        var user = await _db.Users.FirstOrDefaultAsync();

        if (user == null)
        {
            user = new User { CreatedAt = DateTime.Now };
            _db.Users.Add(user);
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            user.PasswordHash = null;
            user.IsAppLocked = false;
        }
        else
        {
            user.PasswordHash = ComputeHash(pin);
            user.IsAppLocked = enableLock;
        }

        // After setting PIN, consider session unlocked
        IsAuthenticated = true;

        await _db.SaveChangesAsync();
    }

    public async Task SetLockStateAsync(bool enabled)
    {
        var user = await _db.Users.FirstOrDefaultAsync();
        if (user == null) return;

        user.IsAppLocked = enabled;
        await _db.SaveChangesAsync();
    }

    public void Unlock() => IsAuthenticated = true;

    public void Lock() => IsAuthenticated = false;

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
