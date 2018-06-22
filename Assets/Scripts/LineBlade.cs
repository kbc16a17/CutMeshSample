using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LineBlade : MonoBehaviour {
    public MeshCutter cutter = new MeshCutter();
    private Plane plane = new Plane();

    private new LineRenderer renderer;

    private Vector3 normal;
    private Vector3 center_pos;

    private Vector3 start_pos;
    private Vector3 end_pos;

    private bool dragging = false;

    private void Awake() {
        renderer = GetComponent<LineRenderer>();
        renderer.enabled = false;
    }

    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            var mpos = Input.mousePosition;
            mpos.z = 10.0f;
            start_pos = Camera.main.ScreenToWorldPoint(mpos);

            dragging = true;
            renderer.enabled = true;
        }

        if (dragging) {
            var mpos = Input.mousePosition;
            mpos.z = 10.0f;
            end_pos = Camera.main.ScreenToWorldPoint(mpos);

            renderer.SetPositions(new Vector3[] { start_pos, end_pos });
        }

        if (Input.GetMouseButtonUp(0)) {
            Create();

            var victims = FindCutVictims();
            victims.ForEach(v => cutter.Cut(plane, v));

            dragging = false;
        }
    }

    private List<CutVictim> FindCutVictims() {
        var direction = end_pos - start_pos;
        var distance = direction.magnitude;
        var hits = Physics2D.RaycastAll(start_pos, direction, distance);

        return hits
            .Where(h => h.collider.gameObject.GetComponent<CutVictim>())
            .Select(h => h.collider.gameObject.GetComponent<CutVictim>())
            .ToList();
    }

    private void Create() {
        center_pos = (start_pos + end_pos) / 2;
        var p1 = start_pos - center_pos;
        normal = (Quaternion.Euler(0f, 0f, 90f) * p1).normalized;

        plane.SetNormalAndPosition(normal, center_pos);
    }

    void OnDrawGizmosSelected() {
        var length = 10.0f;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(center_pos, center_pos + (normal * length));
    }
}
