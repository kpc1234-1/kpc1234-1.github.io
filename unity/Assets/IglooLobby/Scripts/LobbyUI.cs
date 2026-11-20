using UnityEngine;

namespace IglooLobby
{
    /// <summary>
    /// 간단한 로비 UI (OnGUI). 게임 타이틀, 망치 선택 화살표, 시작 버튼.
    /// 정식 UI(uGUI/UI Toolkit)를 만들기 전까지 테스트용으로 쓰고, 버튼 로직은 HammerSelector 를 그대로 호출하면 된다.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        public HammerSelector hammerSelector;
        public string title = "IGLOO";
        [Tooltip("시작 버튼을 누르면 불러올 씬 이름 (Build Settings 에 등록 필요). 비워두면 로그만 출력.")]
        public string gameSceneName = "";

        GUIStyle titleStyle, buttonStyle, labelStyle;

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.25f, 0.42f, 0.62f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.32f, 0.48f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            EnsureStyles();
            float s = Screen.height / 720f;
            titleStyle.fontSize = Mathf.RoundToInt(64 * s);
            labelStyle.fontSize = Mathf.RoundToInt(22 * s);
            buttonStyle.fontSize = Mathf.RoundToInt(26 * s);

            GUI.Label(new Rect(0, 30 * s, Screen.width, 90 * s), title, titleStyle);

            if (hammerSelector != null && hammerSelector.Count > 0)
            {
                float y = Screen.height - 170 * s;
                float cx = Screen.width * 0.5f;
                if (GUI.Button(new Rect(cx - 210 * s, y, 60 * s, 60 * s), "<", buttonStyle)) hammerSelector.Prev();
                string name = hammerSelector.CurrentHammer != null ? hammerSelector.CurrentHammer.name : "-";
                GUI.Label(new Rect(cx - 140 * s, y, 280 * s, 60 * s), name + "  (" + (hammerSelector.currentIndex + 1) + "/" + hammerSelector.Count + ")", labelStyle);
                if (GUI.Button(new Rect(cx + 150 * s, y, 60 * s, 60 * s), ">", buttonStyle)) hammerSelector.Next();
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 120 * s, Screen.height - 95 * s, 240 * s, 64 * s), "START", buttonStyle))
            {
                if (!string.IsNullOrEmpty(gameSceneName)) UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
                else Debug.Log("[LobbyUI] START 눌림 - gameSceneName 을 지정하세요.");
            }
        }
    }
}
