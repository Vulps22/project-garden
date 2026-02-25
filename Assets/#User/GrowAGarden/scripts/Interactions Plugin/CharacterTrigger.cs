using System;
using UnityEngine;

namespace GrowAGarden
{
    public class CharacterTrigger : MonoBehaviour
    {
        public event Action<CharacterController> OnCharacterEnter;
        public event Action<CharacterController> OnCharacterExit;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out CharacterController character))
            {
                //if (character.transform.root.name.StartsWith("Original"))
                {
                    Debug.Log($"[{nameof(CharacterTrigger)}] OnTriggerEnter: {other.name}");
                    OnCharacterEnter?.Invoke(character);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out CharacterController character))
            {
                //if (character.transform.root.name.StartsWith("Original"))
                {
                    Debug.Log($"[{nameof(CharacterTrigger)}] OnTriggerExit: {other.name}");
                    OnCharacterExit?.Invoke(character);
                }
            }
        }
    }
}
