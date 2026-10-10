using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>Constant slow rotation for background decoration.</summary>
    public class UISpin : MonoBehaviour
    {
        public float degreesPerSecond = 4f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
