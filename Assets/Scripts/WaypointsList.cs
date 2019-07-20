using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WaypointsList
{
    public List<Matrix4x4> Waypoints;

    public Matrix4x4 PcfTransformMatrix;
    public Matrix4x4 PcfReverseTransformMatrix;
    public string pcfUid;

    public WaypointsList()
    {
        Waypoints = new List<Matrix4x4>();
    }

    public WaypointsList(Transform pcfTransform)
    {
        Waypoints = new List<Matrix4x4>();
        SetTransform(pcfTransform);
    }

    public void SetTransform(Transform pcfTransform)
    {
        PcfTransformMatrix = pcfTransform.worldToLocalMatrix;
        PcfReverseTransformMatrix = pcfTransform.localToWorldMatrix;
    }

    public void AddWaypoints(Transform transform) 
    {
        Waypoints.Add(PcfTransformMatrix * transform.localToWorldMatrix);
    }

    public Pose GetWayPoint(int i)
    {
        if (i >= 0 && i < Waypoints.Count)
        {
            Matrix4x4 matrix = PcfReverseTransformMatrix * Waypoints[i];
            return new Pose
            {
                position = TransformUtil.ExtractPosition(matrix),
                rotation = matrix.rotation
            };

        }
        return new Pose { };
    }
}
