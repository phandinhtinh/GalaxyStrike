using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyStrike.UI.Home
{
    public class FloatingEffect : MonoBehaviour
    {
        [Header("--- Floating Settings ---")]
        [SerializeField] private float speed = 2f;       // Tốc độ nhấp nhô
        [SerializeField] private float amplitude = 15f;  // Độ cao nhấp nhô (pixels)

        private Vector3 startPos;

        private void Start()
        {
            startPos = transform.localPosition;
        }

        private void Update()
        {
            float newY = startPos.y + Mathf.Sin(Time.time * speed) * amplitude;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        }
    }
}