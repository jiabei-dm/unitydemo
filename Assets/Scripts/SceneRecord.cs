// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
//
// Copyright (c) 2019 Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Creator Agreement, located
// here: https://id.magicleap.com/creator-terms
//
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Threading;
using System.IO;
using System.Xml;

[RequireComponent(typeof(PrivilegeRequester))]
public class SceneRecord : MonoBehaviour
{
    [Serializable]
    public class Pose
    {
        public Vector3 translation;
        public Quaternion rotation;
    }

    [Serializable]
    public class SerializableCameraIntrinsics
    {
        public uint Width;
        public uint Height;
        public Vector2 FocalLength;
        public Vector2 PrincipalPoint;
        public float FOV;
        public double[] Distortion;
    }

    [Serializable]
    public class ImageInfo
    {
        public string fileName;
        public double timestampSec;
        public string poseStatus;
        public Pose framePose;
        public Matrix4x4 poseMatrix;
    }
    
    [Serializable]
    public class Results
    {
        public SerializableCameraIntrinsics cameraIntrinsics;
        public List<ImageInfo> images = new List<ImageInfo>();
    }

    [Serializable]
    private class ImageCaptureEvent : UnityEvent<Texture2D>
    { }

    #region Private Variables
    [SerializeField, Space, Tooltip("ControllerConnectionHandler reference.")]
    private ControllerConnectionHandler _controllerConnectionHandler = null;

    [SerializeField, Space]
    private ImageCaptureEvent OnImageReceivedEvent = null;

    [SerializeField]
    private Text _text = null;

    private bool _isCameraConnected = false;
    private bool _isCapturing = false;
    private bool _hasStarted = false;
    private bool _doPrivPopup = false;
    private bool _hasShownPrivPopup = false;
    private Thread _captureThread = null;

    private bool _autoCapture = false;
    private Results _results = new Results();

    /// <summary>
    /// The example is using threads on the call to MLCamera.CaptureRawImageAsync to alleviate the blocking
    /// call at the beginning of CaptureRawImageAsync, and the safest way to prevent race conditions here is to
    /// lock our access into the MLCamera class, so that we don't accidentally shut down the camera
    /// while the thread is attempting to work
    /// </summary>
    private object _cameraLockObject = new object();

    private PrivilegeRequester _privilegeRequester = null;
    #endregion

    #region Unity Methods

    /// <summary>
    /// Using Awake so that Privileges is set before PrivilegeRequester Start.
    /// </summary>
    void Awake()
    {
        GenerateJsonReport();
        if (_controllerConnectionHandler == null)
        {
            Debug.LogError("Error: ImageCaptureExample._controllerConnectionHandler is not set, disabling script.");
            enabled = false;
            return;
        }

        // If not listed here, the PrivilegeRequester assumes the request for
        // the privileges needed, CameraCapture in this case, are in the editor.
        _privilegeRequester = GetComponent<PrivilegeRequester>();

        // Before enabling the Camera, the scene must wait until the privilege has been granted.
        _privilegeRequester.OnPrivilegesDone += HandlePrivilegesDone;
    }

    /// <summary>
    /// Stop the camera, unregister callbacks, and stop input and privileges APIs.
    /// </summary>
    void OnDisable()
    {
        MLInput.OnControllerButtonDown -= OnButtonDown;
        lock (_cameraLockObject)
        {
            if (_isCameraConnected)
            {
                MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
                MLCamera.OnCaptureCompleted -= OnCaptureCompleted;
                _isCapturing = false;
                DisableMLCamera();
            }
        }
    }

    /// <summary>
    /// Cannot make the assumption that a reality privilege is still granted after
    /// returning from pause. Return the application to the state where it
    /// requests privileges needed and clear out the list of already granted
    /// privileges. Also, disable the camera and unregister callbacks.
    /// </summary>
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            lock (_cameraLockObject)
            {
                if (_isCameraConnected)
                {
                    MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
                    MLCamera.OnCaptureCompleted -= OnCaptureCompleted;
                    _isCapturing = false;
                    DisableMLCamera();
                }
            }

            MLInput.OnControllerButtonDown -= OnButtonDown;

            _hasStarted = false;
        }
    }

    void OnDestroy()
    {
        if (_privilegeRequester != null)
        {
            _privilegeRequester.OnPrivilegesDone -= HandlePrivilegesDone;
        }
    }

    private void Update()
    {
        if (_doPrivPopup && !_hasShownPrivPopup)
        {
            Instantiate(Resources.Load("PrivilegeDeniedError"));
            _doPrivPopup = false;
            _hasShownPrivPopup = true;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Captures a still image using the device's camera and returns
    /// the data path where it is saved.
    /// </summary>
    /// <param name="fileName">The name of the file to be saved to.</param>
    public void TriggerAsyncCapture()
    {
        if (!_isCapturing && (_captureThread == null || (!_captureThread.IsAlive)))
        {
            ThreadStart captureThreadStart = new ThreadStart(CaptureThreadWorker);
            _captureThread = new Thread(captureThreadStart);
            _captureThread.Start();
        }
        else
        {
            Debug.Log("Previous thread has not finished, unable to begin a new capture just yet.");
        }
    }
    #endregion

    #region Private Functions
    /// <summary>
    /// Connects the MLCamera component and instantiates a new instance
    /// if it was never created.
    /// </summary>
    private void EnableMLCamera()
    {
        lock (_cameraLockObject)
        {
            MLResult result = MLCamera.Start();
            if (result.IsOk)
            {
                result = MLCamera.Connect();
                _isCameraConnected = true;
            }
            else
            {
                if (result.Code == MLResultCode.PrivilegeDenied)
                {
                    Instantiate(Resources.Load("PrivilegeDeniedError"));
                }

                Debug.LogErrorFormat("Error: ImageCaptureExample failed starting MLCamera, disabling script. Reason: {0}", result);
                enabled = false;
                return;
            }
        }
    }

    /// <summary>
    /// Disconnects the MLCamera if it was ever created or connected.
    /// </summary>
    private void DisableMLCamera()
    {
        lock (_cameraLockObject)
        {
            if (MLCamera.IsStarted)
            {
                MLCamera.Disconnect();
                // Explicitly set to false here as the disconnect was attempted.
                _isCameraConnected = false;
                MLCamera.Stop();
            }
        }
    }

    /// <summary>
    /// Once privileges have been granted, enable the camera and callbacks.
    /// </summary>
    private void StartCapture()
    {
        if (!_hasStarted)
        {
            lock (_cameraLockObject)
            {
                EnableMLCamera();
                MLCVCameraIntrinsicCalibrationParameters cameraIntrinsics;
                MLCamera.GetIntrinsicCalibrationParameters(out cameraIntrinsics);

                Debug.Log("Camera is connected:" + cameraIntrinsics.FOV);
                _results.cameraIntrinsics = new SerializableCameraIntrinsics
                {
                    Distortion = cameraIntrinsics.Distortion,
                    FocalLength = cameraIntrinsics.FocalLength,
                    FOV = cameraIntrinsics.FOV,
                    Height = cameraIntrinsics.Height,
                    PrincipalPoint = cameraIntrinsics.PrincipalPoint,
                    Width = cameraIntrinsics.Width
                };

                MLCamera.OnRawImageAvailable += OnCaptureRawImageComplete;
                MLCamera.OnCaptureCompleted += OnCaptureCompleted;
            }
            MLInput.OnControllerButtonDown += OnButtonDown;

            _hasStarted = true;
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Responds to privilege requester result.
    /// </summary>
    /// <param name="result"/>
    private void HandlePrivilegesDone(MLResult result)
    {
        if (!result.IsOk)
        {
            if (result.Code == MLResultCode.PrivilegeDenied)
            {
                Instantiate(Resources.Load("PrivilegeDeniedError"));
            }

            Debug.LogErrorFormat("Error: ImageCaptureExample failed to get requested privileges, disabling script. Reason: {0}", result);
            enabled = false;
            return;
        }

        Debug.Log("Succeeded in requesting all privileges");
        StartCapture();
    }

    /// <summary>
    /// Handles the event for button down.
    /// </summary>
    /// <param name="controllerId">The id of the controller.</param>
    /// <param name="button">The button that is being pressed.</param>
    private void OnButtonDown(byte controllerId, MLInputControllerButton button)
    {
        if (_controllerConnectionHandler.IsControllerValid(controllerId) && MLInputControllerButton.Bumper == button)
        {
            if (_autoCapture)
            {
                _autoCapture = false;
                if (!_isCapturing)
                {
                    GenerateJsonReport();
                }
            } else
            {
                _autoCapture = true;
                TriggerAsyncCapture();
            }
        }
    }

    private void GenerateJsonReport()
    {
        String json = JsonUtility.ToJson(_results);
        Debug.Log("JSON is:" + json);
        System.IO.File.WriteAllText(@"/documents/C1/results.json", json);
    }

    private void OnCaptureCompleted(MLCameraResultExtras extras, string extraString)
    {
        Matrix4x4 matrix4X4 = new Matrix4x4();
        MLResult poseResult = MLCamera.GetFramePose(extras.VcamTimestampUs * 1000, out matrix4X4);
        Pose pose = new Pose
        {
            rotation = matrix4X4.rotation,
            translation = new Vector3(matrix4X4.m03, matrix4X4.m13, matrix4X4.m23)
        };

        String info = String.Format("OnCaptureCompleted.\n Frame number: {0}, Frame time: {1}\nExtra String: {2}", extras.FrameNumber, extras.VcamTimestampUs, extraString);
        Debug.Log(info);
        Debug.Log(String.Format("========\n{4}\n{3}\n{0}\nrotation:{1}\ntranslation:{2}\n===========\n", matrix4X4, pose.rotation.ToString("f4"), pose.translation.ToString("f4"), poseResult, info));
        _text.text = String.Format("Frame#{0}, FrameTime: {1}\nRotation:{2}\nTranslation:{3}", extras.FrameNumber, extras.VcamTimestampUs / 1000, pose.rotation.ToString("f4"), pose.translation.ToString("f4"));
        _results.images.Add(new ImageInfo
        {
            fileName = String.Format(@"image{0}.jpeg", _results.images.Count),
            timestampSec = ((double) extras.VcamTimestampUs) / 1000000.0d,
            poseStatus = poseResult.ToString(),
            framePose = pose,
            poseMatrix = matrix4X4
        });

        lock (_cameraLockObject)
        {
            _isCapturing = false;
        }

        if (_autoCapture)
        {
            TriggerAsyncCapture();
        } else
        {
            GenerateJsonReport();
        }
    }

    /// <summary>
    /// Handles the event of a new image getting captured.
    /// </summary>
    /// <param name="imageData">The raw data of the image.</param>
    private void OnCaptureRawImageComplete(byte[] imageData)
    {
        Debug.Log("Image rawbytes (" + imageData.Length + ") available and writting to file " + GetFileName() + "...");

        // Initialize to 8x8 texture so there is no discrepency
        // between uninitalized captures and error texture
        Texture2D texture = new Texture2D(8, 8);
        bool status = texture.LoadImage(imageData);

        if (status && (texture.width != 8 && texture.height != 8))
        {
            OnImageReceivedEvent.Invoke(texture);
        }

        System.IO.File.WriteAllBytes(@GetFileName(), imageData);
    }

    /// <summary>
    /// Worker function to call the API's Capture function
    /// </summary>
    private void CaptureThreadWorker()
    {
        lock (_cameraLockObject)
        {
            if (MLCamera.IsStarted && _isCameraConnected)
            {
                Debug.Log("===============Start Capturing");
                MLResult result = MLCamera.CaptureRawImageAsync();
                Debug.Log("===============Captured result:" + result.Code);

                if (result.IsOk)
                {
                    _isCapturing = true;
                }
                else if (result.Code == MLResultCode.PrivilegeDenied)
                {
                    _doPrivPopup = true;
                }
            }
        }
    }
    #endregion

    private String GetFileName()
    {
        return String.Format(@"/documents/C1/image{0}.jpeg", _results.images.Count);
    }

}
