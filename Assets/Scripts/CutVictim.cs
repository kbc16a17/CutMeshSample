using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
public class CutVictim : MonoBehaviour {
    private new MeshRenderer renderer;
    private new PolygonCollider2D collider;
    private MeshFilter filter;
    private Mesh mesh => filter.mesh;

    public PolygonCollider2D Collider => collider;

    public Vector3 Position => transform.position;
    public Vector3 Scale => transform.localScale;

    public Vector3[] Vertices => mesh.vertices;
    public int[] Triangles => mesh.triangles;
    public Vector2[] UV => mesh.uv;
    public Vector3[] Normals => mesh.normals;

    public Material Material => renderer.material;

    private void Awake() {
        renderer = GetComponent<MeshRenderer>();
        collider = GetComponent<PolygonCollider2D>();
        filter = GetComponent<MeshFilter>();
    }
}