using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;

public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
{
    [SerializeField]
    public float x_Start, y_Start, z_Start;
    public float x_Space, z_Space;
    public Transform[] spawnPoint;
    private GameObject spawnedPlayerPrefab;
    public GameObject gridManager;

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        // when the first person joins the room
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            XRRig rig = FindObjectOfType<XRRig>();
            // set this player's position to that of the empty gameObject on the white side
            rig.transform.position = spawnPoint[0].position;
            rig.transform.rotation = spawnPoint[0].rotation;
        }
        // if a second player joins the room
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2) 
        {
            XRRig rig = FindObjectOfType<XRRig>();
            // set this player's position to that of the empty gameObject on the black side
            rig.transform.position = spawnPoint[1].position;
            rig.transform.rotation = spawnPoint[1].rotation;
        }
        // instantiate both network players at their corresponding positions
        spawnedPlayerPrefab = PhotonNetwork.Instantiate("Network Player", transform.position, transform.rotation);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        // destroy network players' prefabs upon leaving the room
        PhotonNetwork.Destroy(spawnedPlayerPrefab);
    }
}
