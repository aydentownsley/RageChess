using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

// Creates view in Unity to manipulate these variables
[System.Serializable]
public class DefaultRoom
{
    public string Name;
    public int sceneIndex;
    public int maxPlayer;
}

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public List<DefaultRoom> defaultRooms;
    public GameObject roomUI;

    // Connects player to server
    public void ConnectToServer()
    {
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("Trying to Connect to Server...");
    }

    // Moves player in to Photon Lobby
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Server!");
        base.OnConnectedToMaster();
        PhotonNetwork.JoinLobby();
    }

    // Sets roomUI to active when player joins lobby
    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        Debug.Log("We Joined the Lobby!");
        roomUI.SetActive(true);
    }

    // Sets up paramaters of rooms for players which
    // can be set in Unity view
    public void InitializeRoom(int defaultRoomIndex)
    {
        DefaultRoom roomSettings = defaultRooms[defaultRoomIndex];

        // Loads scene
        PhotonNetwork.LoadLevel(roomSettings.sceneIndex);

        // Create the room
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = (byte)roomSettings.maxPlayer;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        PhotonNetwork.JoinOrCreateRoom(roomSettings.Name, roomOptions, TypedLobby.Default);
    }

    // Lets player join created room when clicking Button UI
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room!");
        base.OnJoinedRoom();
    }

    // Runs setup for player when the join room
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log("Player Joined Room!");
        base.OnPlayerEnteredRoom(newPlayer);
    }

}
