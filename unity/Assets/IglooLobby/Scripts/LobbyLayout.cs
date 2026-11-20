using System.Collections.Generic;
using UnityEngine;

namespace IglooLobby
{
    /// <summary>
    /// 로비 씬 배치값(카메라/이글루/캐릭터/빙하/소품 위치)과 로우폴리 메시 생성을 한 곳에 모아둔 곳.
    /// 위치를 바꾸고 싶으면 여기 숫자를 고친 뒤 Lobby Builder 에서 다시 생성하면 된다.
    /// 좌표: 카메라가 -Z 쪽에서 +Z 방향을 바라봄. X 오른쪽, Y 위.
    /// </summary>
    public static class LobbyLayout
    {
        public static readonly Vector3 CameraPosition = new Vector3(0f, 2.6f, -11f);
        public static readonly Vector3 CameraEuler = new Vector3(7f, 0f, 0f);
        public const float CameraFov = 40f;

        public static readonly Vector3 SunEuler = new Vector3(36f, 52f, 0f); // 왼쪽 앞에서 비춰 그림자가 오른쪽 뒤로

        public static readonly Vector3 IglooPosition = new Vector3(2.0f, 0f, 0f);
        public const float IglooYaw = 28f;            // 입구가 카메라 왼쪽 앞을 보도록
        public const float IglooRadius = 2.2f;

        public static readonly Vector3 CharacterPosition = new Vector3(-0.9f, 0f, -4.6f);
        public const float CharacterYaw = 165f;       // 카메라를 바라보도록

        public struct GlacierSpot
        {
            public Vector3 position;
            public Vector3 size;    // 폭, 깊이, 높이
            public float slope;     // 윗면 기울기 (음수면 왼쪽이 높음)
            public float yaw;
            public GlacierSpot(Vector3 p, Vector3 s, float sl, float y) { position = p; size = s; slope = sl; yaw = y; }
        }

        public static readonly GlacierSpot[] Glaciers =
        {
            new GlacierSpot(new Vector3(-46f, 0f, 92f), new Vector3(58f, 20f, 19f), 0.4f, 4f),    // 왼쪽 큰 절벽
            new GlacierSpot(new Vector3(-20f, 0f, 84f), new Vector3(14f, 9f, 4f), 0.35f, 12f),    // 왼쪽 절벽 앞 낮은 덩어리
            new GlacierSpot(new Vector3(58f, 0f, 100f), new Vector3(46f, 20f, 18f), 0.3f, -6f),   // 오른쪽 절벽
            new GlacierSpot(new Vector3(26f, 0f, 104f), new Vector3(14f, 10f, 12f), 0.35f, 20f),  // 가운데 뾰족한 빙산
            new GlacierSpot(new Vector3(36f, 0f, 92f), new Vector3(18f, 10f, 8f), 0.25f, -12f),   // 오른쪽 낮은 빙산
        };

        public enum Mat { Snow, Ice, Interior, Glacier, Floe, Rock, Far }

        public class Piece
        {
            public string name;
            public Mat material;
            public LowPoly.MeshBuilder mesh;   // 월드 좌표
            public bool castShadows = true;
        }

        static float R(System.Random r, float min, float max) => min + (float)r.NextDouble() * (max - min);

        /// <summary>씬 메시를 만든다. includeIgloo/includeGlaciers 를 끄면 사용자 모델을 대신 배치할 수 있다.</summary>
        public static List<Piece> Build(int seed, bool includeIgloo, bool includeGlaciers)
        {
            var rng = new System.Random(seed);
            var list = new List<Piece>();

            var ground = new LowPoly.MeshBuilder();
            ground.Append(LowPoly.Ground(320f, 320f, 64, 0.06f, rng), Matrix4x4.TRS(new Vector3(0, 0, 120f), Quaternion.identity, Vector3.one));
            list.Add(new Piece { name = "Ground", material = Mat.Snow, mesh = ground, castShadows = false });

            // 이글루 (시드와 무관하게 항상 같은 순서로 난수를 쓰도록 먼저 생성)
            var igloo = LowPoly.Igloo(IglooRadius, rng);
            if (includeIgloo)
            {
                var m = Matrix4x4.TRS(IglooPosition, Quaternion.Euler(0, IglooYaw, 0), Vector3.one);
                var blocks = new LowPoly.MeshBuilder(); blocks.Append(igloo.Blocks, m);
                var inner = new LowPoly.MeshBuilder(); inner.Append(igloo.Interior, m);
                list.Add(new Piece { name = "Igloo_Blocks", material = Mat.Ice, mesh = blocks });
                list.Add(new Piece { name = "Igloo_Interior", material = Mat.Interior, mesh = inner });
            }

            // 얼음 큐브: 왼쪽에 3개 쌓인 것 + 이글루 오른쪽 앞 1개
            var cubes = new LowPoly.MeshBuilder();
            void Cube(float s, Vector3 p, float yaw) => cubes.Append(LowPoly.IceCube(s, rng), Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one));
            Cube(0.62f, new Vector3(-3.55f, 0, -3.0f), 6f);
            Cube(0.58f, new Vector3(-2.85f, 0, -3.15f), -10f);
            Cube(0.56f, new Vector3(-3.35f, 0.62f, -3.05f), 14f);
            Cube(0.62f, new Vector3(3.55f, 0, -3.3f), 24f);
            list.Add(new Piece { name = "IceCubes", material = Mat.Ice, mesh = cubes });

            // 눈 더미 + 이글루 앞 얼음 파편
            var rocks = new LowPoly.MeshBuilder();
            rocks.Append(LowPoly.Rock(0.95f, 0.8f, rng), Matrix4x4.TRS(new Vector3(-1.75f, 0, -2.2f), Quaternion.Euler(0, 20, 0), Vector3.one));
            rocks.Append(LowPoly.Rock(0.5f, 0.7f, rng), Matrix4x4.TRS(new Vector3(-0.95f, 0, -2.5f), Quaternion.Euler(0, 70, 0), Vector3.one));
            rocks.Append(LowPoly.Rock(0.3f, 0.6f, rng), Matrix4x4.TRS(new Vector3(-1.3f, 0, -3.0f), Quaternion.Euler(0, 140, 0), Vector3.one));
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(R(rng, 0.6f, 3.2f), 0, R(rng, -5.0f, -4.2f));
                rocks.Append(LowPoly.IceFloe(R(rng, 0.25f, 0.6f), R(rng, 0.18f, 0.38f), 0.025f, rng),
                    Matrix4x4.TRS(p, Quaternion.Euler(0, R(rng, 0, 360), 0), Vector3.one));
            }
            list.Add(new Piece { name = "SnowPiles", material = Mat.Rock, mesh = rocks });

            // 유빙 (중간 거리의 얇은 얼음판)
            var floes = new LowPoly.MeshBuilder();
            Vector3[] floePos =
            {
                new Vector3(-18, 0, 16), new Vector3(-8, 0, 22), new Vector3(4, 0, 30), new Vector3(16, 0, 18),
                new Vector3(-26, 0, 34), new Vector3(14, 0, 42), new Vector3(-6, 0, 48), new Vector3(28, 0, 30),
                new Vector3(-36, 0, 58), new Vector3(30, 0, 62),
            };
            foreach (var p in floePos)
                floes.Append(LowPoly.IceFloe(R(rng, 8f, 16f), R(rng, 2.5f, 4.5f), 0.12f, rng),
                    Matrix4x4.TRS(p, Quaternion.Euler(0, R(rng, -8f, 8f), 0), Vector3.one));
            list.Add(new Piece { name = "IceFloes", material = Mat.Floe, mesh = floes, castShadows = false });

            if (includeGlaciers)
            {
                var gm = new LowPoly.MeshBuilder();
                foreach (var g in Glaciers)
                {
                    var mesh = LowPoly.Glacier(g.size.x, g.size.y, g.size.z, Mathf.Abs(g.slope), rng);
                    Vector3 scale = g.slope < 0 ? new Vector3(-1, 1, 1) : Vector3.one;
                    gm.Append(mesh, Matrix4x4.TRS(g.position, Quaternion.Euler(0, g.yaw, 0), scale));
                }
                list.Add(new Piece { name = "Glaciers", material = Mat.Glacier, mesh = gm });
            }

            // 먼 산 (안개로 흐려짐)
            var far = new LowPoly.MeshBuilder();
            for (int i = 0; i < 11; i++)
            {
                float x = -170f + i * 34f + R(rng, 0, 10);
                far.Append(LowPoly.Glacier(R(rng, 40f, 70f), 18f, R(rng, 5f, 11f), 0.6f, rng, 6),
                    Matrix4x4.TRS(new Vector3(x, -0.5f, R(rng, 190f, 215f)), Quaternion.Euler(0, R(rng, -15f, 15f), 0), Vector3.one));
            }
            list.Add(new Piece { name = "FarMountains", material = Mat.Far, mesh = far, castShadows = false });
            return list;
        }
    }
}
