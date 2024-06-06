using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplicationStarter : MonoBehaviour
{
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
