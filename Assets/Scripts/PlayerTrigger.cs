using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.MagicLeap;
using UnityEngine;

public class PlayerTrigger : MonoBehaviour
{
    [Space, SerializeField, Tooltip("ControllerConnectionHandler reference.")]
    private ControllerConnectionHandler _controllerConnectionHandler = null;

    [SerializeField, Tooltip("RaycastBehaviour.")]
    private RaycastBehaviour _raycastBehaviour = null;

    [SerializeField, Tooltip("PlayerMovement.")]
    private PlayerMovement _playerMovement = null;

    // Start is called before the first frame update
    void Awake()
    {
        if (_controllerConnectionHandler == null)
        {
            Debug.LogError("Error: PlayerTrigger._controllerConnectionHandler not set, disabling script.");
            enabled = false;
            return;
        }
        if (_raycastBehaviour == null)
        {
            Debug.LogError("Error: PlayerTrigger._raycastBehaviour not set, disabling script.");
            enabled = false;
            return;
        }
        if (_playerMovement == null)
        {
            Debug.LogError("Error: PlayerTrigger._playerMovement not set, disabling script.");
            enabled = false;
            return;
        }
        transform.localScale = Vector3.zero;
        MLInput.OnTriggerDown += OnTriggerDown;
    }

    void OnDestroy()
    {
        MLInput.OnTriggerDown -= OnTriggerDown;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerDown(byte controllerId, float triggerValue)
    {
        if (_controllerConnectionHandler.IsControllerValid(controllerId) && triggerValue >= 0.5f) 
        {
            BaseRaycast raycast = _raycastBehaviour.GetActiveRaycast();
            if (raycast != null)
            {
                _playerMovement.ForceMove(raycast.transform.position);
            }
        }
    }
}
