using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.WorldTree
{
    /// <summary>
    /// Lets Yggdrasil's roots hang loose once they let go of the rock: each chain of bones (Root_root_bone) swings as a
    /// rope pinned at the trunk, trailing whatever the trunk does. Off until enabled. Local and cosmetic; nothing is sent.
    /// </summary>
    public class RootDangle : MonoBehaviour
    {
        [SerializeField] private float _gravity = 9.81f;
        [Tooltip("Share of its speed a joint loses each frame: higher is stiffer and calmer.")]
        [SerializeField] private float _damping = 0.03f;
        [SerializeField] private int _iterations = 4;

        private sealed class Chain
        {
            public Transform[] Bones;
            public Vector3[] Axis;          // each bone's own direction to the next joint, in its local space
            public Vector3[] Joint, Previous;
            public float[] Length;
        }

        private readonly List<Chain> _chains = new List<Chain>();
        private bool _built;

        private void Build()
        {
            _built = true;
            var byRoot = new SortedDictionary<int, SortedDictionary<int, Transform>>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string[] parts = t.name.Split('_');
                if (parts.Length != 3 || parts[0] != "Root" || !int.TryParse(parts[1], out int root) || !int.TryParse(parts[2], out int bone)) continue;
                if (!byRoot.TryGetValue(root, out var bones)) byRoot[root] = bones = new SortedDictionary<int, Transform>();
                bones[bone] = t;
            }
            foreach (var bones in byRoot.Values)
            {
                if (bones.Count < 2) continue;
                var chain = new Chain { Bones = new List<Transform>(bones.Values).ToArray() };
                int n = chain.Bones.Length;
                chain.Joint = new Vector3[n + 1];
                chain.Previous = new Vector3[n + 1];
                chain.Length = new float[n];
                chain.Axis = new Vector3[n];
                _chains.Add(chain);
            }
        }

        private void OnEnable()
        {
            if (!_built) Build();
            foreach (Chain c in _chains)
            {
                int n = c.Bones.Length;
                for (int i = 0; i < n; i++) c.Joint[i] = c.Bones[i].position;
                // The tip: the last bone carries on as far as the one before it.
                c.Joint[n] = c.Joint[n - 1] + (c.Joint[n - 1] - c.Joint[n - 2]);
                for (int i = 0; i < n; i++)
                {
                    Vector3 along = c.Joint[i + 1] - c.Joint[i];
                    c.Length[i] = along.magnitude;
                    c.Axis[i] = c.Bones[i].InverseTransformDirection(along.normalized);
                }
                System.Array.Copy(c.Joint, c.Previous, n + 1);
            }
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 fall = Vector3.down * (_gravity * dt * dt);
            foreach (Chain c in _chains)
            {
                int n = c.Bones.Length;
                c.Joint[0] = c.Bones[0].position;   // pinned where it leaves the trunk
                for (int i = 1; i <= n; i++)
                {
                    Vector3 moved = (c.Joint[i] - c.Previous[i]) * (1f - _damping);
                    c.Previous[i] = c.Joint[i];
                    c.Joint[i] += moved + fall;
                }
                for (int k = 0; k < _iterations; k++)
                    for (int i = 1; i <= n; i++)
                        c.Joint[i] = c.Joint[i - 1] + (c.Joint[i] - c.Joint[i - 1]).normalized * c.Length[i - 1];
                // Each bone turned to point at its next joint, root first, so its children follow.
                for (int i = 0; i < n; i++)
                {
                    Transform bone = c.Bones[i];
                    Vector3 now = bone.TransformDirection(c.Axis[i]);
                    Vector3 want = c.Joint[i + 1] - bone.position;
                    if (want.sqrMagnitude > 1e-6f) bone.rotation = Quaternion.FromToRotation(now, want) * bone.rotation;
                }
            }
        }
    }
}
