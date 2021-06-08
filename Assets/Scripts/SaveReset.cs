using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class SaveReset : MonoBehaviour
{
    private int i, j;
    public GameObject[] piecesObjects = new GameObject[32];
    private Vector3[] positions = new Vector3[32];

    void Start()
    {
        GameObject gameObject = new GameObject("LoadStateObject");
    }


    [PunRPC]
    public void PressSave()
    {
        Debug.Log("Save Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("SaveState", RpcTarget.All);

    }

    [PunRPC]
    public void SaveState()
    {
        Debug.Log("Saved...");
        for (i = 0; i < 32; i++)
        {
            positions[i] = piecesObjects[i].transform.position;
        }
    }

    [PunRPC]
    public void PressReset()
    {
        Debug.Log("Reset Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("ResetState", RpcTarget.All);

    }

    [PunRPC]
    public void ResetState()
    {
        Debug.Log("Reset...");
        for (j = 0; j < 32;j ++)
        {
            piecesObjects[j].transform.position = positions[j];
            piecesObjects[j].transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
