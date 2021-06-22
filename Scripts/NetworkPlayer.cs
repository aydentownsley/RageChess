using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;

public class NetworkPlayer : MonoBehaviour
{

    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    public Animator leftHandAnimator;
    public Animator rightHandAnimator;

    private PhotonView photonView;

    private Transform headRig;
    private Transform leftHandRig;
    private Transform rightHandRig;

    // Start() is called before the first frame update
    void Start()
    {
        photonView = GetComponent<PhotonView>();
        XRRig rig = FindObjectOfType<XRRig>();
        // set Main Camera to be the view from the XR head rig
        headRig = rig.transform.Find("Camera Offset/Main Camera");
        // set right and left hand rigs to map the right and left hand controllers
        rightHandRig = rig.transform.Find("Camera Offset/RightHand Controller");
        leftHandRig = rig.transform.Find("Camera Offset/LeftHand Controller");

        // photonView.IsMine will be true if the instance is controlled by the 'client' application,
        // meaning this instance represents the physical person playing on this computer within this
        // application. So if it is false, we don't want to do anything and solely rely on the PhotonView
        // component to synchronize the transform and animator components we've setup earlier. 
        if (photonView.IsMine)
        {
            // loop through an array of all the components in children objects
            foreach (var item in GetComponentsInChildren<Renderer>())
            {
                // disable each of these components so that the views of one's own head and hands, etc
                // are not duplicated
                item.enabled = false;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (photonView.IsMine)
        {
            // tracks movements of head and hand rigs every frame
            MapPosition(head, headRig);
            MapPosition(leftHand, leftHandRig);
            MapPosition(rightHand, rightHandRig);

            // updates animations of both hands every frame
            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), leftHandAnimator);
            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), rightHandAnimator);
        }
    }

    // set both the trigger and grip buttons to perform the proper animations
    void UpdateHandAnimation(InputDevice targetDevice, Animator handAnimator)
    {
        if(targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            handAnimator.SetFloat("Trigger", triggerValue);
        }
        else
        {
            handAnimator.SetFloat("Trigger", 0);
        }

        if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            handAnimator.SetFloat("Grip", gripValue);
        }
        else
        {
            handAnimator.SetFloat("Grip", 0);
        }
    }

    // track position and rotation of rig across network
    void MapPosition(Transform target, Transform rigTransform)
    {
        target.position = rigTransform.position;
        target.rotation = rigTransform.rotation;
    }
}
