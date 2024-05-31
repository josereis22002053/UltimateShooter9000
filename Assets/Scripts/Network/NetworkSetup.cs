using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Debug = UnityEngine.Debug;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
using System.Diagnostics;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
#endif



public class NetworkSetup : MonoBehaviour
{
    [SerializeField] private bool forceServer = false;

    private bool isServer = false;

    private IEnumerator Start()
    {
        // Parse command line arguments
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server")
            {
                // --server found, this should be a server application
                isServer = true;
            }
        }

#if UNITY_EDITOR
        if (forceServer) isServer = true;
#endif

        if (isServer)
            yield return StartAsServerCR();
        else
            yield return StartAsClientCR();
    }

    private IEnumerator StartAsServerCR()
    {
        var networkManager = GetComponent<NetworkManager>();
        networkManager.enabled = true;
        var transport = GetComponent<UnityTransport>();
        transport.enabled = true;

        // Wait a frame for setups to be done
        yield return null;

        if (networkManager.StartServer())
        {
            Debug.Log($"Serving on port {transport.ConnectionData.Port}...");
        }
        else
        {
            Debug.LogError($"Failed to serve on port {transport.ConnectionData.Port}...");
        }

        SetWindowTitle("UltimateShooter9000 - Server");
    }

    private IEnumerator StartAsClientCR()
    {
        var networkManager = GetComponent<NetworkManager>();
        networkManager.enabled = true;
        var transport = GetComponent<UnityTransport>();
        transport.enabled = true;

        // Wait a frame for setups to be done
        yield return null;

        if (networkManager.StartClient())
        {
            Debug.Log($"Connecting on port {transport.ConnectionData.Port}...");
        }
        else
        {
            Debug.LogError($"Failed to connect on port {transport.ConnectionData.Port}...");
        }

        SetWindowTitle("UltimateShooter9000 - Client");
    }

    #if UNITY_STANDALONE_WIN
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowText(IntPtr hWnd, string lpString);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern IntPtr EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        // Delegate to filter windows
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private static IntPtr FindWindowByProcessId(uint processId)
        {
            IntPtr windowHandle = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                uint windowProcessId;
                GetWindowThreadProcessId(hWnd, out windowProcessId);
                if (windowProcessId == processId)
                {
                    windowHandle = hWnd;
                    return false; // Found the window, stop enumerating
                }
                return true; // Continue enumerating
            }, IntPtr.Zero);
            return windowHandle;
        }

        static void SetWindowTitle(string title)
        {
#if !UNITY_EDITOR
        uint processId = (uint)Process.GetCurrentProcess().Id;
        IntPtr hWnd = FindWindowByProcessId(processId);
        if (hWnd != IntPtr.Zero)
        {
            SetWindowText(hWnd, title);
        }
#endif
    }
#else
    static void SetWindowTitle(string title)
    {
    }
#endif
}
