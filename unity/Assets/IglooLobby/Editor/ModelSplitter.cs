using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IglooLobby.EditorTools
{
    /// <summary>
    /// 메뉴: Tools > Igloo > Split Selected Model Into Prefabs
    /// 망치 4개가 한 파일(FBX/OBJ)에 들어있을 때 각각 별도 프리팹으로 분리한다.
    ///  1) 모델 안에 렌더러가 달린 자식 오브젝트가 여러 개면 → 자식별로 분리
    ///  2) 메시가 하나로 합쳐져 있으면 → 서로 떨어진 덩어리를 찾아 분리 (머리/손잡이처럼 맞닿은 조각은 한 덩어리로 묶음)
    /// 결과: 모델과 같은 폴더의 "<모델이름>_Split" 폴더에 왼쪽→오른쪽 순서로 Part_1, Part_2 ... 프리팹 생성.
    /// 각 프리팹의 기준점(pivot)은 덩어리의 맨 아래 가운데(=망치 손잡이 끝)로 맞춰진다.
    /// </summary>
    public static class ModelSplitter
    {
        [MenuItem("Tools/Igloo/Split Selected Model Into Prefabs")]
        static void SplitSelected()
        {
            var src = Selection.activeGameObject;
            if (src == null) { EditorUtility.DisplayDialog("분리", "Project 창이나 Hierarchy 에서 모델을 선택하세요.", "확인"); return; }

            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = string.IsNullOrEmpty(srcPath) ? "Assets" : Path.GetDirectoryName(srcPath).Replace('\\', '/');
            string outDir = dir + "/" + src.name + "_Split";
            if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(dir, src.name + "_Split");

            var temp = Object.Instantiate(src);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var parts = CollectParts(temp, outDir);
                if (parts.Count == 0) { EditorUtility.DisplayDialog("분리", "메시를 찾지 못했습니다.", "확인"); return; }
                parts.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x));
                for (int i = 0; i < parts.Count; i++)
                {
                    var p = parts[i];
                    var root = new GameObject(src.name + "_" + (i + 1));
                    Vector3 pivot = new Vector3(p.bounds.center.x, p.bounds.min.y, p.bounds.center.z);
                    foreach (var piece in p.objects)
                    {
                        piece.transform.SetParent(root.transform, true);
                        piece.transform.position -= pivot;
                    }
                    string path = AssetDatabase.GenerateUniqueAssetPath(outDir + "/" + root.name + ".prefab");
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Object.DestroyImmediate(root);
                }
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("분리 완료", parts.Count + "개 프리팹을 만들었습니다:\n" + outDir, "확인");
            }
            finally
            {
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        class Part { public Bounds bounds; public List<GameObject> objects = new List<GameObject>(); }

        static List<Part> CollectParts(GameObject temp, string outDir)
        {
            var parts = new List<Part>();
            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length >= 2)
            {
                // 렌더러별 조각 -> 겹치는 것끼리 묶기
                foreach (var r in renderers)
                {
                    var go = Object.Instantiate(r.gameObject);
                    for (int c = go.transform.childCount - 1; c >= 0; c--) Object.DestroyImmediate(go.transform.GetChild(c).gameObject);
                    go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                    go.transform.localScale = r.transform.lossyScale;
                    var p = new Part { bounds = r.bounds };
                    p.objects.Add(go);
                    parts.Add(p);
                }
                return MergeTouching(parts);
            }
            if (renderers.Length == 1)
            {
                var r = renderers[0];
                Mesh mesh = null;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) mesh = smr.sharedMesh;
                if (mesh == null) return parts;
                var islands = SplitMesh(mesh, r.transform.localToWorldMatrix, outDir);
                foreach (var isl in islands)
                {
                    var go = new GameObject(isl.name);
                    go.AddComponent<MeshFilter>().sharedMesh = isl;
                    go.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                    var p = new Part { bounds = isl.bounds };
                    p.objects.Add(go);
                    parts.Add(p);
                }
                return MergeTouching(parts);
            }
            return parts;
        }

        static List<Part> MergeTouching(List<Part> parts)
        {
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < parts.Count && !merged; i++)
                    for (int j = i + 1; j < parts.Count && !merged; j++)
                    {
                        var a = parts[i].bounds; var b = parts[j].bounds;
                        float margin = Mathf.Min(a.size.magnitude, b.size.magnitude) * 0.02f;
                        a.Expand(margin);
                        if (a.Intersects(b))
                        {
                            parts[i].bounds.Encapsulate(parts[j].bounds);
                            parts[i].objects.AddRange(parts[j].objects);
                            parts.RemoveAt(j);
                            merged = true;
                        }
                    }
            }
            return parts;
        }

        /// <summary>정점이 연결된 덩어리별로 메시를 나눈다 (월드 좌표로 구워서 저장).</summary>
        static List<Mesh> SplitMesh(Mesh src, Matrix4x4 toWorld, string outDir)
        {
            var verts = src.vertices;
            var normals = src.normals;
            var uvs = src.uv;
            int n = verts.Length;

            // 같은 위치의 정점을 하나로 보고 union-find
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            System.Func<int, int> find = null;
            find = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
            System.Action<int, int> union = (a, b) => { a = find(a); b = find(b); if (a != b) parent[a] = b; };

            var byPos = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < n; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 10000f);
                int other;
                if (byPos.TryGetValue(key, out other)) union(i, other); else byPos[key] = i;
            }
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var t = src.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3) { union(t[i], t[i + 1]); union(t[i], t[i + 2]); }
            }

            var groups = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) { int r = find(i); if (!groups.ContainsKey(r)) groups[r] = groups.Count; }

            var result = new List<Mesh>();
            var normalMat = toWorld.inverse.transpose;
            foreach (var kv in groups)
            {
                int root = kv.Key;
                var map = new Dictionary<int, int>();
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>();
                var subs = new List<int>[src.subMeshCount];
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    subs[s] = new List<int>();
                    var t = src.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        if (find(t[i]) != root) continue;
                        for (int k = 0; k < 3; k++)
                        {
                            int vi = t[i + k];
                            int ni;
                            if (!map.TryGetValue(vi, out ni))
                            {
                                ni = nv.Count; map[vi] = ni;
                                nv.Add(toWorld.MultiplyPoint3x4(verts[vi]));
                                if (normals.Length == n) nn.Add(normalMat.MultiplyVector(normals[vi]).normalized);
                                if (uvs.Length == n) nu.Add(uvs[vi]);
                            }
                            subs[s].Add(ni);
                        }
                    }
                }
                if (nv.Count == 0) continue;
                var m = new Mesh { name = src.name + "_part" + kv.Value };
                if (nv.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(nv);
                if (nn.Count == nv.Count) m.SetNormals(nn);
                if (nu.Count == nv.Count) m.SetUVs(0, nu);
                m.subMeshCount = src.subMeshCount;
                for (int s = 0; s < src.subMeshCount; s++) m.SetTriangles(subs[s], s);
                if (nn.Count != nv.Count) m.RecalculateNormals();
                m.RecalculateBounds();
                m.RecalculateTangents();
                AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath(outDir + "/" + m.name + ".asset"));
                result.Add(m);
            }
            return result;
        }
    }
}
