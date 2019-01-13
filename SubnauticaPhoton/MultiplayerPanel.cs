using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Harmony;

namespace SubnauticaPhoton
{
    public class MultiplayerPanel : MonoBehaviour
    {
        List<RoomInfo> rooms;
        List<GameObject> roomButtons = new List<GameObject>();
        public GameObject savedGamesPanel;
        public GameObject multiplayerPanel;

        GameObject multiplayerButton;
        Transform savedGameAreaContent;

        void Awake()
        {
            multiplayerButton = savedGamesPanel.transform.Find("Scroll View/Viewport/SavedGameAreaContent/NewGame").gameObject;
            savedGameAreaContent = multiplayerPanel.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            RefreshRooms();
            CreateRoomButton();
        }

        public void RefreshRooms()
        {
            if (PhotonNetwork.insideLobby)
            {
                rooms = PhotonNetwork.GetRoomList().ToList();

                foreach (GameObject butn in roomButtons)
                {
                    Destroy(butn);
                }

                roomButtons = new List<GameObject>();

                foreach (RoomInfo room in rooms)
                {
                    NewJoinRoomButton(room);
                }
            }
        }

        /// <summary>
        /// Used to create UI for connecting to individual rooms sent by the photon lobby
        /// </summary>
        /// <param name="room">The room info to create a button for</param>
        void NewJoinRoomButton(RoomInfo room)
        {
            GameObject multiplayerButtonInst = Instantiate(multiplayerButton);
            multiplayerButtonInst.name = (savedGameAreaContent.childCount - 1).ToString();
            Transform txt = multiplayerButtonInst.transform.Find("NewGameButton/Text");
            txt.GetComponent<Text>().text = room.Name;
            DestroyObject(txt.GetComponent<LanguageUpdater>());
            Button multiplayerButtonButton = multiplayerButtonInst.transform.Find("NewGameButton").GetComponent<Button>();
            multiplayerButtonButton.onClick = new Button.ButtonClickedEvent();
            multiplayerButtonButton.onClick.AddListener(() => MainPatch.handler.JoinRoom(room.Name));
            multiplayerButtonInst.transform.SetParent(savedGameAreaContent, false);
            roomButtons.Add(multiplayerButtonInst);
        }

        /// <summary>
        /// Should only be called once in the menu, as we only need one button to create a room
        /// </summary>
        public void CreateRoomButton()
        {
            // TODO: Add an intermediate GUI panel to allow the user to select a username and room name
            GameObject multiplayerButtonInst = Instantiate(multiplayerButton);
            Transform txt = multiplayerButtonInst.transform.Find("NewGameButton/Text");
            txt.GetComponent<Text>().text = "Create A Room";
            DestroyObject(txt.GetComponent<LanguageUpdater>());
            Button multiplayerButtonButton = multiplayerButtonInst.transform.Find("NewGameButton").GetComponent<Button>();
            multiplayerButtonButton.onClick = new Button.ButtonClickedEvent();
            multiplayerButtonButton.onClick.AddListener(() => MainPatch.handler.CreateRoom());
            multiplayerButtonInst.transform.SetParent(savedGameAreaContent, false);
        }

        /// <summary>
        /// Enables the right side panel for connecting to/creating rooms
        /// </summary>
        public void ShowMPPanel()
        {
            MainMenuRightSide.main.OpenGroup("Multiplayer");
            RefreshRooms();
        }
    }
}
