using System.IO;
using UnityEngine;

public class ApplicationSettings : MonoBehaviour
{
    [System.Serializable]
    public struct AppSettings
    {
        public MatchMakingSettings  MatchMakingSettings;
        public GameSettings         GameSettings;
    }

    [System.Serializable]
    public struct MatchMakingSettings
    {
        public string   MatchMakingServerIp;
        public int      MatchMakingServerPortMatchServers;
        public ushort   MatchMakingServerPortClients;
        public ushort[] MatchServerPorts;
        public uint     InitialCompatibleEloGap;
        public uint     CompatibleEloGapUpdateValue;
        public uint     EloGapUpdateInterval;
    }

    [System.Serializable]
    public struct GameSettings
    {
        public string   MatchServerIp;
        public uint     RequiredKillsToWin;
        public ushort   EloUpdateValue;
        public int      MinEloAllowed;
    }

    private static ApplicationSettings _instance;

    public static ApplicationSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ApplicationSettings>();
                if (_instance == null)
                {
                    _instance = new GameObject("ApplicationSettings", 
                        typeof(ApplicationSettings)).GetComponent<ApplicationSettings>();
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    public AppSettings Settings;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);

        string settingsFileName = "AppSettings.json";
        string currentPath = Directory.GetCurrentDirectory();
        string settingsPath = Path.Combine(currentPath, settingsFileName);

        if (File.Exists(settingsPath))
            Settings = GetApplicationSettings(settingsPath);
        else
        {
            CreateApplicationSettings(settingsPath);
            Settings = GetApplicationSettings(settingsPath);
        }   
    }

    private void CreateApplicationSettings(string path)
    {
        Debug.Log("Creating settings");
        AppSettings appSettings = new AppSettings();

        appSettings.MatchMakingSettings.MatchMakingServerIp = "127.0.0.1";
        appSettings.MatchMakingSettings.MatchMakingServerPortMatchServers = 8000;
        appSettings.MatchMakingSettings.MatchMakingServerPortClients = 7777;
        appSettings.MatchMakingSettings.MatchServerPorts = new ushort[] {8885, 8886, 8887, 8888};
        appSettings.MatchMakingSettings.InitialCompatibleEloGap = 50;
        appSettings.MatchMakingSettings.CompatibleEloGapUpdateValue = 50;
        appSettings.MatchMakingSettings.EloGapUpdateInterval = 5;

        appSettings.GameSettings.MatchServerIp = "127.0.0.1";
        appSettings.GameSettings.RequiredKillsToWin = 2;
        appSettings.GameSettings.EloUpdateValue = 10;
        appSettings.GameSettings.MinEloAllowed = 100;

        string appSettingsJson = JsonUtility.ToJson(appSettings, true);
        File.WriteAllText(path, appSettingsJson);
    }

    private AppSettings GetApplicationSettings(string path)
    {
        string appSettingsJson = File.ReadAllText(path);

        return JsonUtility.FromJson<AppSettings>(appSettingsJson);
    }
}
