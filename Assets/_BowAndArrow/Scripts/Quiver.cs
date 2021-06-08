using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

public class OtherXRGrabNetworkInteractable : XRBaseInteractable
{
    // private Vector3 interactorPosition = Vector3.zero;
    // private Quaternion interactorRotation = Quaternion.identity;
    private PhotonView photonView;

    // Start is called before the first frame update
    void Start()
    {
        photonView = GetComponent<PhotonView>();

    }

    // Update is called once per frame
    void Update()
    {

    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        photonView.RequestOwnership();
        base.OnSelectEntering(args);

        // if (args.interactor is XRBaseInteractor)
        // {
        //     interactorPosition = args.interactor.attachTransform.localPosition;
        //     interactorRotation = args.interactor.attachTransform.localRotation;

        //     bool hasAttach = attachTransform != null;
        //     args.interactor.attachTransform.position = hasAttach ? attachTransform.position : transform.position;
        //     args.interactor.attachTransform.rotation = hasAttach ? attachTransform.rotation : transform.rotation;
        // }
    }
}

public class Quiver : OtherXRGrabNetworkInteractable
{
    public GameObject arrowPrefab = null;

    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(CreateAndSelectArrow);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        selectEntered.RemoveListener(CreateAndSelectArrow);
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
        GameObject arrowObject = PhotonNetwork.Instantiate("Arrow", orientation.position, orientation.rotation);
        return arrowObject.GetComponent<Arrow>();
    }
}
