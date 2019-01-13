using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UnityEngine;

namespace SubnauticaPhoton
{
    [HarmonyPatch(typeof(EscapePod))]
    [HarmonyPatch("StartAtPosition")]
    public class LifepodStartPatch
    {
        public static void Prefix(Vector3 position)
        {
            if(PhotonNetwork.inRoom)
            {
                Debug.Log("Setting lifepod start position to zero");
                position = Vector3.zero;
            }
        }
    }
}
