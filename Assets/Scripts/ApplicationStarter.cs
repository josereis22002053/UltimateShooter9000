using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplicationStarter : MonoBehaviour
{
    public bool IsServer;
    
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("Application starter scene");
        // Parse command line arguments
        string[] args = System.Environment.GetCommandLineArgs();

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
