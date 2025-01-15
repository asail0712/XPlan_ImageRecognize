using UnityEngine;
using System.Collections;
using com.rfilkov.kinect;

namespace com.rfilkov.components
{
    public class ModelHatController : MonoBehaviour
    {
        [Tooltip("Depth sensor index - 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
        public int playerIndex = 0;

        [Tooltip("Camera used to estimate the overlay positions of 3D-objects over the background.")]
        public Camera foregroundCamera;

        [Tooltip("Vertical offset of the model above the head (in meters).")]
        [Range(0f, 1f)]
        public float verticalOffset = 0.2f;

        [Tooltip("Scale factor for the hat model.")]
        [Range(0.1f, 2f)]
        public float modelScaleFactor = 1f;

        [Tooltip("Smooth factor used for hat-model rotation.")]
        public float smoothFactorRotation = 10f;

        [Tooltip("Smooth factor used for hat-model movement.")]
        public float smoothFactorMovement = 0f;


        private KinectManager kinectManager;
        private Quaternion initialRotation;

        private KinectInterop.JointType headJoint = KinectInterop.JointType.Head;
        private readonly Quaternion zeroRotation = Quaternion.Euler(0, 180, 0);


        void Start()
        {
            initialRotation = transform.rotation;
            transform.localScale = new Vector3(modelScaleFactor, modelScaleFactor, modelScaleFactor);

            if(foregroundCamera == null)
                foregroundCamera = Camera.main;
        }

        void Update()
        {
            // get the KM instance
            if (kinectManager == null)
            {
                kinectManager = KinectManager.Instance;
            }

            // get user-id by user-index
            ulong userId = kinectManager ? kinectManager.GetUserIdByIndex(playerIndex) : 0;

            if (kinectManager && kinectManager.IsInitialized() && userId != 0 && foregroundCamera)
            {

                // get head rotation
                Quaternion newRotation = initialRotation * kinectManager.GetJointOrientation(userId, headJoint, false);
                //Debug.Log($"{headJoint} rotation: {newRotation.eulerAngles}");

                if (newRotation == Quaternion.identity || newRotation == zeroRotation)
                {
                    var parJoint = kinectManager.GetParentJoint(headJoint);  // neck
                    newRotation = initialRotation * kinectManager.GetJointOrientation(userId, parJoint, false);
                    //Debug.Log($"  {parJoint} rotation: {newRotation.eulerAngles}");
                }

                if (smoothFactorRotation != 0f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, smoothFactorRotation * Time.deltaTime);
                else
                    transform.rotation = newRotation;

                // get the background rectangle (use the portrait background, if available)
                Rect backgroundRect = foregroundCamera.pixelRect;
                PortraitBackground portraitBack = PortraitBackground.Instance;

                if (portraitBack && portraitBack.enabled)
                {
                    backgroundRect = portraitBack.GetBackgroundRect();
                }

                // get head overlay position
                Vector3 newPosition = kinectManager.GetJointPosColorOverlay(userId, headJoint, sensorIndex, foregroundCamera, backgroundRect);
                if (newPosition == Vector3.zero)
                {
                    // hide the model behind the camera
                    newPosition.z = -10f;
                }

                if (verticalOffset != 0f)
                {
                    // add the vertical offset
                    Vector3 dirHead = new Vector3(0, verticalOffset, 0);
                    dirHead = transform.InverseTransformDirection(dirHead);
                    newPosition += dirHead;
                }

                // go to the new position
                if (smoothFactorMovement != 0f && transform.position.z >= 0f)
                    transform.position = Vector3.Lerp(transform.position, newPosition, smoothFactorMovement * Time.deltaTime);
                else
                    transform.position = newPosition;

                // scale the model if needed
                if (transform.localScale.x != modelScaleFactor)
                {
                    transform.localScale = new Vector3(modelScaleFactor, modelScaleFactor, modelScaleFactor);
                }
            }
            else
            {
                // hide the model behind the camera
                if (transform.position.z >= 0f)
                {
                    transform.position = new Vector3(0f, 0f, -10f);
                }
            }
        }

    }
}
