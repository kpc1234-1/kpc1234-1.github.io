using UnityEngine;

namespace IglooLobby
{
    /// <summary>
    /// 캐릭터 손에 망치 4종 중 하나를 들려주는 컴포넌트.
    /// 망치는 각각 별도 프리팹으로 넣고, Next/Prev/Equip 으로 바꾼다. 선택은 PlayerPrefs 에 저장된다.
    /// </summary>
    public class HammerSelector : MonoBehaviour
    {
        [Tooltip("망치가 붙을 손 위치. 비워두면 휴머노이드 오른손 본 또는 이름에 'hand' 가 들어간 본을 자동으로 찾는다.")]
        public Transform handSocket;

        [Tooltip("망치 프리팹 (각각 분리된 4종)")]
        public GameObject[] hammerPrefabs = new GameObject[4];

        [Tooltip("손 기준 망치 위치/회전/크기 보정값 (모델마다 다르니 플레이 중 조정 후 값을 복사해 두세요)")]
        public Vector3 gripPosition = Vector3.zero;
        public Vector3 gripRotation = Vector3.zero;
        public float gripScale = 1f;

        public int currentIndex;
        public bool rememberSelection = true;

        public event System.Action<int> HammerChanged;

        const string PrefKey = "IglooLobby.SelectedHammer";
        GameObject current;

        public GameObject CurrentHammer => current;
        public int Count => hammerPrefabs == null ? 0 : hammerPrefabs.Length;

        void Awake()
        {
            if (handSocket == null) handSocket = FindHand();
        }

        void Start()
        {
            if (rememberSelection) currentIndex = PlayerPrefs.GetInt(PrefKey, currentIndex);
            Equip(currentIndex);
        }

        public void Next() => Equip(currentIndex + 1);
        public void Prev() => Equip(currentIndex - 1);

        public void Equip(int index)
        {
            if (Count == 0) return;
            index = ((index % Count) + Count) % Count;
            // 비어 있는 슬롯은 건너뜀
            for (int tries = 0; tries < Count && hammerPrefabs[index] == null; tries++)
                index = (index + 1) % Count;
            if (hammerPrefabs[index] == null) return;

            if (current != null) Destroy(current);
            Transform parent = handSocket != null ? handSocket : transform;
            current = Instantiate(hammerPrefabs[index], parent);
            current.name = hammerPrefabs[index].name;
            ApplyGrip();

            currentIndex = index;
            if (rememberSelection) { PlayerPrefs.SetInt(PrefKey, index); PlayerPrefs.Save(); }
            HammerChanged?.Invoke(index);
        }

        public void ApplyGrip()
        {
            if (current == null) return;
            current.transform.localPosition = gripPosition;
            current.transform.localRotation = Quaternion.Euler(gripRotation);
            current.transform.localScale = Vector3.one * gripScale;
        }

        void OnValidate()
        {
            // 플레이 중 인스펙터에서 그립 값을 바꾸면 바로 반영
            if (Application.isPlaying) ApplyGrip();
        }

        Transform FindHand()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (bone != null) return bone;
            }
            string[] keys = { "righthand", "hand_r", "hand.r", "r_hand", "r hand", "handr", "hand" };
            foreach (var key in keys)
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant().Replace(" ", "");
                    if (n.Contains(key.Replace(" ", ""))) return t;
                }
            return null;
        }
    }
}
