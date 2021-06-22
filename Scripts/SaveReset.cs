using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class SaveReset : MonoBehaviourPunCallbacks
{
    private int i, j;
    public GameObject[] piecesObjects = new GameObject[33];
    private Vector3[] positions = new Vector3[33];

    // Runs on start of scene
    void Start()
    {
        GameObject gameObject = new GameObject("LoadStateObject");
    }

    // RPC - that saves board state when players joins room
    [PunRPC]
    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == 0 ||
            PhotonNetwork.CurrentRoom.PlayerCount == 1 ||
            PhotonNetwork.CurrentRoom.PlayerCount == 2)
        {
            Debug.Log("Initial Save Made");
            gameObject.AddComponent<PhotonView>();
            gameObject.GetComponent<PhotonView>().RPC("SaveState", RpcTarget.All);
        }

    }

    // RPC -  saves board state on Save Button UI press
    [PunRPC]
    public void PressSave()
    {
        Debug.Log("Save Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("SaveState", RpcTarget.All);

    }

    // RPC - called by PressSave
    [PunRPC]
    public void SaveState()
    {
        Debug.Log("Saved...");
        for (i = 0; i < 33; i++)
        {
            positions[i] = piecesObjects[i].transform.position;
        }
    }

    // RPC - resets board state to last save on Reset
    // Button UI press
    [PunRPC]
    public void PressReset()
    {
        Debug.Log("Reset Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("ResetState", RpcTarget.All);

    }

    // RPC - called by PressReset
    [PunRPC]
    public void ResetState()
    {
        Debug.Log("Reset...");
        for (j = 0; j < 33;j ++)
        {
            piecesObjects[j].transform.position = positions[j];
            piecesObjects[j].transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
