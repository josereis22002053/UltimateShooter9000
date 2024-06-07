using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplicationStarter : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log(ApplicationSettings.Instance.Settings.MatchMakingSettings.MatchMakingServerIp);
    }

    private void Start()
    {
        // Parse command line arguments
        string[] args = System.Environment.GetCommandLineArgs();
//         for (int i = 0; i < args.Length; i++)
//         {
//             if (args[i] == "--gameServer")
//             {
//                 SceneManager.LoadScene(2);
//                 break;
//             }
//             else
//             {
//                 SceneManager.LoadScene(1);
//                 break;
//             }
//         }

        if (args.Contains("--gameServer"))
        {
            SceneManager.LoadScene(2);
        }
        else
        {
            SceneManager.LoadScene(1);
        }
    }
}


