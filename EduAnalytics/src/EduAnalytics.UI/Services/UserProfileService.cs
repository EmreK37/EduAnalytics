using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EduAnalytics.UI.Services;

public enum UserGender
{
    Unspecified,
    Female,
    Male
}

public sealed class UserProfile
{
    public string Username { get; set; } = string.Empty;
    public UserGender Gender { get; set; } = UserGender.Unspecified;
    public string? PhotoPath { get; set; }
    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public interface IUserProfileService
{
    bool HasUser { get; }
    UserProfile Current { get; }
    void CreateUser(string username, string password, UserGender gender = UserGender.Unspecified, string? sourcePhotoPath = null);
    bool SignIn(string username, string password);
    bool ValidatePassword(string? password);
    void UpdateProfile(string username, UserGender gender, string? currentPassword, string? newPassword, string? sourcePhotoPath, bool removePhoto);
    string GetAvatarText();
    string? GetPhotoPath();
}

public sealed class UserProfileService : IUserProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _profileDir;
    private readonly string _profileFile;

    public UserProfile Current { get; private set; } = new();

    public bool HasUser => File.Exists(_profileFile) && !string.IsNullOrWhiteSpace(Current.Username);

    public UserProfileService()
    {
        _profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EduAnalytics");
        _profileFile = Path.Combine(_profileDir, "user-profile.json");
        Directory.CreateDirectory(_profileDir);
        Load();
    }

    public void CreateUser(string username, string password, UserGender gender = UserGender.Unspecified, string? sourcePhotoPath = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Kullanıcı adını girmediniz.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Şifreyi girmediniz.");

        var salt = CreateSalt();
        Current = new UserProfile
        {
            Username = username.Trim(),
            Gender = gender,
            PasswordSalt = salt,
            PasswordHash = HashPassword(password, salt),
            PhotoPath = CopyPhoto(sourcePhotoPath)
        };
        Save();
    }

    public bool SignIn(string username, string password)
    {
        Load();
        return string.Equals(Current.Username, username.Trim(), StringComparison.Ordinal) &&
               ValidatePassword(password);
    }

    public bool ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(Current.PasswordSalt) ||
            string.IsNullOrWhiteSpace(Current.PasswordHash))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Current.PasswordHash),
            Encoding.UTF8.GetBytes(HashPassword(password, Current.PasswordSalt)));
    }

    public void UpdateProfile(
        string username,
        UserGender gender,
        string? currentPassword,
        string? newPassword,
        string? sourcePhotoPath,
        bool removePhoto)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Kullanıcı adını girmediniz.");

        Current.Username = username.Trim();
        Current.Gender = gender;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (ValidatePassword(newPassword))
                throw new InvalidOperationException("Yeni şifre mevcut şifreyle aynı olamaz.");

            var salt = CreateSalt();
            Current.PasswordSalt = salt;
            Current.PasswordHash = HashPassword(newPassword, salt);
        }

        if (removePhoto)
            Current.PhotoPath = null;
        else if (!string.IsNullOrWhiteSpace(sourcePhotoPath))
            Current.PhotoPath = CopyPhoto(sourcePhotoPath);

        Save();
    }

    public string GetAvatarText()
    {
        if (!string.IsNullOrWhiteSpace(Current.Username))
            return Current.Username.Trim()[0].ToString().ToUpperInvariant();

        return "EA";
    }

    public string? GetPhotoPath()
        => !string.IsNullOrWhiteSpace(Current.PhotoPath) && File.Exists(Current.PhotoPath)
            ? Current.PhotoPath
            : null;

    private void Load()
    {
        if (!File.Exists(_profileFile))
            return;

        try
        {
            Current = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(_profileFile)) ?? new UserProfile();
        }
        catch
        {
            Current = new UserProfile();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(_profileDir);
        File.WriteAllText(_profileFile, JsonSerializer.Serialize(Current, JsonOptions));
    }

    private string? CopyPhoto(string? sourcePhotoPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePhotoPath) || !File.Exists(sourcePhotoPath))
            return Current.PhotoPath;

        var extension = Path.GetExtension(sourcePhotoPath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var target = Path.Combine(_profileDir, $"avatar-{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePhotoPath, target, overwrite: true);
        return target;
    }

    private static string CreateSalt()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var input = Encoding.UTF8.GetBytes($"{salt}:{password}");
        return Convert.ToBase64String(SHA256.HashData(input));
    }
}

