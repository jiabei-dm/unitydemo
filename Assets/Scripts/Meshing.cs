using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

public class Meshing : MonoBehaviour
{
    [SerializeField, Tooltip("The spatial mapper from which to update mesh params.")]
    private MLSpatialMapper mlSpatialMapper = null;

    [SerializeField, Tooltip("Visualizer for the meshing results.")]
    private MeshingVisualizer meshingVisualizer = null;

    [SerializeField, Space, Tooltip("ControllerConnectionHandler reference.")]
    private ControllerConnectionHandler controllerConnectionHandler = null;

    private Camera mainCamera = null;

    private MeshingVisualizer.RenderMode renderMode = MeshingVisualizer.RenderMode.Wireframe;
    private int renderModeCount = System.Enum.GetNames(typeof(MeshingVisualizer.RenderMode)).Length;

    private void Awake()
    {
        if (mlSpatialMapper == null)
        {
            Debug.LogError("Error: Meshing._mlSpatialMapper is not set, disabling script.");
            enabled = false;
            return;
        }
        if (meshingVisualizer == null)
        {
            Debug.LogError("Error: Meshing._meshingVisualizer is not set, disabling script.");
            enabled = false;
            return;
        }
        if (controllerConnectionHandler == null)
        {
            Debug.LogError("Error Meshing._controllerConnectionHandler not set, disabling script.");
            enabled = false;
            return;
        }
        mainCamera = Camera.main;
        MagicLeapDevice.RegisterOnHeadTrackingMapEvent(OnHeadTrackingMapEvent);
        MLInput.OnControllerTouchpadGestureStart += OnTouchpadGestureStart;

    }

    // Start is called before the first frame update
    void Start()
    {
        meshingVisualizer.SetRenderers(renderMode);

        mlSpatialMapper.gameObject.transform.position = mainCamera.gameObject.transform.position;
        mlSpatialMapper.gameObject.transform.localScale = Vector3.one * 10;
    }

    // Update is called once per frame
    void Update()
    {
        mlSpatialMapper.gameObject.transform.position = mainCamera.gameObject.transform.position;
    }

    private void OnDestroy()
    {
        MagicLeapDevice.UnregisterOnHeadTrackingMapEvent(OnHeadTrackingMapEvent);
        MLInput.OnControllerTouchpadGestureStart -= OnTouchpadGestureStart;
    }

    private void OnHeadTrackingMapEvent(MLHeadTrackingMapEvent mapEvents)
    {
        if (mapEvents.IsLost())
        {
            mlSpatialMapper.DestroyAllMeshes();
            mlSpatialMapper.RefreshAllMeshes();
        }
    }

    private void OnTouchpadGestureStart(byte controllerId, MLInputControllerTouchpadGesture gesture)
    {
        if (controllerConnectionHandler.IsControllerValid(controllerId) 
            && gesture.Type == MLInputControllerTouchpadGestureType.Swipe 
            && gesture.Direction == MLInputControllerTouchpadGestureDirection.Up)
        {
            renderMode = (MeshingVisualizer.RenderMode) ((int)(renderMode + 1) % renderModeCount);
            meshingVisualizer.SetRenderers(renderMode);
        }
    }
}

