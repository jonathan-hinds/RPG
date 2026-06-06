using System;
using UnityEngine;

namespace RPGClone.UI
{
    public sealed class MMONpcPanelDistanceCloser : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float closeDistance = 6f;

        private Transform npc;
        private Transform player;
        private Action onClose;
        private bool closing;

        public void Track(Transform npcTransform, Transform playerTransform, float distance, Action closeAction)
        {
            npc = npcTransform;
            player = playerTransform;
            closeDistance = Mathf.Max(0f, distance);
            onClose = closeAction;
            closing = false;
        }

        private void OnDisable()
        {
            closing = false;
        }

        private void Update()
        {
            if (closing || npc == null || player == null)
            {
                Close();
                return;
            }

            if ((player.position - npc.position).sqrMagnitude > closeDistance * closeDistance)
            {
                Close();
            }
        }

        private void Close()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            onClose?.Invoke();
        }
    }
}
