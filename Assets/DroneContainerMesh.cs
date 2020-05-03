using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class DroneContainerMesh : MonoBehaviour
{
    [Range(0, 45)]
    public byte topSlopeDegrees = 30;
    [Range(0, 45)]
    public byte bottomSlopeDegrees = 10;

    // Start is called before the first frame update
    void Start()
    {
        var size = new Vector3(1, 1, 1);
        var max = new Vector3(size.x / 2, size.y / 2, size.z / 2);
        var min = -max;


        var topSlopeRadians = topSlopeDegrees * (Math.PI / 180);
        var bottomSlopeRadians = bottomSlopeDegrees * (Math.PI / 180);


        var meshFilter = GetComponent<MeshFilter>();
        var mesh = meshFilter.sharedMesh;
        var frontTopY = max.y - (float)Math.Tan(topSlopeRadians);
        var frontBottomY = min.y + (float)Math.Tan(bottomSlopeRadians);
        Vector3[] vertices;
        vertices = new[]
        {
            // front face //
            new Vector3(min.x, frontTopY, max.z),
            new Vector3(max.x, frontTopY, max.z),
            new Vector3(min.x, frontBottomY, max.z),
            new Vector3(max.x, frontBottomY, max.z),
            // back face //
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, min.y, min.z),
            // left face //
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, frontTopY, max.z),
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, frontBottomY, max.z),
            // right face //
            new Vector3(max.x, frontTopY, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, frontBottomY, max.z),
            new Vector3(max.x, min.y, min.z),
            // top face //
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, frontTopY, max.z),
            new Vector3(max.x, frontTopY, max.z),
            // bottom face //
            new Vector3(min.x, frontBottomY, max.z),
            new Vector3(max.x, frontBottomY, max.z),
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
        };
        var triangles = new[]
        {
            // front face //
            0, 2, 3,
            3, 1, 0,
            // back face //
            4, 6, 7,
            7, 5, 4,
            // left face //
            8, 10, 11,
            11, 9, 8,
            // right face //
            12, 14, 15,
            15, 13, 12,
            // top face //
            16, 18, 19,
            19, 17, 16,
            // bottom face //
            20, 22, 23,
            23, 21, 20
        };
        var uv = new[]
        {
            // front face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0),
            // back face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0),
            // left face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0),
            // right face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0),
            // top face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0),
            // bottom face //
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.Optimize();
        mesh.RecalculateNormals();
    }

    // Update is called once per frame
    void OnValidate()
    {
        Start();
    }
}
