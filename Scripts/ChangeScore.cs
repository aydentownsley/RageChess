using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class ChangeScore : MonoBehaviour
{
    public GameObject Score2;
    public Image scoreImage2;
    public int currScore2 = 0;
    public Dictionary<int, Sprite> ScoreDict2 = new Dictionary<int, Sprite>();

    // Creates dictionary of score images on scene start
    void Start()
    {
        ScoreDict2.Add(0, Resources.Load<Sprite>("0"));
        ScoreDict2.Add(1, Resources.Load<Sprite>("1"));
        ScoreDict2.Add(2, Resources.Load<Sprite>("2"));
        ScoreDict2.Add(3, Resources.Load<Sprite>("3"));
        ScoreDict2.Add(4, Resources.Load<Sprite>("4"));
        ScoreDict2.Add(5, Resources.Load<Sprite>("5"));
        ScoreDict2.Add(6, Resources.Load<Sprite>("6"));
        ScoreDict2.Add(7, Resources.Load<Sprite>("7"));
        ScoreDict2.Add(8, Resources.Load<Sprite>("8"));
        ScoreDict2.Add(9, Resources.Load<Sprite>("9"));
        ScoreDict2.Add(10, Resources.Load<Sprite>("10"));
        ScoreDict2.Add(11, Resources.Load<Sprite>("Win"));
        GameObject gameObject = new GameObject("ScoreObject");
    }

    // RPC - Calls AddPointOther on (+) Button UI press
    [PunRPC]
    public void PressAdd()
    {
        Debug.Log("Add Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("AddPointOther", RpcTarget.All);
    }

    // RPC - Calls SubPointOther on (-) Button UI press
    [PunRPC]
    public void PressSub()
    {
        Debug.Log("Sub Button Pushed");
        gameObject.AddComponent<PhotonView>();
        gameObject.GetComponent<PhotonView>().RPC("SubPointOther", RpcTarget.All);
    }

    // RPC - Changes Canvas image using dictionary of images
    // images correspond to in game points
    // adds 1 to currScore2 each time method called
    // ex: image of 1 for 1 point
    [PunRPC]
    public void AddPointOther()
    {
        Debug.Log("Point Add button pressed...");

        if (currScore2 < 11 && currScore2 >= 0)
        {
            currScore2 = currScore2 + 1;
            scoreImage2.sprite = ScoreDict2[currScore2];
        }
    }

    // RPC - Changes Canvas image using dictionary of images
    // images correspond to in game points
    // subtracts 1 from currScore2 each time method called
    // ex: image of 1 for 1 point
    [PunRPC]
    public void SubPointOther()
    {
        Debug.Log("Point Sub button pressed...");

        if (currScore2 < 11 && currScore2 > 0)
        {
            currScore2 = currScore2 - 1;
            scoreImage2.sprite = ScoreDict2[currScore2];
        }
    }
}
