using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float turnSpeed = 20f;

    Vector3 _target;
    Vector3 _Movement;
    Animator _Animator;
    Quaternion _Rotation = Quaternion.identity;
    Rigidbody _Rigibody;

    WaypointsList PlayBackList;
    int CurrentIndex = -1;

    // Start is called before the first frame update
    void Start()
    {
        _Animator = GetComponent<Animator>();
        _Rigibody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        _Movement = _target - _Rigibody.position;
        _Movement.y = 0;
        if (_Movement.magnitude > 1f)
        {
            _Movement.Normalize();
        }

        bool isWalking = _Movement.magnitude > 0.1f;
        bool wasWalking = _Animator.GetBool("IsWalking");

        _Animator.SetBool("IsWalking", isWalking);

        Vector3 desiredForward = Vector3.RotateTowards(transform.forward, _Movement, turnSpeed * Time.deltaTime, 0f);
        _Rotation = Quaternion.LookRotation(desiredForward);

        if (!isWalking)
        {
            if (CurrentIndex > -1 && CurrentIndex < PlayBackList.Waypoints.Count - 1)
            {
                CurrentIndex++;
                MoveTo(PlayBackList.GetWayPoint(CurrentIndex).position);
            }
        }
    }

    public void ForceMove(Vector3 location)
    {
        CurrentIndex = -1;
        MoveTo(location);
    }

    private void MoveTo(Vector3 location)
    {
        _target = location;
        if (transform.localScale.Equals(Vector3.zero))
        {
            transform.localScale = Vector3.one;
            transform.position = location;
        }
    }

    private void OnAnimatorMove()
    {
        _Rigibody.MovePosition(_Rigibody.position + _Movement * _Animator.deltaPosition.magnitude);
        _Rigibody.MoveRotation(_Rotation);
    }

    public void PlayBack(WaypointsList waypointsList)
    {
        if (waypointsList.Waypoints.Count <= 0)
        {
            return;
        }
        PlayBackList = waypointsList;
        transform.localScale = Vector3.zero;
        MoveTo(PlayBackList.GetWayPoint(0).position);
        CurrentIndex = 0;
    }

}
