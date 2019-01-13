using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using UWE;

namespace SubnauticaPhoton
{
    [HarmonyPatch(typeof(FreezeTime))]
    [HarmonyPatch("Begin")]
    public class PauseGamePatch
    {
        public static bool Prefix(string userId, bool dontPauseSound)
        {
            if(userId == "FeedbackPanel")
            {
                return true;
            }
            return false;
        }
    }
}
