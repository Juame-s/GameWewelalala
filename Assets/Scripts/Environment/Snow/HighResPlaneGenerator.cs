using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HighResPlaneGenerator : MonoBehaviour
{
    [Header("Plane Settings")]
    [Tooltip("Total size of the plane in Unity units.")]
    public float size = 50f;
    [Tooltip("Number of vertices along one side. Higher = more detailed snow craters, but more expensive.")]
    public int resolution = 100;

    [ContextMenu("Generate Plane")]
    public void GeneratePlane()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        mesh.name = "HighResPlane";

        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[resolution * resolution * 6];

        float halfSize = size * 0.5f;
        float stepSize = size / resolution;

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                int index = z * (resolution + 1) + x;
                vertices[index] = new Vector3(-halfSize + x * stepSize, 0f, -halfSize + z * stepSize);
                uvs[index] = new Vector2((float)x / resolution, (float)z / resolution);
                normals[index] = Vector3.up;
            }
        }

        int t = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;
                triangles[t + 0] = i;
                triangles[t + 1] = i + resolution + 1;
                triangles[t + 2] = i + 1;
                triangles[t + 3] = i + 1;
                triangles[t + 4] = i + resolution + 1;
                triangles[t + 5] = i + resolution + 2;
                t += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        filter.mesh = mesh;
    }

    private void Start()
    {
        GeneratePlane();
    }
}
