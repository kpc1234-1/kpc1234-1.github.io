using UnityEngine;

namespace IglooLobby
{
    /// <summary>로비 카메라를 아주 천천히 흔들어 정지 화면 느낌을 줄인다.</summary>
    public class LobbyCameraDrift : MonoBehaviour
    {
        public float positionAmount = 0.15f;
        public float rotationAmount = 0.6f;
        public float speed = 0.12f;

        Vector3 basePos;
        Quaternion baseRot;

        void Start()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
        }

        void LateUpdate()
        {
            float t = Time.time * speed;
            transform.position = basePos + new Vector3(Mathf.Sin(t) * positionAmount, Mathf.Sin(t * 0.7f) * positionAmount * 0.3f, 0);
            transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.8f) * rotationAmount * 0.3f, Mathf.Sin(t * 0.5f) * rotationAmount, 0);
        }
    }
}
