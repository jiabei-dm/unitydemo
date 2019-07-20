using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

public class PersistentCoordinates : MonoBehaviour
{

    [SerializeField]
    private PrivilegeRequester PrivilegeRequester = null;

    [SerializeField]
    private GameObject PersistentCoordinatesPrefab = null;

    private List<MLPersistentBehavior> PointBehaviors = new List<MLPersistentBehavior>();

    public event Action<Transform, String> OnPersistentCoordinatesInitialized;

    // Start is called before the first frame update
    void Start()
    {
        if (PrivilegeRequester == null)
        {
            Debug.LogError("Error: PersistentCoordinates.PrivilegeRequester is not set, disabling script.");
            enabled = false;
            return;

        }
        PrivilegeRequester.OnPrivilegesDone += HandlePrivilegesDone;
    }

    void OnDestroy()
    {
        MLPersistentStore.Stop();
        MLPersistentCoordinateFrames.Stop();
    }

    void HandlePrivilegesDone(MLResult result)
    {
        PrivilegeRequester.OnPrivilegesDone -= HandlePrivilegesDone;
        if (!result.IsOk)
        {
            Debug.LogErrorFormat("Error: PersistentCoordinates failed to get requested privileges, " +
                "disabling script. Reason: {0}", result);
            enabled = false;
            return;
        }

        result = MLPersistentStore.Start();
        if (!result.IsOk)
        {
            Debug.LogErrorFormat("Error: PersistentCoordinates failed starting MLPersistentStore, disabling script. Reason: {0}", result);
            enabled = false;
            return;
        }

        result = MLPersistentCoordinateFrames.Start();
        if (!result.IsOk)
        {
            MLPersistentStore.Stop();
            Debug.LogErrorFormat("Error: PersistentCoordinates failed starting MLPersistentCoordinateFrames, disabling script. Reason: {0}", result);
            enabled = false;
            return;
        }

        if (MLPersistentCoordinateFrames.IsReady)
        {
            Inititalize();
        }
        else
        {
            MLPersistentCoordinateFrames.OnInitialized += OnPcfInitialized;
        }
    }

    void Inititalize()
    {
        List<MLContentBinding> allBindings = MLPersistentStore.AllBindings;
        foreach (MLContentBinding binding in allBindings)
        {
            InstantiatePersistentPrefab(binding.ObjectId);
        }
        if (PointBehaviors.Count == 0)
        {
            InstantiatePersistentPrefab(Guid.NewGuid().ToString());
        }
    }

    void InstantiatePersistentPrefab(string id)
    {
        GameObject gameObj = Instantiate(PersistentCoordinatesPrefab, Vector3.zero, Quaternion.identity);
        MLPersistentBehavior persistentBehavior = gameObj.GetComponent<MLPersistentBehavior>();
        persistentBehavior.UniqueId = id;
        PointBehaviors.Add(persistentBehavior);
        PcfRegistry pcfRegistry = new PcfRegistry
        {
            Behavior = persistentBehavior,
            Parent = this
        };
        persistentBehavior.OnStatusUpdate += pcfRegistry.HandlePersistentCoordStatusUpdate;
    }

    struct PcfRegistry
    {
        internal MLPersistentBehavior Behavior;
        internal PersistentCoordinates Parent;

        internal void HandlePersistentCoordStatusUpdate(MLPersistentBehavior.Status status, MLResult result)
        {
            Debug.Log(string.Format("HandlePersistentCoordStatusUpdate: {0}, Result: {1}", status, result));
            //if (result.IsOk)
            {
                Parent.OnPersistentCoordinatesInitialized?.Invoke(Behavior.transform, Behavior.UniqueId);
            }
        }
    }

    void HandlePersistentCoordStatusUpdate(MLPersistentBehavior.Status status, MLResult result)
    {
        Debug.Log(string.Format("HandlePersistentCoordStatusUpdate: {0}, Result: {1}", status, result));
    }

    void OnPcfInitialized(MLResult status)
    {
        MLPersistentCoordinateFrames.OnInitialized -= OnPcfInitialized;
        if (status.IsOk)
        {
            Inititalize();
        }
        else
        {
            Debug.LogErrorFormat("Error: MLPersistentCoordinateFrames failed to initialize, disabling script. Reason: {0}", status);
            enabled = false;
        }

    }
}
