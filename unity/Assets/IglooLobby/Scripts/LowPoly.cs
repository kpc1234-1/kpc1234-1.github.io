using System.Collections.Generic;
using UnityEngine;

namespace IglooLobby
{
    /// <summary>
    /// 로우폴리(면이 각진) 메시를 코드로 만드는 도구 모음.
    /// 모든 삼각형이 자기 정점을 따로 가지므로 RecalculateNormals 만으로 플랫 셰이딩이 된다.
    /// </summary>
    public static class LowPoly
    {
        public class MeshBuilder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<int> Tris = new List<int>();

            void Raw(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = Verts.Count;
                Verts.Add(a); Verts.Add(b); Verts.Add(c);
                Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
            }

            /// <summary>면의 법선이 dir 쪽을 향하도록 삼각형을 추가.</summary>
            public void TriDir(Vector3 a, Vector3 b, Vector3 c, Vector3 dir)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, dir) >= 0f) Raw(a, b, c); else Raw(a, c, b);
            }

            /// <summary>면의 법선이 center 반대쪽(바깥)을 향하도록 삼각형을 추가.</summary>
            public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 center)
            {
                TriDir(a, b, c, (a + b + c) / 3f - center);
            }

            public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
            {
                TriOut(a, b, c, center);
                TriOut(a, c, d, center);
            }

            public void QuadDir(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 dir)
            {
                TriDir(a, b, c, dir);
                TriDir(a, c, d, dir);
            }

            public void Append(MeshBuilder other, Matrix4x4 m)
            {
                int start = Verts.Count;
                for (int i = 0; i < other.Verts.Count; i++) Verts.Add(m.MultiplyPoint3x4(other.Verts[i]));
                // 음수 스케일이면 와인딩을 뒤집는다
                bool flip = m.determinant < 0f;
                for (int i = 0; i < other.Tris.Count; i += 3)
                {
                    Tris.Add(start + other.Tris[i]);
                    if (flip) { Tris.Add(start + other.Tris[i + 2]); Tris.Add(start + other.Tris[i + 1]); }
                    else { Tris.Add(start + other.Tris[i + 1]); Tris.Add(start + other.Tris[i + 2]); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (Verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Verts);
                mesh.SetTriangles(Tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static float Range(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

        // ------------------------------------------------------------------
        // 얼음 블록: 바깥면(+Z)이 살짝 볼록한 피라미드 형태 (이미지의 이글루 벽돌 느낌)
        // ------------------------------------------------------------------
        public static MeshBuilder PillowBlock(float w, float h, float d, float bevel, float bulge, System.Random rng = null)
        {
            var mb = new MeshBuilder();
            float hw = w * 0.5f, hh = h * 0.5f, hd = d * 0.5f;
            bevel = Mathf.Min(bevel, Mathf.Min(hw, hh) * 0.6f);
            Vector2[] sign = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            var B = new Vector3[4]; var M = new Vector3[4]; var F = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                B[i] = new Vector3(sign[i].x * hw, sign[i].y * hh, -hd);
                M[i] = new Vector3(sign[i].x * hw, sign[i].y * hh, hd - bevel);
                float jx = rng != null ? Range(rng, -0.25f, 0.25f) * bevel : 0f;
                float jy = rng != null ? Range(rng, -0.25f, 0.25f) * bevel : 0f;
                F[i] = new Vector3(sign[i].x * (hw - bevel) + jx, sign[i].y * (hh - bevel) + jy, hd);
            }
            Vector3 C = new Vector3(
                rng != null ? Range(rng, -0.15f, 0.15f) * w : 0f,
                rng != null ? Range(rng, -0.15f, 0.15f) * h : 0f,
                hd + bulge);
            Vector3 o = Vector3.zero;
            mb.QuadOut(B[0], B[1], B[2], B[3], o);
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                mb.QuadOut(B[i], B[n], M[n], M[i], o);
                mb.QuadOut(M[i], M[n], F[n], F[i], o);
                mb.TriOut(F[i], F[n], C, o);
            }
            return mb;
        }

        /// <summary>정육면체 얼음 큐브 (윗면 모서리를 깎음, 윗면이 +Y).</summary>
        public static MeshBuilder IceCube(float size, System.Random rng)
        {
            var mb = new MeshBuilder();
            var blk = PillowBlock(size, size, size, size * 0.07f, size * 0.015f, rng);
            // PillowBlock 의 바깥면(+Z)을 위(+Y)로 돌리고 바닥이 y=0 에 오도록
            mb.Append(blk, Matrix4x4.TRS(new Vector3(0, size * 0.5f, 0), Quaternion.Euler(-90f, 0, 0), Vector3.one));
            return mb;
        }

        // ------------------------------------------------------------------
        // 이글루: 블록을 반구 형태로 쌓고 앞쪽(-Z)에 아치형 입구 터널
        // ------------------------------------------------------------------
        public class IglooMeshes
        {
            public MeshBuilder Blocks = new MeshBuilder();
            public MeshBuilder Interior = new MeshBuilder();
        }

        public static IglooMeshes Igloo(float radius, System.Random rng)
        {
            var result = new IglooMeshes();
            float R = radius;
            float depth = R * 0.17f;
            float gap = R * 0.018f;
            float targetW = R * 0.36f;
            int rows = Mathf.Max(4, Mathf.RoundToInt((R * Mathf.PI * 0.5f) / (R * 0.19f)));
            float dPhi = (Mathf.PI * 0.5f) / rows;
            float entranceTheta = Mathf.PI;       // -Z 방향
            float entranceHalf = 26f * Mathf.Deg2Rad;
            int entranceRows = 3;
            float capPhi = Mathf.PI * 0.5f;

            for (int i = 0; i < rows; i++)
            {
                float phi0 = i * dPhi, phi1 = phi0 + dPhi;
                if (R * Mathf.Cos(phi1) < R * 0.28f) { capPhi = phi0; break; }
                float phi = (phi0 + phi1) * 0.5f;
                float ring = R * Mathf.Cos(phi);
                int k = Mathf.Max(5, Mathf.RoundToInt(2f * Mathf.PI * ring / targetW));
                float step = 2f * Mathf.PI / k;
                float bw = ring * step - gap;
                float bh = R * dPhi - gap;
                float offset = (i % 2) * step * 0.5f;
                for (int j = 0; j < k; j++)
                {
                    float th = offset + j * step;
                    float diff = Mathf.Abs(Mathf.DeltaAngle(th * Mathf.Rad2Deg, entranceTheta * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    if (i < entranceRows && diff < entranceHalf) continue;
                    Vector3 n = new Vector3(Mathf.Cos(phi) * Mathf.Sin(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(th));
                    Vector3 pos = n * (R - depth * 0.5f);
                    Quaternion rot = Quaternion.LookRotation(n, Vector3.up);
                    var blk = PillowBlock(bw, bh, depth, depth * 0.22f, depth * 0.18f, rng);
                    result.Blocks.Append(blk, Matrix4x4.TRS(pos, rot, Vector3.one));
                }
            }

            // 꼭대기 덮개
            {
                int seg = 7;
                float y0 = R * Mathf.Sin(capPhi), r0 = R * Mathf.Cos(capPhi);
                Vector3 top = new Vector3(0, R + depth * 0.05f, 0);
                for (int s = 0; s < seg; s++)
                {
                    float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
                    Vector3 p0 = new Vector3(Mathf.Sin(a0) * r0, y0, Mathf.Cos(a0) * r0);
                    Vector3 p1 = new Vector3(Mathf.Sin(a1) * r0, y0, Mathf.Cos(a1) * r0);
                    Vector3 m = Vector3.Lerp(p0, p1, 0.5f);
                    Vector3 mid = Vector3.Lerp(m, top, 0.5f).normalized * R;
                    result.Blocks.TriOut(p0, p1, mid, Vector3.zero);
                    result.Blocks.TriOut(p0, mid, top, Vector3.zero);
                    result.Blocks.TriOut(mid, p1, top, Vector3.zero);
                }
            }

            // 내부 (안쪽을 향하는 어두운 반구) - 블록 사이 틈과 입구 안쪽을 어둡게 보이게 함
            {
                float ri = R - depth * 0.9f;
                int lon = 16, lat = 6;
                for (int a = 0; a < lat; a++)
                    for (int b = 0; b < lon; b++)
                    {
                        float p0 = a * Mathf.PI * 0.5f / lat, p1 = (a + 1) * Mathf.PI * 0.5f / lat;
                        float t0 = b * Mathf.PI * 2f / lon, t1 = (b + 1) * Mathf.PI * 2f / lon;
                        Vector3 v00 = Sph(p0, t0) * ri, v01 = Sph(p0, t1) * ri, v10 = Sph(p1, t0) * ri, v11 = Sph(p1, t1) * ri;
                        // 법선이 중심을 향하도록
                        Vector3 c = (v00 + v11) * 0.5f;
                        result.Interior.QuadDir(v00, v01, v11, v10, -c);
                    }
                // 바닥
                for (int b = 0; b < lon; b++)
                {
                    float t0 = b * Mathf.PI * 2f / lon, t1 = (b + 1) * Mathf.PI * 2f / lon;
                    result.Interior.TriDir(Vector3.up * 0.01f, Sph(0, t0) * ri + Vector3.up * 0.01f, Sph(0, t1) * ri + Vector3.up * 0.01f, Vector3.up);
                }
            }

            // 입구 터널 (로컬 +Z 로 만든 뒤 180도 돌려 -Z 에 붙임)
            {
                var tunnel = new MeshBuilder();
                var inner = new MeshBuilder();
                float ra = R * 0.29f;          // 아치 안쪽 반지름
                float leg = R * 0.16f;         // 기둥 높이
                float td = depth;
                float zStart = R * 0.72f, zEnd = R + R * 0.42f;
                int segZ = 2;
                float segLen = (zEnd - zStart) / segZ;
                int archBlocks = 7;
                for (int s = 0; s < segZ; s++)
                {
                    float zc = zStart + segLen * (s + 0.5f);
                    float stagger = (s % 2 == 0) ? 0f : 0.5f;
                    // 기둥
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 n = new Vector3(side, 0, 0);
                        Vector3 pos = new Vector3(side * (ra + td * 0.5f), leg * 0.5f, zc);
                        var blk = PillowBlock(segLen - gap, leg - gap, td, td * 0.22f, td * 0.18f, rng);
                        tunnel.Append(blk, Matrix4x4.TRS(pos, Quaternion.LookRotation(n, Vector3.up), Vector3.one));
                    }
                    // 아치
                    int count = archBlocks + (s % 2);
                    for (int b = 0; b < count; b++)
                    {
                        float th0 = Mathf.PI * (b - stagger) / archBlocks;
                        float th1 = Mathf.PI * (b + 1 - stagger) / archBlocks;
                        th0 = Mathf.Clamp(th0, 0, Mathf.PI); th1 = Mathf.Clamp(th1, 0, Mathf.PI);
                        if (th1 - th0 < 0.05f) continue;
                        float th = (th0 + th1) * 0.5f;
                        Vector3 n = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0);
                        Vector3 tangent = new Vector3(-Mathf.Sin(th), Mathf.Cos(th), 0);
                        Vector3 pos = new Vector3(0, leg, zc) + n * (ra + td * 0.5f);
                        float arc = (ra + td) * (th1 - th0) - gap;
                        var blk = PillowBlock(segLen - gap, arc, td, td * 0.22f, td * 0.18f, rng);
                        tunnel.Append(blk, Matrix4x4.TRS(pos, Quaternion.LookRotation(n, tangent), Vector3.one));
                    }
                }
                // 터널 안쪽 어두운 면 (아치 모양 팬)
                float zIn = zStart + 0.02f;
                int fan = 12;
                var outline = new List<Vector3>();
                outline.Add(new Vector3(ra, 0, zIn));
                for (int f = 0; f <= fan; f++)
                {
                    float th = Mathf.PI * f / fan;
                    outline.Add(new Vector3(Mathf.Cos(th) * ra, leg + Mathf.Sin(th) * ra, zIn));
                }
                outline.Add(new Vector3(-ra, 0, zIn));
                Vector3 centre = new Vector3(0, leg * 0.6f, zIn);
                for (int f = 0; f < outline.Count - 1; f++) inner.TriDir(centre, outline[f], outline[f + 1], Vector3.forward);
                inner.TriDir(centre, outline[outline.Count - 1], outline[0], Vector3.forward);
                // 터널 안쪽 벽 (반원통, 안쪽을 향함)
                for (int f = 0; f < fan; f++)
                {
                    float a0 = Mathf.PI * f / fan, a1 = Mathf.PI * (f + 1) / fan;
                    Vector3 c0 = new Vector3(Mathf.Cos(a0) * ra, leg + Mathf.Sin(a0) * ra, 0);
                    Vector3 c1 = new Vector3(Mathf.Cos(a1) * ra, leg + Mathf.Sin(a1) * ra, 0);
                    Vector3 mid = (c0 + c1) * 0.5f;
                    Vector3 dirIn = new Vector3(0, leg, 0) - mid;
                    inner.QuadDir(c0 + Vector3.forward * zStart, c1 + Vector3.forward * zStart, c1 + Vector3.forward * zEnd, c0 + Vector3.forward * zEnd, dirIn);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 a = new Vector3(side * ra, 0, zStart), b = new Vector3(side * ra, leg, zStart);
                    Vector3 c = new Vector3(side * ra, leg, zEnd), d = new Vector3(side * ra, 0, zEnd);
                    inner.QuadDir(a, b, c, d, new Vector3(-side, 0, 0));
                }
                Matrix4x4 toFront = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 180f, 0), Vector3.one);
                result.Blocks.Append(tunnel, toFront);
                result.Interior.Append(inner, toFront);
            }
            return result;
        }

        static Vector3 Sph(float phi, float th) => new Vector3(Mathf.Cos(phi) * Mathf.Sin(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Cos(th));

        // ------------------------------------------------------------------
        // 빙하 / 절벽: 불규칙한 바닥 다각형을 위로 밀어 올리고 윗면을 한쪽으로 기울임
        // ------------------------------------------------------------------
        public static MeshBuilder Glacier(float width, float depth, float height, float slope, System.Random rng, int points = 10)
        {
            var mb = new MeshBuilder();
            float[] levels = { 0f, 0.4f, 0.75f, 1f };
            float[] shrink = { 1f, 0.96f, 0.9f, 0.8f };
            var foot = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float a = (i + Range(rng, -0.3f, 0.3f)) / points * Mathf.PI * 2f;
                float j = Range(rng, 0.78f, 1.08f);
                foot[i] = new Vector2(Mathf.Cos(a) * width * 0.5f * j, Mathf.Sin(a) * depth * 0.5f * j);
            }
            var ringVerts = new Vector3[levels.Length, points];
            for (int l = 0; l < levels.Length; l++)
                for (int i = 0; i < points; i++)
                {
                    Vector2 p = foot[i] * shrink[l];
                    float xn = Mathf.Clamp01(foot[i].x / width + 0.5f);
                    float topH = height * Mathf.Lerp(1f - slope, 1f, xn) * Range(rng, 0.92f, 1.04f);
                    float y = levels[l] * topH;
                    float jit = l == 0 ? 0f : width * 0.03f;
                    ringVerts[l, i] = new Vector3(p.x + Range(rng, -jit, jit), y, p.y + Range(rng, -jit, jit));
                }
            for (int l = 0; l < levels.Length - 1; l++)
                for (int i = 0; i < points; i++)
                {
                    int n = (i + 1) % points;
                    Vector3 a = ringVerts[l, i], b = ringVerts[l, n], c = ringVerts[l + 1, n], d = ringVerts[l + 1, i];
                    Vector3 cen = (a + b + c + d) * 0.25f;
                    Vector3 outDir = new Vector3(cen.x, 0, cen.z);
                    // 사각형을 대각선으로 나눠 면이 더 깨진 느낌
                    if ((i + l) % 2 == 0) { mb.TriDir(a, b, c, outDir); mb.TriDir(a, c, d, outDir); }
                    else { mb.TriDir(a, b, d, outDir); mb.TriDir(b, c, d, outDir); }
                }
            // 윗면
            int top = levels.Length - 1;
            Vector3 tc = Vector3.zero;
            for (int i = 0; i < points; i++) tc += ringVerts[top, i];
            tc /= points;
            tc.y += height * 0.03f;
            for (int i = 0; i < points; i++)
                mb.TriDir(tc, ringVerts[top, i], ringVerts[top, (i + 1) % points], Vector3.up);
            return mb;
        }

        /// <summary>바다 위 얇은 유빙 판.</summary>
        public static MeshBuilder IceFloe(float width, float depth, float thickness, System.Random rng)
        {
            var mb = new MeshBuilder();
            int points = 7;
            var top = new Vector3[points];
            var bot = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = (i + Range(rng, -0.3f, 0.3f)) / points * Mathf.PI * 2f;
                float j = Range(rng, 0.8f, 1.1f);
                Vector3 p = new Vector3(Mathf.Cos(a) * width * 0.5f * j, 0, Mathf.Sin(a) * depth * 0.5f * j);
                top[i] = p + Vector3.up * thickness;
                bot[i] = p - Vector3.up * thickness;
            }
            Vector3 c = Vector3.up * thickness;
            for (int i = 0; i < points; i++)
            {
                int n = (i + 1) % points;
                mb.TriDir(c, top[i], top[n], Vector3.up);
                Vector3 mid = (top[i] + top[n]) * 0.5f;
                mb.QuadDir(bot[i], bot[n], top[n], top[i], new Vector3(mid.x, 0, mid.z));
            }
            return mb;
        }

        /// <summary>울퉁불퉁한 눈/얼음 덩어리.</summary>
        public static MeshBuilder Rock(float radius, float flatten, System.Random rng)
        {
            var mb = new MeshBuilder();
            int lon = 7, lat = 4;
            var v = new Vector3[lat + 1, lon];
            for (int a = 0; a <= lat; a++)
                for (int b = 0; b < lon; b++)
                {
                    float phi = Mathf.Lerp(-0.15f, Mathf.PI * 0.5f, (float)a / lat);
                    float th = (b + (a % 2) * 0.5f) * Mathf.PI * 2f / lon;
                    float r = radius * (a == lat ? 0.05f : Range(rng, 0.8f, 1.1f));
                    var p = Sph(phi, th) * r;
                    p.y *= flatten;
                    v[a, b] = p;
                }
            Vector3 center = new Vector3(0, radius * flatten * 0.3f, 0);
            for (int a = 0; a < lat; a++)
                for (int b = 0; b < lon; b++)
                {
                    int n = (b + 1) % lon;
                    mb.TriOut(v[a, b], v[a, n], v[a + 1, b], center);
                    mb.TriOut(v[a, n], v[a + 1, n], v[a + 1, b], center);
                }
            return mb;
        }

        /// <summary>약간 기복이 있는 눈밭 지면 (플랫 셰이딩으로 면이 보임).</summary>
        public static MeshBuilder Ground(float sizeX, float sizeZ, int cells, float bump, System.Random rng)
        {
            var mb = new MeshBuilder();
            var h = new Vector3[cells + 1, cells + 1];
            float cx = sizeX / cells, cz = sizeZ / cells;
            for (int x = 0; x <= cells; x++)
                for (int z = 0; z <= cells; z++)
                {
                    bool edge = x == 0 || z == 0 || x == cells || z == cells;
                    float jx = edge ? 0 : Range(rng, -0.3f, 0.3f) * cx;
                    float jz = edge ? 0 : Range(rng, -0.3f, 0.3f) * cz;
                    h[x, z] = new Vector3(-sizeX * 0.5f + x * cx + jx, Range(rng, -bump, bump), -sizeZ * 0.5f + z * cz + jz);
                }
            for (int x = 0; x < cells; x++)
                for (int z = 0; z < cells; z++)
                {
                    if ((x + z) % 2 == 0)
                    {
                        mb.TriDir(h[x, z], h[x + 1, z], h[x + 1, z + 1], Vector3.up);
                        mb.TriDir(h[x, z], h[x + 1, z + 1], h[x, z + 1], Vector3.up);
                    }
                    else
                    {
                        mb.TriDir(h[x, z], h[x + 1, z], h[x, z + 1], Vector3.up);
                        mb.TriDir(h[x + 1, z], h[x + 1, z + 1], h[x, z + 1], Vector3.up);
                    }
                }
            return mb;
        }
    }
}
