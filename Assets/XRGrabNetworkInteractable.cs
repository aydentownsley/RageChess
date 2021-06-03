using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

public class XRGrabNetworkInteractable : XRGrabInteractable
{
    private Vector3 interactorPosition = Vector3.zero;
    private Quaternion interactorRotation = Quaternion.identity;
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

        if (args.interactor is XRBaseInteractor)
        {
            interactorPosition = args.interactor.attachTransform.localPosition;
            interactorRotation = args.interactor.attachTransform.localRotation;

            bool hasAttach = attachTransform != null;
            args.interactor.attachTransform.position = hasAttach ? attachTransform.position : transform.position;
            args.interactor.attachTransform.rotation = hasAttach ? attachTransform.rotation : transform.rotation;
        }
    }
}
