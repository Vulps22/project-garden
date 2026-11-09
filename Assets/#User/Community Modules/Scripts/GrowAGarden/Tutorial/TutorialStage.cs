using System.Collections;
using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>One piece of the tutorial platform that dissolves into smoke and builds back out of it, a second each way.</summary>
    public class TutorialStage : MonoBehaviour
    {
        /// <summary>How long a piece takes to dissolve, and to build again (Vulps: a second each way).</summary>
        public const float SECONDS = 1f;
        private const float PUFF_EVERY = 0.15f;

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

        /// <summary>Builds out of smoke over <see cref="SECONDS"/>.</summary>
        public void Appear()
        {
            RememberScale();
            if (_running != null) StopCoroutine(_running);
            gameObject.SetActive(true);
            transform.localScale = _scale;
            Bounds bounds = Extent();
            transform.localScale = _scale * 0.01f;
            _running = StartCoroutine(Run(bounds, 0.01f, 1f, false));
        }

        /// <summary>Dissolves into smoke over <see cref="SECONDS"/>, then is gone.</summary>
        public void Vanish()
        {
            if (!gameObject.activeInHierarchy) return;
            RememberScale();
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(Extent(), transform.localScale.x / Mathf.Max(0.0001f, _scale.x), 0.01f, true));
        }

        /// <summary>Gone at once, no smoke.</summary>
        public void Hide()
        {
            RememberScale();
            if (_running != null) { StopCoroutine(_running); _running = null; }
            transform.localScale = _scale;
            gameObject.SetActive(false);
        }

        /// <summary>The world-space box its renderers fill now.</summary>
        private Bounds Extent()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        /// <summary>Scales between two shares of full size while smoke keeps pouring over the piece.</summary>
        private IEnumerator Run(Bounds bounds, float from, float to, bool hideAfter)
        {
            float nextPuff = 0f;
            for (float t = 0f; t < SECONDS; t += Time.deltaTime)
            {
                if (_smoke != null && t >= nextPuff) { _smoke.Play(bounds); nextPuff += PUFF_EVERY; }
                transform.localScale = _scale * Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / SECONDS));
                yield return null;
            }
            transform.localScale = _scale * to;
            _running = null;
            if (hideAfter) Hide();
        }
    }
}
