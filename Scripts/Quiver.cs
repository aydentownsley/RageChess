using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// adds networkability to the quiver so that the arrow is given to the player
public class XRQuiver : XRBaseInteractable
{
    private PhotonView photonView;

    // Start is called before the first frame update
    void Start()
    {
        // gets photon view of arrow
        photonView = GetComponent<PhotonView>();
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        // assigns owner to player
        photonView.RequestOwnership();
        base.OnSelectEntering(args);
    }
}

// quiver behavior
public class Quiver : XRQuiver
{
    public GameObject arrowPrefab = null;

    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener (CreateAndSelectArrow);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        selectEntered.RemoveListener (CreateAndSelectArrow);
    }

    private void CreateAndSelectArrow(SelectEnterEventArgs args)
    {
        // Create arrow, force into interacting hand
        Arrow arrow = CreateArrow(args.interactor.transform);
        interactionManager.ForceSelect(args.interactor, arrow);
    }

    private Arrow CreateArrow(Transform orientation)
    {
        // Create arrow, and get arrow component
        GameObject arrowObject =
            PhotonNetwork
                .Instantiate("Arrow",
                orientation.position,
                orientation.rotation);
        StartCoroutine(Despawn(60, arrowObject));
        return arrowObject.GetComponent<Arrow>();
    }

    IEnumerator Despawn(float time, GameObject go)
    {
        yield return new WaitForSeconds(time);

        // Code to execute after the delay
        PhotonNetwork.Destroy (go);
    }
}
