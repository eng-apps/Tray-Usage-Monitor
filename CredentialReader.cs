using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ClaudeUsageMonitor;

/// <summary>
/// Liest den OAuth Access Token von Claude Code.
/// 
/// Suchpfade (in Reihenfolge):
///   1. Windows Credential Manager: "Claude Code-credentials"
///   2. %USERPROFILE%\.claude\.credentials.json
///   3. %HOMEDRIVE%%HOMEPATH%\.claude\.credentials.json  (Fallback)
/// </summary>
public static class CredentialReader
{
    private const string CredentialName = "Claude Code-credentials";
    private const string FileName = ".credentials.json";
    private const string DirName = ".claude";

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    /// <summary>
    /// Liest den OAuth Access Token.
    /// </summary>
    public static string? GetAccessToken()
    {
        // --- Versuch 1: Credential Manager ---
        try
        {
            var json = ReadFromCredentialManager();
            if (json != null)
            {
                Log($"Token aus Credential Manager gelesen ({json.Length} Bytes)");
                var token = ExtractAccessToken(json);
                if (token != null) return token;
                Log("Credential Manager: JSON gefunden aber kein accessToken extrahierbar");
            }
            else
            {
                Log("Credential Manager: Kein Eintrag 'Claude Code-credentials' gefunden");
            }
        }
        catch (Exception ex)
        {
            Log($"Credential Manager Fehler: {ex.Message}");
        }

        // --- Versuch 2: Datei (mehrere Pfade) ---
        foreach (var path in GetCredentialFilePaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    Log($"Datei nicht vorhanden: {path}");
                    continue;
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                Log($"Datei gelesen: {path} ({json.Length} Bytes)");

                if (!json.Contains("claudeAiOauth"))
                {
                    Log($"Datei enthält kein 'claudeAiOauth': {path}");
                    continue;
                }

                var token = ExtractAccessToken(json);
                if (token != null)
                {
                    Log($"Token aus Datei extrahiert: {path}");
                    return token;
                }

                Log($"Datei enthält claudeAiOauth aber kein accessToken: {path}");
            }
            catch (Exception ex)
            {
                Log($"Datei-Fehler ({path}): {ex.Message}");
            }
        }

        Log("KEIN TOKEN GEFUNDEN in keiner Quelle");
        return null;
    }

    /// <summary>Alle möglichen Pfade für die Credentials-Datei.</summary>
    private static IEnumerable<string> GetCredentialFilePaths()
    {
        // Primär: %USERPROFILE%\.claude\.credentials.json
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrEmpty(userProfile))
            yield return Path.Combine(userProfile, DirName, FileName);

        // Fallback: Environment.SpecialFolder.UserProfile
        var specialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(specialFolder) && specialFolder != userProfile)
            yield return Path.Combine(specialFolder, DirName, FileName);

        // Fallback: %HOMEDRIVE%%HOMEPATH%
        var homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
        var homePath = Environment.GetEnvironmentVariable("HOMEPATH");
        if (!string.IsNullOrEmpty(homeDrive) && !string.IsNullOrEmpty(homePath))
        {
            var combined = homeDrive + homePath;
            if (combined != userProfile && combined != specialFolder)
                yield return Path.Combine(combined, DirName, FileName);
        }
    }

    private static string? ReadFromCredentialManager()
    {
        if (!CredReadW(CredentialName, 1, 0, out var credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize <= 0) return null;

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);

            // UTF-8 versuchen (Standard)
            var text = Encoding.UTF8.GetString(bytes);
            if (text.Contains("claudeAiOauth")) return text;

            // UTF-16 Fallback
            text = Encoding.Unicode.GetString(bytes);
            if (text.Contains("claudeAiOauth")) return text;

            Log($"Credential Manager: Blob gelesen ({cred.CredentialBlobSize} Bytes) aber kein claudeAiOauth drin");
            return null;
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    private static string? ExtractAccessToken(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) &&
                oauth.TryGetProperty("accessToken", out var token))
            {
                var val = token.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    Log($"accessToken extrahiert: {val[..Math.Min(20, val.Length)]}...");
                    return val;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"JSON-Parse-Fehler: {ex.Message}");
        }
        return null;
    }

    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine($"[CredentialReader] {msg}");
    }
}
