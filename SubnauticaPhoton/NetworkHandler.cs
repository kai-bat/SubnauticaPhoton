using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using Photon;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;
using UWE;
using System.Runtime.InteropServices;

namespace SubnauticaPhoton
{
    /// <summary>
    /// This class is the backbone of SubnauticaPhoton, it will manually sync player objects and, (in the future), world objects and story events
    /// </summary>
    public class NetworkHandler : PunBehaviour
    {
        string currentRoomName = "";
        string playerNameToUse = "";

        // Keeps track of which players we have spawned objects for, allowing us to avoid using RPCs
        public Dictionary<PhotonPlayer, int> objectIdsByPlayer = new Dictionary<PhotonPlayer, int>();

        public Dictionary<TechType, string> ClassIDForTechType;

        float playerCheckDelay = 1f;
        float nextCheckTime = 0f;
        Vector2 playerListScroll = Vector2.zero;

        // Allows us to wait until the remote player prefab is initialized before we start spawning it in
        bool canCreateRemotePlayerObjects = false;
        bool optionsPanelOpen = false;

        GameObject remotePlayerPrefab;
        PhotonView localPlayerView;
        Rect windowRect = new Rect(Screen.width-420, 30, 400, 0);

        // A list of types to exclude when purging non-essential components from the remote player clones
        public static readonly List<Type> remotePlayerEssentials = new List<Type>
        {
            typeof(PhotonView),
            typeof(PhotonTransformView),
            typeof(PhotonAnimatorView),
            typeof(SkinnedMeshRenderer),
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(Transform),
            typeof(RectTransform),
            typeof(NetPlayerSync)
        };

        void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if(PlayerPrefs.HasKey("PlayerNameCache"))
            {
                playerNameToUse = PlayerPrefs.GetString("PlayerNameCache");
            }
            if(PlayerPrefs.HasKey("RoomNameCache"))
            {
                currentRoomName = PlayerPrefs.GetString("RoomNameCache");
            }

            gameObject.AddComponent<PhotonView>().viewID = 1;

            // Using a GUID for now, will implement a UI for player names later
            PhotonNetwork.playerName = Guid.NewGuid().ToString();
            PhotonNetwork.autoCleanUpPlayerObjects = true;
            PhotonNetwork.logLevel = PhotonLogLevel.Full;

            PhotonNetwork.ConnectUsingSettings("0.1");
        }

        /// <summary>
        /// Loads the game world, adds necessary components to the player and creates a remote player prefab
        /// </summary>
        /// <returns></returns>
        public IEnumerator StartGame ()
        {
            // Using reflection to get this method, as it will load everything in one go
            MethodInfo info = typeof(uGUI_MainMenu).GetMethod("StartNewGame", BindingFlags.NonPublic | BindingFlags.Instance);
            // Using creative for now, easier for testing purposes
            IEnumerator startNewGame = (IEnumerator)info.Invoke(uGUI_MainMenu.main, new object[] { GameMode.Creative });
            StartCoroutine(startNewGame);

            // Now waiting until the game has fully finished loading before continuing
            yield return new WaitUntil(() => LargeWorldStreamer.main != null);
            yield return new WaitUntil(() => LargeWorldStreamer.main.IsReady() || LargeWorldStreamer.main.IsWorldSettled());
            yield return new WaitUntil(() => !PAXTerrainController.main.isWorking);

            ClassIDForTechType = typeof(CraftData).GetField("techMapping", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as Dictionary<TechType, string>;

            AddEssentialComponentsToPlayer();
            InitRemotePlayerPrefab();
        }

        /// <summary>
        /// Will create the scripts needed to sync the local players position, rotation and animation to others
        /// </summary>
        void AddEssentialComponentsToPlayer ()
        {
            GameObject playerBody = Player.main.transform.Find("body").gameObject;
            // Because we can't use PhotonNetwork.Instantiate(), we need to allocate a view ID manually
            int id = PhotonNetwork.AllocateViewID();

            localPlayerView = playerBody.AddComponent<PhotonView>();
            localPlayerView.viewID = id;
            localPlayerView.TransferOwnership(PhotonNetwork.player);
            localPlayerView.synchronization = ViewSynchronization.UnreliableOnChange;
            localPlayerView.ownershipTransfer = OwnershipOption.Takeover;

            // We can sync this ID to other players using PhotonPlayer.CustomProperties
            Hashtable playerProps = new Hashtable();
            playerProps.Add("ObjectID", id);

            PhotonNetwork.player.SetCustomProperties(playerProps);

            // Probably need to change some properties with these, will do later
            //PhotonTransformView transView = playerBody.AddComponent<PhotonTransformView>();
            //transView.m_PositionModel.SynchronizeEnabled = true;
            //transView.m_RotationModel.SynchronizeEnabled = true;

            //PhotonAnimatorView animView = Player.main.playerAnimator.gameObject.AddComponent<PhotonAnimatorView>();
            //SetAnimatorProperties(Player.main.playerAnimator, animView);

            NetPlayerSync positionSync = playerBody.AddComponent<NetPlayerSync>();
            positionSync.animator = playerBody.transform.Find("player_view").GetComponent<Animator>();

            localPlayerView.ObservedComponents = new List<Component>();
            //localPlayerView.ObservedComponents.Add(transView);
            localPlayerView.ObservedComponents.Add(positionSync);
        }

        /// <summary>
        /// Creates the initial prefab to be used when other players join the game
        /// </summary>
        void InitRemotePlayerPrefab ()
        {
            GameObject playerBody = Player.main.transform.Find("body").gameObject;

            // Set the ID to zero before cloning to avoid a duplicate ID being created and throwing an exception (ids of zero are ignored)
            int id = localPlayerView.viewID;
            localPlayerView.viewID = 0;

            // Set the player's head to be visible, clone it then revert it back to create an appropriate remote prefab
            Player.main.head.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            remotePlayerPrefab = Instantiate(playerBody, Vector3.one*10000f, Quaternion.identity);
            
            localPlayerView.viewID = id;

            Player.main.head.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

            // Remove all components that we dont need from the remote player prefab
            remotePlayerPrefab.RemoveNonEssentialComponents(remotePlayerEssentials);

            remotePlayerPrefab.GetComponent<PhotonView>().enabled = false;

            GameObject signalBase = Instantiate(Resources.Load("VFX/xSignal"), remotePlayerPrefab.transform) as GameObject;
            signalBase.name = "signalbase";
            signalBase.transform.localScale = new Vector3(.5f, .5f, .5f);
            signalBase.transform.localPosition += new Vector3(0, 0.8f, 0);

            PingInstance ping = signalBase.GetComponent<PingInstance>();
            ping.pingType = PingType.Sunbeam;
            signalBase.SetActive(false);

            // Now that we have created the initial prefab we can begin to spawn other players
            canCreateRemotePlayerObjects = true;
        }

        void SetAnimatorProperties(Animator anim, PhotonAnimatorView animView)
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                PhotonAnimatorView.ParameterType type = PhotonAnimatorView.ParameterType.Bool;
                switch (param.type)
                {
                    case (AnimatorControllerParameterType.Bool):
                        type = PhotonAnimatorView.ParameterType.Bool;
                        break;
                    case (AnimatorControllerParameterType.Float):
                        type = PhotonAnimatorView.ParameterType.Float;
                        break;
                    case (AnimatorControllerParameterType.Int):
                        type = PhotonAnimatorView.ParameterType.Int;
                        break;
                    case (AnimatorControllerParameterType.Trigger):
                        type = PhotonAnimatorView.ParameterType.Trigger;
                        break;
                }

                animView.SetParameterSynchronized(param.name, type, PhotonAnimatorView.SynchronizeType.Discrete);
            }

            if (anim.layerCount > 0)
            {
                for (int i = 0; i < anim.layerCount; i++)
                {
                    animView.SetLayerSynchronized(i, PhotonAnimatorView.SynchronizeType.Discrete);
                }
            }
        }

        public void Update ()
        {
            // Using a slight delay to prevent this check happening every frame
            if (Time.time > nextCheckTime)
            {
                nextCheckTime = Time.time + playerCheckDelay;

                if (PhotonNetwork.inRoom && canCreateRemotePlayerObjects)
                {
                    // Spawns a remote players prefab if we havent already and syncs its photonViews ID
                    foreach (PhotonPlayer player in PhotonNetwork.otherPlayers)
                    {
                        if (!objectIdsByPlayer.ContainsKey(player))
                        {
                            if (player.CustomProperties["ObjectID"] != null)
                            {
                                objectIdsByPlayer.Add(player, CreateNewPlayerPrefab(player));
                            }
                        }
                    }
                }
            }

            // Return to menu if disconnected
            if(!PhotonNetwork.connected || !PhotonNetwork.inRoom)
            {
                if(IngameMenu.main)
                {
                    IngameMenu.main.QuitGame(false);
                }
            }

            if(Input.GetKeyDown(KeyCode.O))
            {
                optionsPanelOpen = !optionsPanelOpen;
            }
        }

        void LateUpdate ()
        {
            if (optionsPanelOpen)
            { 
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Will create a new remote player object from their custom properties
        /// </summary>
        /// <param name="player">The remote player to create an object for</param>
        /// <returns>The remote player objects PhotonView.viewID, to be stored in the dictionary</returns>
        int CreateNewPlayerPrefab(PhotonPlayer player)
        {
            // Gets the ID allocated by the remote player from the customproperties
            int id = (int)player.CustomProperties["ObjectID"];
            GameObject newPlayer = Instantiate(remotePlayerPrefab);

            PhotonView newView = newPlayer.GetComponent<PhotonView>();
            newView.enabled = true;
            newView.viewID = id;
            newView.TransferOwnership(player);

            GameObject signal = newPlayer.transform.Find("signalbase").gameObject;
            PingInstance ping = signal.GetComponent<PingInstance>();
            ping.SetLabel(player.NickName);
            signal.SetActive(true);

            return id;
        }

        /// <summary>
        /// Creates a room with the locally defined room name, and syncs that name to the lobby when OnJoinedRoom is called
        /// </summary>
        /// <param name="name">The name of the room to create</param>
        public void CreateRoom ()
        {
            if (!string.IsNullOrEmpty(playerNameToUse))
            {
                if (!string.IsNullOrEmpty(currentRoomName))
                {
                    PlayerPrefs.SetString("PlayerNameCache", playerNameToUse);
                    PlayerPrefs.SetString("RoomNameCache", currentRoomName);
                    PhotonNetwork.playerName = playerNameToUse;
                    if (PhotonNetwork.connectedAndReady)
                    {
                        ErrorMessage.AddMessage("Creating room: " + currentRoomName);
                        PhotonNetwork.JoinOrCreateRoom(currentRoomName, new RoomOptions(), PhotonNetwork.lobby);
                        optionsPanelOpen = false;
                    }
                    else
                    {
                        ErrorMessage.AddMessage("Not yet connected to photon!");
                    }
                }
                else
                {
                    ErrorMessage.AddMessage("Please assign a room name via the options window");
                    optionsPanelOpen = true;
                }
            }
            else
            {
                ErrorMessage.AddMessage("Please assign a player name via the options window");
                optionsPanelOpen = true;
            }
        }

        /// <summary>
        /// Much more simple than CreateRoom, as we don't need to do any name syncing due to us not being the master client
        /// </summary>
        /// <param name="name">The name of the room to join</param>
        public void JoinRoom(string name)
        {
            if (!string.IsNullOrEmpty(playerNameToUse))
            {
                PhotonNetwork.playerName = playerNameToUse;
                PlayerPrefs.SetString("PlayerNameCache", playerNameToUse);
                if (PhotonNetwork.connectedAndReady)
                {
                    ErrorMessage.AddMessage("Connecting to room: " + name);
                    PhotonNetwork.JoinRoom(name);
                    optionsPanelOpen = false;
                }
                else
                {
                    ErrorMessage.AddMessage("Not yet connected to photon!");
                }
            }
            else
            {
                ErrorMessage.AddMessage("Please assign a player name via the options window");
                optionsPanelOpen = true;
            }
        }

        [PunRPC]
        public void SpawnNetworkTechType(TechType type, Vector3 position, Quaternion rotation)
        {
            string classId = ClassIDForTechType[type];
            if (PrefabDatabase.TryGetPrefabFilename(classId, out string filename))
            {
                GameObject go = PhotonNetwork.Instantiate(filename, position, rotation, 0);
            }
        }

        void OnGUI()
        {
            GUI.skin = MainPatch.subnauticaSkin;
            GUI.skin.window.font = GUI.skin.font;
            if (optionsPanelOpen)
            {
                windowRect = GUILayout.Window(0, windowRect, MainMenuWindow, "Multiplayer Options");
            }
            else
            {
                optionsPanelOpen = GUILayout.Button("Open Options Menu (O)");
            }
        }

        void MainMenuWindow(int windowID)
        {
            using (new GUILayout.VerticalScope())
            {
                string roomText = PhotonNetwork.inRoom ? "We are in a room!" : "Not connected to a photon room";
                GUILayout.Label(roomText);
                GUILayout.Label("Photon Cloud ping: " + PhotonNetwork.GetPing());

                if (!PhotonNetwork.inRoom)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Player Name: ");
                        playerNameToUse = GUILayout.TextField(playerNameToUse, 60, GUILayout.Width(200));
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Room Name: ");
                        currentRoomName = GUILayout.TextField(currentRoomName, 50, GUILayout.Width(200));
                    }
                }
                else
                {
                    GUILayout.Label("Current players: ");
                    using (var scrollView = new GUILayout.ScrollViewScope(playerListScroll, GUILayout.Width(300), GUILayout.Height(50)))
                    {
                        playerListScroll = scrollView.scrollPosition;
                        foreach(PhotonPlayer player in PhotonNetwork.playerList)
                        {
                            GUILayout.Label(player.NickName);
                        }
                    }
                    if(GUILayout.Button("Disconnect from room"))
                    {
                        PhotonNetwork.LeaveRoom();
                    }
                }

                GUILayout.Space(10);
                optionsPanelOpen = !GUILayout.Button("Close Menu (O)");
            }

            GUI.DragWindow();
        }

        // PHOTON OVERRIDE SECTION

        // Called when we connect to the room
        public override void OnJoinedRoom()
        {
            ErrorMessage.AddMessage("Connected to room! Loading game...");
            Hashtable props = new Hashtable();
            props.Add("HasLoaded", false);
            props.Add("ObjectID", 0);
            StartCoroutine(StartGame());
        }

        // Updates the main menu room list when it is received from the servers
        public override void OnReceivedRoomListUpdate()
        {
            if (!PhotonNetwork.inRoom)
            {
                MainPatch.panel.RefreshRooms();
                ErrorMessage.AddMessage("Received room list from photon lobby!");
            }
        }

        // Join the default lobby when connected in case we havent
        public override void OnConnectedToMaster()
        {
            ErrorMessage.AddMessage("Connected to photon cloud successfully!");
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            ErrorMessage.AddMessage("Connected to default photon lobby successfully!");
        }

        // When the connection to photon fails, assume that there is no network and switch to offline mode
        // This method is bad, as it could be a number of reasons, but for debug purposes we will use offline mode
        public override void OnFailedToConnectToPhoton(DisconnectCause cause)
        {
            ErrorMessage.AddMessage("Failed to connect to photon: " + cause.ToString());
            if (IngameMenu.main)
            {
                IngameMenu.main.QuitGame(false);
            }
        }

        public override void OnDisconnectedFromPhoton()
        {
            if (IngameMenu.main)
            {
                IngameMenu.main.QuitGame(false);
            }
        }

        // Notify players when somebody has started loading into the game
        public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
        {
            ErrorMessage.AddMessage(newPlayer.NickName + " joined the game");
        }

        public override void OnLeftRoom()
        {
            ErrorMessage.AddMessage("Left room, returning to main menu");
            if (IngameMenu.main)
            {
                IngameMenu.main.QuitGame(false);
            }
        }
    }
}
