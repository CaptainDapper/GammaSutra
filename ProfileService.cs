using System.IO;
using System.Text.Json;
using GammaControl.Models;

namespace GammaControl;

public class ProfileService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GammaSutra");

    private static readonly string ProfilesPath = Path.Combine(DataDir, "profiles.json");

    // Migrate data from the old GammaControl folder on first run
    private static void MigrateIfNeeded()
    {
        if (Directory.Exists(DataDir)) return;
        var oldDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GammaControl");
        var oldFile = Path.Combine(oldDir, "profiles.json");
        if (!File.Exists(oldFile)) return;
        Directory.CreateDirectory(DataDir);
        File.Copy(oldFile, ProfilesPath, overwrite: false);
    }

    public List<Profile> Profiles { get; private set; } = new();
    public string? LastUsedProfileName { get; set; }

    public void Load()
    {
        MigrateIfNeeded();
        if (!File.Exists(ProfilesPath))
        {
            Profiles = new List<Profile> { CreateDefaultProfile() };
            return;
        }
        try
        {
            var json = File.ReadAllText(ProfilesPath);
            var data = JsonSerializer.Deserialize<ProfileData>(json);
            Profiles = data?.Profiles is { Count: > 0 } list ? list : new List<Profile> { CreateDefaultProfile() };
            LastUsedProfileName = data?.LastUsedProfileName;
        }
        catch
        {
            Profiles = new List<Profile> { CreateDefaultProfile() };
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        var data = new ProfileData { Profiles = Profiles, LastUsedProfileName = LastUsedProfileName };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ProfilesPath, json);
    }

    public Profile? GetProfile(string name) => Profiles.FirstOrDefault(p => p.Name == name);

    public void AddOrUpdate(Profile profile)
    {
        var idx = Profiles.FindIndex(p => p.Name == profile.Name);
        if (idx >= 0)
            Profiles[idx] = profile;
        else
            Profiles.Add(profile);
    }

    public void Delete(string name) => Profiles.RemoveAll(p => p.Name == name);

    private static Profile CreateDefaultProfile() => new() { Name = "Default" };

    private class ProfileData
    {
        public List<Profile> Profiles { get; set; } = new();
        public string? LastUsedProfileName { get; set; }
    }
}
