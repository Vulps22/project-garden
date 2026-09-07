using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden.Generation
{
    // ⚠ Unsure if we're keeping this. Landed on a result that reads well (2026-09-07) after
    // several passes — amalgamation of boulders, then a too-formal symmetric mountain, then
    // rubble/gaps from small filler rocks — but it hasn't been walked through in first person or
    // judged against actually replacing hand-placed fragments yet. Don't build on top of this
    // (procgen, more tooling) until that's settled.
    //
    // Every rock's MeshCollider used to be stripped here on the assumption the formation was
    // decorative and unreachable — a call that was never actually checked against this being a
    // free-roaming world, and meant a player standing on or flying under one of these fell
    // straight through. Colliders are left alone now; each rock keeps whatever collider its own
    // prefab ships with. Do not strip them again without confirming first.

    /// <summary>
    /// Builds one floating-island fragment: a flat top where the grass sits, a chunk of rock
    /// hanging below it. Aloft is the reference, not a geometric crystal — Aloft's islands are
    /// small, irregular, asymmetric lumps of rock, not matching mountain-peak silhouettes, and
    /// that is deliberately what this produces now. A fixed prefab sits on top of every fragment
    /// regardless of its shape, so fragments never needed to share one silhouette or a dramatic
    /// taper to a point — an early pass built exactly that (a symmetric inverted hexagonal
    /// mountain with a rigid ridge structure) and it read as too formal and too busy. Depth and
    /// ridge count are now rolled per fragment specifically so no two look like the same stamp.
    ///
    /// One rock per ridge position, scaled up until neighbours overlap — not one rock plus a
    /// smaller "filler" rock pulled in to cover whatever gap is left. A pass that added small
    /// rocks to close gaps between bigger ones was the actual cause of a report of standing
    /// inside the mesh surrounded by "rubble": from inside, a cluster of small overlapping
    /// pieces has gaps a single bigger rock closing the same span does not.
    ///
    /// No UnityEditor dependency anywhere in this class. That is the whole point of it: the exact
    /// code that authors a hand-picked fragment today is the code that will spawn one live once
    /// islands are generated at runtime instead of by hand (see docs/roadmap.md's procgen
    /// section). The one thing that legitimately differs between those two callers — how a rock
    /// prefab gets instantiated, PrefabUtility.InstantiatePrefab in the Editor so it stays a
    /// nested prefab instance, plain Instantiate at runtime — is injected as a delegate rather
    /// than branched on internally, so this class never needs to know which caller it has.
    ///
    /// Rocks still sit on a ring of ridge angles at full band radius rather than scattering at
    /// fully random angles — that structure is what keeps a small rock count reading as one mass
    /// with distinct faces instead of a handful of boulders that happen to be near each other.
    /// Each ridge angle now carries its own random jitter rather than sitting at a perfectly even
    /// step, which is what keeps the result irregular rather than a clean hexagon.
    /// </summary>
    public static class IslandFragmentGenerator
    {
        [Serializable]
        public struct Settings
        {
            public float TopRadius;

            /// <summary>How far below the flat top the fragment reaches, rolled once per
            /// fragment from this range — fragments are not meant to share one depth.</summary>
            public Vector2 DepthRange;

            /// <summary>Ridge count is rolled once per fragment from this range (inclusive).</summary>
            public Vector2Int RidgeCountRange;

            public int BandCount;

            /// <summary>Radius falloff per band is TopRadius * (1 - t)^TaperPower. Above 1 gives a
            /// steeper taper; 1 is linear. Kept gentle by default — a steep taper is what reads as
            /// a deliberate mountain peak rather than an irregular lump.</summary>
            public float TaperPower;

            /// <summary>World-space Y the top must never rise above — wherever the grass sits.</summary>
            public float FlatTopY;

            public int Seed;
        }

        public static Settings DefaultSettings => new Settings
        {
            TopRadius = 6.5f,
            DepthRange = new Vector2(4f, 10f),
            RidgeCountRange = new Vector2Int(4, 7),
            BandCount = 3,
            TaperPower = 1.15f,
            FlatTopY = 0f,
            Seed = 0,
        };

        /// <summary>
        /// Rejects flat slabs and shards — the asset pack mixes chunky boulder shapes in with
        /// thin wedge/wing shapes, and the latter don't cluster into a coherent mass, they just
        /// stick out. About half of a typical rock-pack folder gets rejected by this.
        /// </summary>
        public static List<GameObject> FilterChunky(IEnumerable<GameObject> candidates, float maxAspect = 2.0f)
        {
            var list = new List<GameObject>();
            foreach (GameObject p in candidates)
            {
                MeshFilter mf = p != null ? p.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;

                Vector3 s = mf.sharedMesh.bounds.size;
                float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                float minDim = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                if (minDim <= 0.001f || maxDim / minDim > maxAspect) continue;

                list.Add(p);
            }
            return list;
        }

        /// <summary>
        /// Builds the fragment under a new root GameObject (parented under <paramref name="parent"/>
        /// if given) and returns it. <paramref name="instantiate"/> is called once per rock —
        /// pass PrefabUtility.InstantiatePrefab from an Editor tool, or plain Object.Instantiate
        /// from a runtime spawner.
        /// </summary>
        public static GameObject Generate(
            Settings settings,
            IReadOnlyList<GameObject> graniteVariants,
            IReadOnlyList<GameObject> mossVariants,
            Func<GameObject, Transform, GameObject> instantiate,
            Transform parent = null)
        {
            var rng = new System.Random(settings.Seed);
            var root = new GameObject("IslandFragment");
            if (parent != null) root.transform.SetParent(parent, false);

            float NextFloat(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            GameObject PickGranite() => graniteVariants[rng.Next(graniteVariants.Count)];
            GameObject PickMoss() => mossVariants[rng.Next(mossVariants.Count)];

            // Rolled once per fragment, not fixed — this is what makes fragments differ from each
            // other rather than all being the same depth stamped with a different random seed.
            float depth = NextFloat(settings.DepthRange.x, settings.DepthRange.y);
            int ridgeCount = rng.Next(settings.RidgeCountRange.x, settings.RidgeCountRange.y + 1);
            float ridgeStep = 360f / ridgeCount;
            // Randomised per fragment so identical settings don't always put a ridge at the same
            // world angle — matters once several of these stand near each other.
            float ridgeRotationOffset = NextFloat(0f, 360f);

            for (int band = 0; band < settings.BandCount; band++)
            {
                float t0 = band / (float)settings.BandCount;
                float t1 = (band + 1) / (float)settings.BandCount;
                float tMid = (t0 + t1) * 0.5f;

                float bandRadius = settings.TopRadius * Mathf.Pow(1f - tMid, settings.TaperPower);
                float yTop = -depth * t0;
                float yBottom = -depth * t1;
                bool isTopBand = band == 0;
                // Scale tied directly to this band's own radius, not to depth fraction on a
                // separate curve — a first pass sized rocks by tMid alone, and near the point,
                // where TaperPower had already shrunk the ring hard, rocks sized off the gentler
                // depth curve came out wider than the ring they sat on and bulged outward into a
                // mushroom tip instead of a clean point. Generous multiplier — big enough that
                // neighbouring ridge rocks overlap and close the ring on their own.
                float scaleBase = Mathf.Clamp(bandRadius * 0.85f, 0.6f, 2.4f);

                for (int r = 0; r < ridgeCount; r++)
                {
                    // Jittered off the even step rather than sitting exactly on it — an evenly
                    // spaced ring is what read as a deliberate geometric crystal instead of an
                    // irregular lump.
                    float ridgeAngle = ridgeRotationOffset + r * ridgeStep + NextFloat(-ridgeStep * 0.25f, ridgeStep * 0.25f);
                    // Moss only ever appears as a top-band accent — a surface growth, not a
                    // material the bedrock is made of — and rides the same full-size rock rather
                    // than a smaller piece added next to it. A separate small rock "closing a
                    // gap" is exactly what read as loose rubble instead of one mass; a single
                    // bigger rock closes the same gap cleanly and is the only rock there.
                    GameObject prefab = (isTopBand && rng.NextDouble() < 0.25) ? PickMoss() : PickGranite();
                    PlaceRock(root.transform, instantiate, prefab, ridgeAngle, bandRadius,
                        yTop, yBottom, scaleBase, settings.FlatTopY, rng);
                }
            }

            // The point — one rock, scaled up rather than several small ones clustered together.
            // A single isolated small rock at the tip read as a drip hanging off a thread; a
            // cluster of small ones read as the same rubble problem as the detail rocks did.
            {
                GameObject tip = instantiate(PickGranite(), root.transform);
                tip.transform.localPosition = new Vector3(0f, -depth, 0f);
                tip.transform.localRotation = Quaternion.Euler(NextFloat(0f, 360f), NextFloat(0f, 360f), NextFloat(0f, 360f));
                tip.transform.localScale = Vector3.one * NextFloat(0.7f, 1.0f);
            }

            return root;
        }

        private static void PlaceRock(
            Transform parent, Func<GameObject, Transform, GameObject> instantiate, GameObject prefab,
            float angleDeg, float radius, float yTop, float yBottom, float scaleBase,
            float flatTopY, System.Random rng)
        {
            float NextFloat(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            GameObject instance = instantiate(prefab, parent);
            float rad = angleDeg * Mathf.Deg2Rad;
            float y = NextFloat(yBottom, yTop);
            instance.transform.localPosition = new Vector3(Mathf.Cos(rad) * radius, y, Mathf.Sin(rad) * radius);
            // Rotation stays close to upright — a small tilt reads as a placed facet; the wide
            // random tumble a first pass used is what made separate boulders instead of one shard.
            instance.transform.localRotation = Quaternion.Euler(NextFloat(-5f, 5f), NextFloat(0f, 360f), NextFloat(-5f, 5f));
            instance.transform.localScale = Vector3.one * scaleBase * NextFloat(0.85f, 1.15f);

            // Every rock, not just the top band's — some rocks in this pack run up to ~7m tall,
            // so even a band-1 rock can poke through the flat plane on an unlucky roll. The flat
            // top is a global invariant of the whole formation, not a per-band special case.
            ClampBelow(instance, flatTopY);
        }

        /// <summary>
        /// Pulls a rock straight down until no part of it rises above <paramref name="y"/>. The
        /// flat top is enforced this way — by clamping whichever instances would poke through —
        /// rather than by a special flat mesh, so it works for any rock in the pack.
        /// </summary>
        private static void ClampBelow(GameObject instance, float y)
        {
            Renderer rend = instance.GetComponentInChildren<Renderer>();
            if (rend == null) return;

            float overshoot = rend.bounds.max.y - y;
            if (overshoot > 0f)
                instance.transform.position -= new Vector3(0f, overshoot + 0.05f, 0f);
        }
    }
}
