using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GrowAGarden
{
    /// <summary>
    /// Low-poly cloud puffs that spawn, drift on a wind vector, and break apart. Purely decorative
    /// — nothing here is a fact anyone needs to agree on, so unlike everything else in this project
    /// it is deliberately not networked. Every client rolls its own random clouds on its own timer,
    /// and nobody will ever notice theirs don't match.
    ///
    /// Each cloud is a cluster of small icosahedra (12 verts, 20 tris each) built once in code and
    /// shared as one Mesh across every blob in every cloud — the variety comes from per-instance
    /// scale, position and cluster arrangement, not from unique geometry, so there is nothing left
    /// to generate at spawn time. Cloud roots and their blob children are pre-built into a fixed
    /// pool sized by _maxClouds; spawning only toggles objects active and re-randomizes their local
    /// transforms. Nothing is ever Instantiated or Destroyed after OnEnable.
    ///
    /// [ExecuteAlways] plus an EditorApplication.update hook means this also runs outside Play
    /// mode, so the wind and timing can be tuned by watching the Scene view rather than guessing
    /// and hitting Play. Update() drives it in Play mode as normal; the two paths funnel into the
    /// same Tick() so there is only one place the animation is defined.
    ///
    /// Clouds spawn in a ring around <see cref="Anchor"/> — the local player's head, not this
    /// object's own position — so density holds up wherever the player has wandered in an
    /// unbounded world rather than only near wherever this GameObject happens to sit.
    /// </summary>
    [ExecuteAlways]
    public class CloudGenerator : MonoBehaviour
    {
        [Tooltip("Which way clouds drift. Length doesn't matter — only the direction is used.")]
        [SerializeField] private Vector3 _windDirection = Vector3.right;

        [Tooltip("Drift speed in metres/second.")]
        [SerializeField] private float _windSpeed = 1.5f;

        [Tooltip("Clouds spawn at a random point in this full ring around the anchor (see below) — " +
                 "every direction, not just upwind — so the sky reads full no matter which way the " +
                 "player is looking, and no matter where in an unbounded world they are.")]
        [SerializeField] private Vector2 _spawnRadiusRange = new Vector2(40f, 160f);

        [Tooltip("Height range above the anchor that clouds spawn at.")]
        [SerializeField] private Vector2 _spawnHeightRange = new Vector2(10f, 30f);

        [Tooltip("Average seconds between spawns, while under the cloud cap. The real density " +
                 "lever is Max Clouds below — this only controls how fast the pool fills and " +
                 "refills, not how many can be alive at once.")]
        [SerializeField] private float _spawnIntervalSeconds = 0.35f;

        [Tooltip("Hard cap on clouds alive at once. Also the pool size — this many cloud roots " +
                 "are built once and reused forever, never Instantiated per spawn. This is the " +
                 "actual density knob; each cloud costs almost nothing, so there is little reason " +
                 "to keep this low.")]
        [SerializeField] private int _maxClouds = 90;

        [Tooltip("How many blob slots each pooled cloud has. A spawn uses a random count between " +
                 "the two Blobs Per Cloud fields below and leaves the rest inactive.")]
        [SerializeField] private int _maxBlobsPerCloud = 10;

        [Tooltip("Range of blobs actually used per spawn, out of Max Blobs Per Cloud.")]
        [SerializeField] private Vector2Int _blobsPerCloudRange = new Vector2Int(6, 10);

        [Tooltip("Overall size (metres) a cloud's cluster spreads across, rolled per spawn.")]
        [SerializeField] private Vector2 _cloudSizeRange = new Vector2(3f, 7f);

        [Tooltip("Per-blob scale relative to the cloud size, rolled per blob.")]
        [SerializeField] private Vector2 _blobScaleRange = new Vector2(0.35f, 0.6f);

        [Tooltip("Seconds to grow from nothing to full size after spawning.")]
        [SerializeField] private float _fadeInSeconds = 2f;

        [Tooltip("A cloud's lifetime is rolled once, in this range, at spawn.")]
        [SerializeField] private Vector2 _lifetimeRange = new Vector2(5f, 10f);

        [Tooltip("How long the break-up takes, at the end of a cloud's life. Blobs drift apart " +
                 "and the whole cluster shrinks to nothing over this window.")]
        [SerializeField] private float _breakUpSeconds = 1.5f;

        [Tooltip("How far blobs separate from the cluster centre by the end of break-up.")]
        [SerializeField] private float _breakUpSpread = 1.5f;

        [Tooltip("Shared material for every blob. Needs to be an actual Material asset, not " +
                 "created with Shader.Find at runtime — a runtime-only shader reference is not " +
                 "guaranteed to survive Somnium's asset bundle export.")]
        [SerializeField] private Material _cloudMaterial;

        [Tooltip("Run the drift/spawn/break-up simulation in the Editor outside Play mode, so " +
                 "wind and timing can be tuned by watching the Scene view.")]
        [SerializeField] private bool _previewInEditor = true;

        private class CloudSlot
        {
            public Transform Root;
            public Transform[] Blobs;
        }

        private class ActiveCloud
        {
            public CloudSlot Slot;
            public int BlobsUsed;
            public Vector3[] SettledLocalPosition;
            public Vector3[] BreakDirection;
            public float SpawnedAtClock;
            public float Lifetime;
            public float BaseScale;
        }

        private Mesh _blobMesh;
        private readonly Queue<CloudSlot> _pooled = new Queue<CloudSlot>();
        private readonly List<ActiveCloud> _active = new List<ActiveCloud>();
        private float _clock;
        private float _nextSpawnAt;
#if UNITY_EDITOR
        private double _lastEditorTime;
#endif

        private void OnEnable()
        {
            _blobMesh = BuildIcosahedron();
            BuildPool();
            _clock = 0f;
            _nextSpawnAt = _spawnIntervalSeconds;

#if UNITY_EDITOR
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick;
            if (!Application.isPlaying && _previewInEditor) EditorApplication.update += EditorTick;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            DestroyPool();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.update -= EditorTick;
            if (isActiveAndEnabled && !Application.isPlaying && _previewInEditor)
                EditorApplication.update += EditorTick;
        }

        private void EditorTick()
        {
            if (Application.isPlaying || !_previewInEditor) return;
            double now = EditorApplication.timeSinceStartup;
            // Clamped so refocusing after a long idle doesn't age every cloud through its whole
            // life in one jump — the same shape of trap as an uncapped retry timer elsewhere here.
            float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, 0.1f);
            _lastEditorTime = now;
            Tick(dt);
            SceneView.RepaintAll();
        }
#endif

        private void Update()
        {
            // Edit-mode ticking is EditorTick's job, driven off EditorApplication.update rather
            // than Update() — Unity only calls Update() in edit mode on a redraw, not a steady
            // clock, which reads as stuttery rather than drifting.
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            _clock += dt;

            if (_active.Count < _maxClouds && _clock >= _nextSpawnAt)
            {
                Spawn();
                _nextSpawnAt = _clock + _spawnIntervalSeconds * Random.Range(0.6f, 1.4f);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveCloud cloud = _active[i];
                float age = _clock - cloud.SpawnedAtClock;

                if (age >= cloud.Lifetime)
                {
                    Despawn(cloud);
                    continue;
                }

                cloud.Slot.Root.position += WindVelocity() * dt;

                float spawnT = Smoothstep01(Mathf.Clamp01(age / _fadeInSeconds));
                float breakStart = Mathf.Max(cloud.Lifetime - _breakUpSeconds, _fadeInSeconds);
                float breakT = age <= breakStart
                    ? 0f
                    : Smoothstep01(Mathf.Clamp01((age - breakStart) / (cloud.Lifetime - breakStart)));

                cloud.Slot.Root.localScale = Vector3.one * (cloud.BaseScale * spawnT * (1f - breakT));

                for (int b = 0; b < cloud.BlobsUsed; b++)
                {
                    cloud.Slot.Blobs[b].localPosition = Vector3.Lerp(
                        cloud.SettledLocalPosition[b],
                        cloud.SettledLocalPosition[b] + cloud.BreakDirection[b] * _breakUpSpread,
                        breakT);
                }
            }
        }

        private Vector3 WindVelocity()
        {
            Vector3 dir = _windDirection.sqrMagnitude > 0.0001f ? _windDirection.normalized : Vector3.right;
            return dir * _windSpeed;
        }

        /// <summary>
        /// Where clouds spawn around. The local player's head when there is one, so density holds
        /// up "as far as the player can see" no matter where they've wandered in an unbounded
        /// world — a fixed point (this object's own transform) would only ever populate the sky
        /// near world origin. Falls back to this object's position when there is no local player
        /// yet, which is also what makes the edit-mode / no-Play-mode preview work at all.
        /// </summary>
        private Vector3 Anchor()
        {
            Transform head = PlayerManager.LocalPlayerHead;
            return head != null ? head.position : transform.position;
        }

        private void Spawn()
        {
            if (_pooled.Count == 0) return; // at _maxClouds already; the count check above prevents this
            CloudSlot slot = _pooled.Dequeue();

            Vector3 spawnDir = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
            float spawnRadius = Random.Range(_spawnRadiusRange.x, _spawnRadiusRange.y);
            float height = Random.Range(_spawnHeightRange.x, _spawnHeightRange.y);
            slot.Root.position = Anchor() + spawnDir * spawnRadius + Vector3.up * height;
            slot.Root.localScale = Vector3.zero;
            slot.Root.gameObject.SetActive(true);

            int blobsUsed = Random.Range(_blobsPerCloudRange.x, _blobsPerCloudRange.y + 1);
            blobsUsed = Mathf.Clamp(blobsUsed, 1, slot.Blobs.Length);
            float clusterRadius = Random.Range(_cloudSizeRange.x, _cloudSizeRange.y) * 0.5f;

            var settled = new Vector3[blobsUsed];
            var breakDir = new Vector3[blobsUsed];

            for (int b = 0; b < slot.Blobs.Length; b++)
            {
                bool used = b < blobsUsed;
                slot.Blobs[b].gameObject.SetActive(used);
                if (!used) continue;

                float angle = (b / (float)blobsUsed) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float radius = clusterRadius * Random.Range(0.35f, 1f);
                Vector3 local = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Random.Range(-0.15f, 0.15f) * clusterRadius,
                    Mathf.Sin(angle) * radius * 0.6f);

                settled[b] = local;
                breakDir[b] = local.sqrMagnitude > 0.0001f ? local.normalized : Random.onUnitSphere;

                slot.Blobs[b].localPosition = local;
                slot.Blobs[b].localScale = Vector3.one * Random.Range(_blobScaleRange.x, _blobScaleRange.y) * clusterRadius;
            }

            _active.Add(new ActiveCloud
            {
                Slot = slot,
                BlobsUsed = blobsUsed,
                SettledLocalPosition = settled,
                BreakDirection = breakDir,
                SpawnedAtClock = _clock,
                Lifetime = Random.Range(_lifetimeRange.x, _lifetimeRange.y),
                BaseScale = 1f,
            });
        }

        private void Despawn(ActiveCloud cloud)
        {
            cloud.Slot.Root.gameObject.SetActive(false);
            _pooled.Enqueue(cloud.Slot);
            _active.Remove(cloud);
        }

        private static float Smoothstep01(float t) => t * t * (3f - 2f * t);

        // A named container, not a bare list of children. An Editor domain reload (any recompile
        // while this component sits enabled in the scene) resets every plain field back to its
        // default — including _pooled/_active — but does NOT destroy DontSave GameObjects, which
        // are native engine objects rather than managed C# state. Tracking the pool only in fields
        // meant BuildPool() forgot the old batch existed and built a second one on top of it, every
        // single recompile. Finding it by name instead of remembering it sidesteps that entirely —
        // same shape of fix as Socket checking real geometry instead of trusting OnTriggerExit.
        private const string PoolContainerName = "CloudPool";

        private void BuildPool()
        {
            DestroyPool();

            var container = new GameObject(PoolContainerName);
            container.hideFlags = HideFlags.DontSave;
            container.transform.SetParent(transform, false);

            for (int i = 0; i < _maxClouds; i++)
            {
                var root = new GameObject($"Cloud_{i}");
                root.hideFlags = HideFlags.DontSave; // procedural — never belongs in the saved scene
                root.transform.SetParent(container.transform, false);
                root.SetActive(false);

                var blobs = new Transform[_maxBlobsPerCloud];
                for (int b = 0; b < _maxBlobsPerCloud; b++)
                {
                    var blob = new GameObject($"Blob_{b}");
                    blob.hideFlags = HideFlags.DontSave;
                    blob.transform.SetParent(root.transform, false);
                    blob.AddComponent<MeshFilter>().sharedMesh = _blobMesh;
                    var renderer = blob.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = _cloudMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    blob.SetActive(false);
                    blobs[b] = blob.transform;
                }

                _pooled.Enqueue(new CloudSlot { Root = root.transform, Blobs = blobs });
            }
        }

        private void DestroyPool()
        {
            // Destroys everything under the container regardless of which generation created it —
            // including a batch orphaned by a prior domain reload that _pooled/_active never knew
            // about this time around.
            Transform existing = transform.Find(PoolContainerName);
            if (existing != null) DestroyImmediateOrRuntime(existing.gameObject);
            _pooled.Clear();
            _active.Clear();
        }

        private static void DestroyImmediateOrRuntime(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        /// <summary>A 12-vertex, 20-face icosahedron — the low-poly blob every cloud is built from.</summary>
        private static Mesh BuildIcosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            Vector3[] vertices =
            {
                new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
            };
            for (int i = 0; i < vertices.Length; i++) vertices[i] = vertices[i].normalized * 0.5f;

            int[] triangles =
            {
                0,11,5,  0,5,1,  0,1,7,  0,7,10,  0,10,11,
                1,5,9,   5,11,4, 11,10,2, 10,7,6,  7,1,8,
                3,9,4,   3,4,2,  3,2,6,  3,6,8,   3,8,9,
                4,9,5,   2,4,11, 6,2,10, 8,6,7,   9,8,1,
            };

            var mesh = new Mesh { name = "CloudBlob_Icosahedron", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
