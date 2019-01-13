using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;

namespace SubnauticaPhoton
{
    [HarmonyPatch(typeof(IngameMenu), "QuitGame")]
    public class ReturnToMenuPatch
    {
        public static void Postfix()
        {
            if(PhotonNetwork.inRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }
    }
}
