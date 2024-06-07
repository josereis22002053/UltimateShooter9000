using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectedClientInfo : MonoBehaviour
{
    public ulong ClientID;
    public string UserName;
    public string Password;
    public int Elo;
    public uint EloGapMatching;
    public float TimeSinceLastGapUpdate;
    public bool FoundMatch;
}
