using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;

public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
{
    private bool first = true;

    [SerializeField]
    public float x_Start, y_Start, z_Start;
    public float x_Space, z_Space;
    public Transform[] spawnPoint;
    private GameObject spawnedPlayerPrefab;
    public GameObject gridManager;

    public override void OnJoinedRoom()
    {
   
        base.OnJoinedRoom();
        // gridManager.SetActive(true);

        // if (first == true)
        // {
        //     for (int i = 0; i < 4; i++)
        //     {
        //         PhotonNetwork.Instantiate("Socket", new Vector3(x_Start + x_Space * (i % 8), y_Start + 0, z_Start + z_Space * (i / 8)), Quaternion.identity);
        //     }
        //     first = false;
        // }

        if (PhotonNetwork.CurrentRoom.PlayerCount == 1) 
        {
            XRRig rig = FindObjectOfType<XRRig>();
            rig.transform.position = spawnPoint[0].position;
            rig.transform.rotation = spawnPoint[0].rotation;
        }
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2) 
        {
            XRRig rig = FindObjectOfType<XRRig>();
            rig.transform.position = spawnPoint[1].position;
            rig.transform.rotation = spawnPoint[1].rotation;
        }
        spawnedPlayerPrefab = PhotonNetwork.Instantiate("Network Player", transform.position, transform.rotation);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        PhotonNetwork.Destroy(spawnedPlayerPrefab);
    }

}
