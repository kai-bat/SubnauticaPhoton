using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using Harmony;
using System.Reflection;
using System.IO;

namespace SubnauticaPhoton
{
    public static class MainPatch
    {
        public static ServerSettings photonSettings;
        public static NetworkHandler handler;
        public static MultiplayerPanel panel;

        public static GUISkin subnauticaSkin;

        /// <summary>
        /// Executed by QMM, should only be executed then
        /// </summary>
        public static void Patch()
        {
            photonSettings = ScriptableObject.CreateInstance<ServerSettings>();

            photonSettings.AppID = "04c651af-6ffe-4039-a2aa-0f9b4fa07d0e";

            photonSettings.HostType = ServerSettings.HostingOption.PhotonCloud;
            photonSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.Udp;
            photonSettings.PreferredRegion = CloudRegionCode.au;

            PhotonNetwork.PhotonServerSettings = photonSettings;

            SceneManager.sceneLoaded += SceneLoad;

            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Environment.CurrentDirectory, "QMods/SubnauticaOnline/subnauticagui.assets"));
            subnauticaSkin = (GUISkin)bundle.LoadAsset("SubnauticaGUI.guiskin");

            HarmonyInstance harmony = HarmonyInstance.Create("SubnauticaPhoton");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void SceneLoad(Scene scene, LoadSceneMode mode)
        {
            if(scene.name == "XMenu")
            {
                ModMainMenu();
                GameObject managerObj = new GameObject("Manager");
                handler = managerObj.AddComponent<NetworkHandler>();
            }
        }

        /// <summary>
        /// Creates the UI necessary to load up the photon multiplayer mode (Mostly stolen from Nitrox)
        /// </summary>
        static void ModMainMenu()
        {
            GameObject playButton = GameObject.Find("Menu canvas/Panel/MainMenu/PrimaryOptions/MenuButtons/ButtonPlay");
            GameObject mpButton = GameObject.Instantiate(playButton, playButton.transform.parent);


            mpButton.name = "MultiplayerButton_Object";
            Text buttonText = mpButton.transform.Find("Circle/Bar/Text").GetComponent<Text>();
            buttonText.text = "Multiplayer";
            GameObject.Destroy(buttonText.GetComponent<LanguageUpdater>());
            mpButton.transform.SetSiblingIndex(3);

            Button mpButtonInteract = mpButton.GetComponent<Button>();
            mpButtonInteract.onClick.RemoveAllListeners();
            mpButtonInteract.onClick.AddListener(() => panel.ShowMPPanel());

            MainMenuRightSide rightSide = MainMenuRightSide.main;
            GameObject savedGamesRef = rightSide.transform.Find("SavedGames").gameObject;
            GameObject LoadedMultiplayer = GameObject.Instantiate(savedGamesRef, rightSide.transform);
            LoadedMultiplayer.name = "Multiplayer";
            LoadedMultiplayer.transform.Find("Header").GetComponent<Text>().text = "Multiplayer";
            UnityEngine.Object.Destroy(LoadedMultiplayer.transform.Find("Scroll View/Viewport/SavedGameAreaContent/NewGame").gameObject);
            UnityEngine.Object.Destroy(LoadedMultiplayer.GetComponent<MainMenuLoadPanel>());

            panel = LoadedMultiplayer.AddComponent<MultiplayerPanel>();
            panel.savedGamesPanel = savedGamesRef;
            panel.multiplayerPanel = LoadedMultiplayer;

            rightSide.groups.Add(LoadedMultiplayer);
        }

        /// <summary>
        /// Can be used to remove all components excluding an 'essential' list from a GameObject and its children
        /// </summary>
        /// <param name="gobject">GameObject to modify</param>
        /// <param name="typesToExclude">The selected whitelisted types to not remove</param>
        public static void RemoveNonEssentialComponents(this GameObject gobject, List<Type> typesToExclude)
        {
            foreach(Component comp in gobject.GetAllComponentsInChildren<Component>())
            {
                if(typesToExclude.Contains(comp.GetType()) == false)
                {
                    Debug.Log("Removing " + comp.GetType().ToString() + " from GameObject " + comp.gameObject.name);
                    UnityEngine.Object.Destroy(comp);
                }
            }
        }
    }
}
