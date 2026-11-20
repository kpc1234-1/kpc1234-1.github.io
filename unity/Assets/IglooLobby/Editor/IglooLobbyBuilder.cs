using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IglooLobby.EditorTools
{
    /// <summary>
    /// 메뉴: Tools > Igloo > Lobby Builder
    /// 참고 이미지(하늘색 로우폴리 빙하 + 블록 이글루) 분위기의 로비 씬을 자동으로 만든다.
    /// 캐릭터/이글루/빙하/망치 모델을 넣으면 그 모델을 쓰고, 비워두면 코드로 만든 로우폴리 모델을 쓴다.
    /// </summary>
    public class IglooLobbyBuilder : EditorWindow
    {
        const string Root = "Assets/IglooLobby";
        const string GenFolder = Root + "/Generated";
        const string ScenePath = Root + "/Lobby.unity";

        GameObject characterPrefab;
        GameObject iglooPrefab;
        GameObject glacierPrefab;
        GameObject[] hammerPrefabs = new GameObject[4];
        float characterScale = 1f;
        float iglooScale = 1f;
        float glacierScale = 1f;
        int seed = 7;
        Vector2 scroll;

        [MenuItem("Tools/Igloo/Lobby Builder")]
        static void Open()
        {
            var w = GetWindow<IglooLobbyBuilder>("Igloo Lobby");
            w.minSize = new Vector2(360, 420);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("모델을 비워두면 코드로 만든 로우폴리 모델(참고 이미지 스타일)이 들어갑니다.\n버튼을 누르면 " + ScenePath + " 씬이 새로 만들어집니다.", MessageType.Info);

            EditorGUILayout.LabelField("모델", EditorStyles.boldLabel);
            characterPrefab = (GameObject)EditorGUILayout.ObjectField("캐릭터", characterPrefab, typeof(GameObject), false);
            characterScale = EditorGUILayout.FloatField("  캐릭터 크기", characterScale);
            iglooPrefab = (GameObject)EditorGUILayout.ObjectField("이글루", iglooPrefab, typeof(GameObject), false);
            iglooScale = EditorGUILayout.FloatField("  이글루 크기", iglooScale);
            glacierPrefab = (GameObject)EditorGUILayout.ObjectField("빙하", glacierPrefab, typeof(GameObject), false);
            glacierScale = EditorGUILayout.FloatField("  빙하 크기", glacierScale);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("망치 (각각 분리된 프리팹)", EditorStyles.boldLabel);
            for (int i = 0; i < hammerPrefabs.Length; i++)
                hammerPrefabs[i] = (GameObject)EditorGUILayout.ObjectField("망치 " + (i + 1), hammerPrefabs[i], typeof(GameObject), false);
            EditorGUILayout.HelpBox("망치 4개가 한 파일에 붙어 있다면: Project 창에서 그 모델을 선택하고\nTools > Igloo > Split Selected Model Into Prefabs 를 먼저 실행하세요.", MessageType.None);

            EditorGUILayout.Space();
            seed = EditorGUILayout.IntField("랜덤 시드", seed);

            EditorGUILayout.Space();
            if (GUILayout.Button("로비 씬 생성", GUILayout.Height(36)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
            }
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        void Build()
        {
            EnsureFolder(GenFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 재질
            var mats = new Dictionary<LobbyLayout.Mat, Material>
            {
                { LobbyLayout.Mat.Snow, MakeMat("Snow", new Color(0.94f, 0.97f, 1.00f), 0.15f) },
                { LobbyLayout.Mat.Ice, MakeMat("IceBlock", new Color(0.82f, 0.90f, 0.98f), 0.25f) },
                { LobbyLayout.Mat.Interior, MakeMat("IglooInterior", new Color(0.40f, 0.52f, 0.68f), 0f) },
                { LobbyLayout.Mat.Glacier, MakeMat("Glacier", new Color(0.78f, 0.87f, 0.97f), 0.2f) },
                { LobbyLayout.Mat.Floe, MakeMat("IceFloe", new Color(0.90f, 0.94f, 0.99f), 0.3f) },
                { LobbyLayout.Mat.Rock, MakeMat("SnowRock", new Color(0.92f, 0.95f, 1.00f), 0.1f) },
                { LobbyLayout.Mat.Far, MakeMat("FarMountain", new Color(0.76f, 0.84f, 0.93f), 0f) },
            };

            SetupLighting();

            // 카메라
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = LobbyLayout.CameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGO.transform.position = LobbyLayout.CameraPosition;
            camGO.transform.rotation = Quaternion.Euler(LobbyLayout.CameraEuler);
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<LobbyCameraDrift>();

            // 로우폴리 환경 메시
            var env = new GameObject("Environment").transform;
            foreach (var piece in LobbyLayout.Build(seed, iglooPrefab == null, glacierPrefab == null))
                Spawn(piece.name, piece.mesh, mats[piece.material], env, piece.castShadows);

            // 사용자 이글루 모델
            if (iglooPrefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(iglooPrefab);
                go.transform.SetParent(env, false);
                go.transform.SetPositionAndRotation(LobbyLayout.IglooPosition, Quaternion.Euler(0, LobbyLayout.IglooYaw, 0));
                go.transform.localScale *= iglooScale;
            }

            // 사용자 빙하 모델 (빙하 자리마다 하나씩, 높이에 비례해 크기 조절)
            if (glacierPrefab != null)
            {
                var glacierRoot = new GameObject("Glaciers").transform;
                glacierRoot.SetParent(env, false);
                foreach (var g in LobbyLayout.Glaciers)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(glacierPrefab);
                    go.transform.SetParent(glacierRoot, false);
                    go.transform.SetPositionAndRotation(g.position, Quaternion.Euler(0, g.yaw + (g.slope < 0 ? 180f : 0f), 0));
                    go.transform.localScale = Vector3.one * glacierScale * (g.size.z / 10f);
                }
            }

            // 캐릭터 + 망치
            var lobby = new GameObject("Lobby");
            var ui = lobby.AddComponent<LobbyUI>();
            Vector3 charPos = LobbyLayout.CharacterPosition;
            Quaternion charRot = Quaternion.Euler(0f, LobbyLayout.CharacterYaw, 0f);
            GameObject character;
            if (characterPrefab != null)
            {
                character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                character.transform.SetPositionAndRotation(charPos, charRot);
                character.transform.localScale *= characterScale;
            }
            else
            {
                character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                character.name = "Character_Placeholder (캐릭터 모델로 교체)";
                character.transform.SetPositionAndRotation(charPos + Vector3.up, charRot);
                Object.DestroyImmediate(character.GetComponent<Collider>());
                character.GetComponent<MeshRenderer>().sharedMaterial = MakeMat("Placeholder", new Color(1f, 0.6f, 0.3f), 0.3f);
                var hand = new GameObject("RightHand").transform;
                hand.SetParent(character.transform, false);
                hand.localPosition = new Vector3(0.6f, 0f, 0f);
            }
            var selector = character.GetComponent<HammerSelector>();
            if (selector == null) selector = character.AddComponent<HammerSelector>();
            selector.hammerPrefabs = (GameObject[])hammerPrefabs.Clone();
            ui.hammerSelector = selector;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = character;
            Debug.Log("[Igloo] 로비 씬 생성 완료: " + ScenePath);
        }

        // ------------------------------------------------------------------
        static void SetupLighting()
        {
            // 하늘 (그라데이션 + 옅은 구름) 텍스처를 만들어 Panoramic 스카이박스로 사용
            var sky = MakeSkyTexture();
            var skyShader = Shader.Find("Skybox/Panoramic");
            if (skyShader != null)
            {
                var skyMat = new Material(skyShader) { name = "LobbySky" };
                skyMat.SetTexture("_MainTex", sky);
                skyMat.SetFloat("_Mapping", 1f);
                skyMat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
                skyMat.SetFloat("_ImageType", 0f);
                skyMat.SetFloat("_Exposure", 1f);
                SaveAsset(skyMat, "LobbySky.mat");
                RenderSettings.skybox = skyMat;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.82f, 0.93f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.72f, 0.86f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.84f, 0.90f, 0.97f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 260f;

            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sunGO.transform.rotation = Quaternion.Euler(LobbyLayout.SunEuler);
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        static Texture2D MakeSkyTexture()
        {
            const int W = 1024, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            Color horizon = new Color(0.88f, 0.92f, 0.96f);
            Color zenith = new Color(0.56f, 0.74f, 0.91f);
            Color ground = new Color(0.86f, 0.91f, 0.97f);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float elev = (v - 0.5f) * 2f; // -1..1
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W;
                    Color c;
                    if (elev <= 0f) c = ground;
                    else
                    {
                        float t = Mathf.Pow(elev, 0.55f);
                        c = Color.Lerp(horizon, zenith, t);
                        // 가로로 길게 늘어진 옅은 구름
                        float n = Mathf.PerlinNoise(u * 9f + 13.1f, elev * 26f + 3.7f) * 0.7f
                                + Mathf.PerlinNoise(u * 23f + 5.3f, elev * 60f + 1.1f) * 0.3f;
                        float band = Mathf.Clamp01(1f - Mathf.Abs(elev - 0.28f) / 0.26f);
                        float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.72f, n)) * band;
                        c = Color.Lerp(c, new Color(0.95f, 0.97f, 1f), cloud * 0.55f);
                    }
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            string path = GenFolder + "/LobbySky.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------
        static GameObject Spawn(string name, LowPoly.MeshBuilder mb, Material mat, Transform parent, bool castShadows)
        {
            var mesh = mb.ToMesh(name);
            SaveAsset(mesh, name + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static Material MakeMat(string name, Color color, float smoothness)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader sh = null;
            if (srp)
            {
                sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("HDRP/Lit");
            }
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights"))
            {
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            return (Material)SaveAsset(m, name + ".mat");
        }

        static Object SaveAsset(Object obj, string fileName)
        {
            string path = GenFolder + "/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
