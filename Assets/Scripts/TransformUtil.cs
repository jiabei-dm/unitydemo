using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransformUtil 
{
    public static Vector3 ExtractPosition(Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }
}
