using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.MagicLeap;


public class WaypointsVisualizer : MonoBehaviour
{
    [Space, SerializeField, Tooltip("ControllerConnectionHandler reference.")]
    private ControllerConnectionHandler ControllerConnectionHandler = null;

    [SerializeField]
    private GameObject WaypointObjPrefab = null;

    [SerializeField]
    private GameObject JohnLemon = null;

    [SerializeField, Tooltip("PlayerMovement.")]
    private PlayerMovement PlayerMovement = null;

    [SerializeField]
    private PersistentCoordinates persistentCoordinates;

    private WaypointsList WaypointsList = null;

    private Transform pcfTransform;

    bool IsAdded = false;

    // Start is called before the first frame update
    void Start()
    {
        persistentCoordinates.OnPersistentCoordinatesInitialized += OnPersistentCoordinatesInitialized;
        string json = null;
        try {
            json = System.IO.File.ReadAllText(@"/documents/C1/waypoints.json");
            if (json.Length != 0)
            {
                WaypointsList = JsonUtility.FromJson<WaypointsList>(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        Debug.Log("Load JSON:" + json);
    }


    void OnPersistentCoordinatesInitialized(Transform transform, string uid)
    {
        Debug.Log("Loaded PCF:" + uid);
        if (WaypointsList == null)
        {
            WaypointsList = new WaypointsList(transform);
            WaypointsList.pcfUid = uid;
        } else if (uid == WaypointsList.pcfUid)
        {
            WaypointsList.SetTransform(transform);
            for (int i = 0; i < WaypointsList.Waypoints.Count; i++)
            {
                Pose pose = WaypointsList.GetWayPoint(i);
                Instantiate(WaypointObjPrefab, pose.position, pose.rotation);
            }
        }

        if (!IsAdded) {
            MLInput.OnControllerButtonDown += OnButtonDown;
            IsAdded = true;
        }
    }

    void OnDestroy()
    {
        MLInput.OnControllerButtonDown -= OnButtonDown;
    }


    private void OnButtonDown(byte controllerId, MLInputControllerButton button)
    {
        if (ControllerConnectionHandler.IsControllerValid(controllerId))
        {
            if (button == MLInputControllerButton.Bumper)
            {
                if (JohnLemon != null)
                {
                    WaypointsList.AddWaypoints(JohnLemon.transform);
                    // Quaternion rotation = Quaternion.Euler(0, raycast.transform.rotation.eulerAngles.y, 0);
                    //Matrix4x4 worldTransform = pcfTransform.localToWorldMatrix * WaypointsList.Waypoints[WaypointsList.Waypoints.Count - 1];

                    Pose pose = WaypointsList.GetWayPoint(WaypointsList.Waypoints.Count - 1);
                    GameObject gameObj = Instantiate(WaypointObjPrefab, pose.position, pose.rotation);
                }
            } else if (button == MLInputControllerButton.HomeTap)
            {
                PlayerMovement.PlayBack(WaypointsList);
                string json = JsonUtility.ToJson(WaypointsList);
                Debug.Log("Write JSON:" + json);
                System.IO.File.WriteAllText(@"/documents/C1/waypoints.json", json);
            }
        }
    }

}
