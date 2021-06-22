using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

// Added Bow Networkability
public class XRBowNetworkInteractable : XRGrabInteractable
{
    private Vector3 interactorPosition = Vector3.zero;
    private Quaternion interactorRotation = Quaternion.identity;
    private PhotonView photonView;

    // Gets photon view of bow
    void Start()
    {
        photonView = GetComponent<PhotonView>();
    }

    // overrides default behavior for XRGrabInteractable
    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        // transfers ownership to player grabbing bow
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

// Bow Behavior
public class Bow : XRBowNetworkInteractable
{
    private Notch notch = null;

    protected override void Awake()
    {
        base.Awake();
        notch = GetComponentInChildren<Notch>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // Only notch an arrow if the bow is held
        selectEntered.AddListener(notch.SetReady);
        selectExited.AddListener(notch.SetReady);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        selectEntered.RemoveListener(notch.SetReady);
        selectExited.RemoveListener(notch.SetReady);
    }
}
