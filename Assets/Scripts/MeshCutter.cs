using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshCutter {
    private class CutGroup {
        public List<Vector3> Positions = new List<Vector3>();
        public List<int> Triangles = new List<int>();

        public List<Vector3> CapPositions = new List<Vector3>();
        public List<int> CapTriangles = new List<int>();

        public void Capping(Plane plane, bool isFront) {
            // 中心点を求める
            var center = Vector3.zero;
            foreach (var v in CapPositions) {
                center += v;
            }
            center = center / CapPositions.Count;

            // 中心点を入れる
            CapPositions.Add(center);

            var centerIdx = CapPositions.Count - 1;
            for (int i = 0; i < CapPositions.Count - 1; i += 2) {
                var idx0 = centerIdx;
                var idx1 = i;
                var idx2 = i + 1;

                var cross = Vector3.Cross(CapPositions[idx2] - CapPositions[idx0], CapPositions[idx1] - CapPositions[idx0]);
                var inner = Vector3.Dot(cross, plane.normal);

                // plateに対してどちら側の蓋かによって計算が変わるのに注意
                if (isFront) {
                    if (inner < 0) {
                        idx0 = idx2;
                        idx2 = centerIdx;
                    }
                } else {
                    if (inner > 0) {
                        idx0 = idx2;
                        idx2 = centerIdx;
                    }
                }

                // indexを詰める
                CapTriangles.Add(idx0);
                CapTriangles.Add(idx1);
                CapTriangles.Add(idx2);
            }

            // マージ
            MergeCap();
        }

        public void MergeCap() {
            var count = Positions.Count;

            // positions
            Positions.AddRange(CapPositions);

            // triangles
            foreach (var idx in CapTriangles) {
                Triangles.Add(count + idx);
            }

            CapPositions.Clear();
            CapTriangles.Clear();
        }

        public GameObject CreateObject(CutVictim original) {
            var obj = new GameObject("cut obj", typeof(CutVictim), typeof(Rigidbody2D));

            var mesh = new Mesh {
                vertices = Positions.ToArray(),
                triangles = Triangles.ToArray()
            };
            mesh.RecalculateNormals();

            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = original.Material;
            var collider = obj.GetComponent<PolygonCollider2D>();
            collider.points = original.Collider.points;

            var rigidBody = obj.GetComponent<Rigidbody2D>();
            var force = new Vector2(Random.Range(-5, 5), Random.Range(-5, 5));
            rigidBody.AddForce(force, ForceMode2D.Impulse);

            return obj;
        }
    }

    private Vector3 pos1; // planeとmeshの交点その1
    private Vector3 pos2; // planeとmeshの交点その2

    public void Cut(Plane plane, CutVictim victim) {
        var group1 = new CutGroup();
        var group2 = new CutGroup();

        for (var i = 0; i < victim.Triangles.Length; i += 3) {
            var positions1 = new List<Vector3>();
            var positions2 = new List<Vector3>();

            var idx0 = victim.Triangles[i];
            var idx1 = victim.Triangles[i + 1];
            var idx2 = victim.Triangles[i + 2];

            var vertices = new List<Vector3>();
            var v1 = Vector3.Scale(victim.Vertices[idx0], victim.Scale) + victim.Position;
            vertices.Add(v1);
            var v2 = Vector3.Scale(victim.Vertices[idx1], victim.Scale) + victim.Position;
            vertices.Add(v2);
            var v3 = Vector3.Scale(victim.Vertices[idx2], victim.Scale) + victim.Position;
            vertices.Add(v3);

            // そのポリゴンの法線を計算しておく
            var normal = Vector3.Cross(victim.Vertices[idx2] - victim.Vertices[idx0], victim.Vertices[idx1] - victim.Vertices[idx0]);

            AllocateVertices(plane, vertices, positions1, positions2); // 1.グループ分け

            // どちらにもカウントがあるということはplateと交差しているポリゴンということ
            if (positions1.Count > 0 && positions2.Count > 0) {
                CalcCrossPoint(plane, positions1, positions2); // 2.planeとの交点を求める

                // 3.両方のグループともに交点を入れる
                positions1.Add(pos1);
                positions1.Add(pos2);
                group1.CapPositions.Add(pos1);
                group1.CapPositions.Add(pos2);

                positions2.Add(pos1);
                positions2.Add(pos2);
                group2.CapPositions.Add(pos1);
                group2.CapPositions.Add(pos2);
            }

            if (positions1.Count > 0) {
                var triangles = CreateTriangles(positions1, normal);
                var count = group1.Positions.Count;

                group1.Positions.AddRange(positions1);

                // 二つめ以降ならidxがずれることに注意
                foreach (var idx in triangles) {
                    group1.Triangles.Add(idx + count);
                }
            }

            if (positions2.Count > 0) {
                var triangles = CreateTriangles(positions2, normal);
                var count = group2.Positions.Count;

                group2.Positions.AddRange(positions2);

                // 二つめ以降ならidxがずれることに注意
                foreach (var idx in triangles) {
                    group2.Triangles.Add(idx + count);
                }
            }
        }

        // 蓋を作る
        group1.Capping(plane, true);
        group2.Capping(plane, true);

        // 4.2つのグループに分けたオブジェクトを作成する
        group1.CreateObject(victim);
        group2.CreateObject(victim);

        // 5.元オブジェクトを消去する
        Object.Destroy(victim.gameObject);
    }

    // planeのどちらにあるかを計算して振り分ける
    private void AllocateVertices(Plane plane, List<Vector3> vertices, List<Vector3> group1, List<Vector3> group2) {
        foreach (var v in vertices) {
            if (plane.GetSide(v)) {
                group1.Add(v);
            } else {
                group2.Add(v);
            }
        }
    }

    // planeとmeshの交点を求める
    private void CalcCrossPoint(Plane plane, List<Vector3> group1, List<Vector3> group2) {
        float distance = 0;
        Vector3 basePos; // 計算する基準となる頂点
        Vector3 tmpPos1; // 基準点以外の頂点1
        Vector3 tmpPos2; // 基準点以外の頂点2

        // 少ない方からplaneに対して交差するpointを聞く
        if (group2.Count < group1.Count) {
            basePos = group2[0];
            tmpPos1 = group1[0];
            tmpPos2 = group1[1];
        } else {
            basePos = group1[0];
            tmpPos1 = group2[0];
            tmpPos2 = group2[1];
        }

        // 少ない所から多い片方の頂点に向かってrayを飛ばす。
        var ray1 = new Ray(basePos, (tmpPos1 - basePos).normalized);
        // planeと交差する距離を求める
        plane.Raycast(ray1, out distance);
        // ray1がその距離を進んだ位置を取得(ここが交点になる)
        pos1 = ray1.GetPoint(distance);

        // 同じようにもう片方も計算
        var ray2 = new Ray(basePos, (tmpPos2 - basePos).normalized);
        plane.Raycast(ray2, out distance);
        pos2 = ray2.GetPoint(distance);
    }

    // 頂点インデックスを計算する
    private List<int> CreateTriangles(List<Vector3> vertices, Vector3 normal) {
        if (vertices.Count < 3) {
            return null;
        }

        var triangles = new List<int>();

        var idx = 0;

        var idx0 = 0; // 0固定
        var idx1 = 0;
        var idx2 = 0;

        var cross = Vector3.zero;
        var inner = 0.0f;

        for (int i = 0; i < vertices.Count; i += 3) {
            idx0 = idx;
            idx1 = idx + 1;
            idx2 = idx + 2;

            cross = Vector3.Cross(vertices[idx2] - vertices[idx0], vertices[idx1] - vertices[idx0]);
            inner = Vector3.Dot(cross, normal);

            // 逆向いている場合は反転させる
            if (inner < 0) {
                idx0 = idx2;
                idx2 = idx;
            }

            triangles.Add(idx0);
            triangles.Add(idx1);
            triangles.Add(idx2);

            idx++;
        }

        return triangles;
    }

    // 切ったオブジェクトの蓋を作る
    private void Capping(List<Vector3> capPositions, List<int> capTriangles, Plane plane, bool isFront) {
        // 中心点を求める
        var center = Vector3.zero;
        foreach (var v in capPositions) {
            center += v;
        }
        center = center / capPositions.Count;

        // 中心点を入れる
        capPositions.Add(center);

        var centerIdx = capPositions.Count - 1;
        for (int i = 0; i < capPositions.Count - 1; i += 2) {
            var idx0 = centerIdx;
            var idx1 = i;
            var idx2 = i + 1;

            var cross = Vector3.Cross(capPositions[idx2] - capPositions[idx0], capPositions[idx1] - capPositions[idx0]);
            var inner = Vector3.Dot(cross, plane.normal);

            // plateに対してどちら側の蓋かによって計算が変わるのに注意
            if (isFront) {
                if (inner < 0) {
                    idx0 = idx2;
                    idx2 = centerIdx;
                }
            } else {
                if (inner > 0) {
                    idx0 = idx2;
                    idx2 = centerIdx;
                }
            }

            // indexを詰める
            capTriangles.Add(idx0);
            capTriangles.Add(idx1);
            capTriangles.Add(idx2);
        }
    }

    // cutしたmeshを作る
    private void CreateCutObj(List<Vector3> vertices, List<int> triangles, Material material) {
        var obj = new GameObject("cut obj", typeof(CutVictim), typeof(Rigidbody2D));

        var mesh = new Mesh {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();

        obj.GetComponent<MeshFilter>().mesh = mesh;
        obj.GetComponent<MeshRenderer>().material = material;

        var rigidBody = obj.GetComponent<Rigidbody2D>();
        var force = new Vector2(Random.Range(-5, 5), Random.Range(-5, 5));
        rigidBody.AddForce(force, ForceMode2D.Impulse);
    }
}