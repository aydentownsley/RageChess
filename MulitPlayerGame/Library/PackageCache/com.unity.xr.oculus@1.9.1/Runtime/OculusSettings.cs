﻿using System;

using UnityEngine;
using UnityEngine.XR.Management;

namespace Unity.XR.Oculus
{
    [System.Serializable]
    [XRConfigurationData("Oculus", "Unity.XR.Oculus.Settings")]
    public class OculusSettings : ScriptableObject
    {
        public enum StereoRenderingModeDesktop
        {
            /// <summary>
            /// Unity makes two passes across the scene graph, each one entirely indepedent of the other. 
            /// Each pass has its own eye matrices and render target. Unity draws everything twice, which includes setting the graphics state for each pass. 
            /// This is a slow and simple rendering method which doesn't require any special modification to shaders.
            /// </summary>
            MultiPass = 0,
            /// <summary>
            /// Unity uses a single texture array with two elements. Unity converts each call into an instanced draw call. 
            /// Shaders need to be aware of this. Unity's shader macros handle the situation.
            /// </summary>
            SinglePassInstanced = 1,
        }

        public enum StereoRenderingModeAndroid
        {
            /// <summary>
            /// Unity makes two passes across the scene graph, each one entirely indepedent of the other. 
            /// Each pass has its own eye matrices and render target. Unity draws everything twice, which includes setting the graphics state for each pass. 
            /// This is a slow and simple rendering method which doesn't require any special modification to shaders.
            /// </summary>
            MultiPass = 0,
            /// <summary>
            /// Unity uses a single texture array with two elements. 
            /// Multiview is very similar to Single Pass Instanced; however, the graphics driver converts each call into an instanced draw call so it requires less work on Unity's side. 
            /// As with Single Pass Instanced, shaders need to be aware of the Multiview setting. Unity's shader macros handle the situation.
            /// </summary>
            Multiview = 2
        }

        /// <summary>
        /// The current stereo rendering mode selected for desktop-based Oculus platforms
        /// </summary>
        [SerializeField, Tooltip("The current stereo rendering mode selected for desktop-based Oculus platforms.")]
        public StereoRenderingModeDesktop m_StereoRenderingModeDesktop;

        /// <summary>
        /// The current stereo rendering mode selected for Android-based Oculus platforms
        /// </summary>
        [SerializeField, Tooltip("The current stereo rendering mode selected for Android-based Oculus platforms.")]
        public StereoRenderingModeAndroid m_StereoRenderingModeAndroid;

        /// <summary>
        /// Enable or disable support for using a shared depth buffer. This allows Unity and Oculus to use a common depth buffer which enables Oculus to composite the Oculus Dash and other utilities over the Unity application.
        /// </summary>
        [SerializeField, Tooltip("Allows Unity and the Oculus runtime to share a common depth buffer for better scene integration with the Dash.")]
        public bool SharedDepthBuffer = true;

        /// <summary>
        /// Enable or disable Dash support. This inintializes the Oculus Plugin with Dash support which enables the Oculus Dash to composite over the Unity application.
        /// </summary>
        [SerializeField, Tooltip("Initialize the Oculus Plugin with Dash support which allows the Oculus Dash to composite over the Unity application.")]
        public bool DashSupport = true;

        /// <summary>
        /// Enable this if you are building for Quest. This enables application signing with the Android Package (APK) Signature Scheme v2. Disable v2 signing if building for Oculus Go.
        /// This is deprecated and will be removed in Oculus XR Plugin 2.0.0
        /// </summary>
        [SerializeField, Tooltip("Configure Manifest for Oculus Quest. This is deprecated and will be removed in Oculus XR Plugin 2.0.0")]
        public bool V2Signing = true;

        /// <summary>
        /// If enabled, the GLES graphics driver will bypass validation code, potentially running faster.
        /// </summary>
        [SerializeField, Tooltip("If enabled, the GLES graphics driver will bypass validation code, potentially running faster at the expense of detecting and reporting errors. GLES only.")]
        public bool LowOverheadMode = false;
        
        /// <summary>
        /// If enabled, the Oculus SDK will create a protected graphics context. Has a slight overhead; only use if needed for protected content.
        /// This is deprecated and will be removed in Oculus XR Plugin 2.0.0
        /// </summary>
        [SerializeField, Tooltip("If enabled, the Oculus runtime will create a protected graphics context. Has a slight overhead; only use if needed for protected content. This is deprecated and will be removed in Oculus XR Plugin 2.0.0")]
        public bool ProtectedContext = false;

        /// <summary>
        /// If enabled, the application will continue running when system overlays appear.
        /// This is deprecated and will be removed in Oculus XR Plugin 2.0.0
        /// </summary>
        [SerializeField, Tooltip("If enabled, the application will continue running when system overlays appear. This is deprecated and will be removed in Oculus XR Plugin 2.0.0")]
        public bool FocusAware = true;

        /// <summary>
        /// If enabled, the depth buffer and MSAA contents will be discarded rather than resolved. This is an optimization that can possibly break rendering in certain cases.  Vulkan only.
        /// </summary>
        [SerializeField, Tooltip("If enabled, the depth buffer and MSAA contents will be discarded rather than resolved. This is an optimization that can possibly break rendering in certain cases. Vulkan only.")]
        public bool OptimizeBufferDiscards = true;

        /// <summary>
        /// Enables a latency optimization technique which can reduce simulation latency by several ms, depending on application workload.
        /// </summary>
        [SerializeField, Tooltip("Enables a latency optimization technique which can reduce simulation latency by several ms, depending on application workload.")]
        public bool PhaseSync = false;

        /// <summary>
        /// Adds a Quest entry to the supported devices list in the Android manifest.
        /// </summary>
        [SerializeField, Tooltip("Adds a Quest entry to the supported devices list in the Android manifest.")]
        public bool TargetQuest = true;

        /// <summary>
        /// Adds a Quest 2 entry to the supported devices list in the Android manifest.
        /// </summary>
        [SerializeField, Tooltip("Adds a Quest 2 entry to the supported devices list in the Android manifest.")]
        public bool TargetQuest2 = true;


        public ushort GetStereoRenderingMode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return (ushort)m_StereoRenderingModeAndroid;
# else
            return (ushort)m_StereoRenderingModeDesktop;
#endif
        }
#if !UNITY_EDITOR
		public static OculusSettings s_Settings;

		public void Awake()
		{
			s_Settings = this;
		}
#endif
    }
}
