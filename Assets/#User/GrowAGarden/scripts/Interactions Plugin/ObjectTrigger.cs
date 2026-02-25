using UnityEngine;
using UnityEngine.Events;

namespace GrowAGarden
{
    public class ObjectTrigger : MonoBehaviour
    {
        public UnityEvent OnEnter;
        public UnityEvent OnExit;
        [SerializeField] private string _objectName = "GameObject";

        private void OnTriggerEnter(Collider other)
        {
            if (other.name == _objectName)
            {
                OnEnter?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.name == _objectName)
            {
                OnExit?.Invoke();
            }
        }
    }
}
