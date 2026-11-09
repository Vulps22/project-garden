using System.Collections;
using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>One piece of the tutorial platform that comes in from smoke and goes out in smoke.</summary>
    public class TutorialStage : MonoBehaviour
    {
        private const float GROW_SECONDS = 0.35f;
        private const float SHRINK_SECONDS = 0.25f;

        [SerializeField] private SmokePuff _smoke;

        private Vector3 _scale;
        private bool _scaleKnown;
        private Coroutine _running;

        private void Awake() => RememberScale();

        /// <summary>The smoke it comes and goes in, for stages built at runtime.</summary>
        public void UseSmoke(SmokePuff smoke) => _smoke = smoke;

        private void RememberScale()
        {
            if (_scaleKnown) return;
            _scale = transform.localScale;
            _scaleKnown = true;
        }

        /// <summary>Comes in from smoke.</summary>
        public void Appear()
        {
            RememberScale();
            if (_running != null) StopCoroutine(_running);
            gameObject.SetActive(true);
            transform.localScale = _scale;
            Puff();
            transform.localScale = _scale * 0.01f;
            _running = StartCoroutine(Scale(0.01f, 1f, GROW_SECONDS, false));
        }

        /// <summary>Goes out in smoke.</summary>
        public void Vanish()
        {
            if (!gameObject.activeInHierarchy) return;
            RememberScale();
            if (_running != null) StopCoroutine(_running);
            Puff();
            _running = StartCoroutine(Scale(1f, 0.01f, SHRINK_SECONDS, true));
        }

        /// <summary>Gone at once, no smoke.</summary>
        public void Hide()
        {
            RememberScale();
            if (_running != null) { StopCoroutine(_running); _running = null; }
            transform.localScale = _scale;
            gameObject.SetActive(false);
        }

        private void Puff()
        {
            if (_smoke == null) return;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { _smoke.Play(new Bounds(transform.position, Vector3.one)); return; }
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            _smoke.Play(any ? bounds : new Bounds(transform.position, Vector3.one));
        }

        private IEnumerator Scale(float from, float to, float seconds, bool hideAfter)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(from, to, t / seconds);
                transform.localScale = _scale * k;
                yield return null;
            }
            transform.localScale = _scale * to;
            _running = null;
            if (hideAfter) Hide();
        }
    }
}
