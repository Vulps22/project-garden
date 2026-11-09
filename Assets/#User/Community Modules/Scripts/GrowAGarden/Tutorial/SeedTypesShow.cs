using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.DevTools;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>The tutorial's seed types: every buffing and upgrade seed in the game, coming in from smoke and going back out, on a loop.</summary>
    public class SeedTypesShow : MonoBehaviour
    {
        [Tooltip("Buffing seeds line up along this transform's right.")]
        [SerializeField] private Transform _buffingRow;
        [Tooltip("Upgrade seeds line up along this transform's right.")]
        [SerializeField] private Transform _upgradeRow;
        [Tooltip("Shown above each row only while the row has seeds.")]
        [SerializeField] private GameObject _buffingLabel;
        [SerializeField] private GameObject _upgradeLabel;
        [SerializeField] private SmokePuff _smoke;
        [Tooltip("How much bigger than in the hand the seeds are shown.")]
        [SerializeField] private float _scale = 2.5f;
        [Tooltip("Metres between seeds in a row.")]
        [SerializeField] private float _spacing = 0.7f;
        [SerializeField] private float _holdSeconds = 3f;   // after the second it takes to build
        [SerializeField] private float _gapSeconds = 1f;
        [Tooltip("Degrees a second each seed turns while shown.")]
        [SerializeField] private float _spin = 40f;

        private readonly List<TutorialStage> _seeds = new List<TutorialStage>();
        private bool _built;

        /// <summary>Starts the loop.</summary>
        public void Play()
        {
            gameObject.SetActive(true);
            Build();
            StopAllCoroutines();
            StartCoroutine(Loop());
        }

        /// <summary>Ends the loop and hides everything at once.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            foreach (TutorialStage seed in _seeds) if (seed != null) seed.Hide();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            foreach (TutorialStage seed in _seeds)
                if (seed != null && seed.gameObject.activeSelf) seed.transform.Rotate(0f, _spin * Time.deltaTime, 0f, Space.World);
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                foreach (TutorialStage seed in _seeds) seed.Appear();
                yield return new WaitForSeconds(TutorialStage.SECONDS + _holdSeconds);
                foreach (TutorialStage seed in _seeds) seed.Vanish();
                yield return new WaitForSeconds(TutorialStage.SECONDS + _gapSeconds);
            }
        }

        /// <summary>Copies of every buffing and upgrade seed in the stalls and the Developer Market, once.</summary>
        private void Build()
        {
            if (_built) return;
            _built = true;
            var buffing = new List<SeedDefinition>();
            var upgrades = new List<SeedDefinition>();
            foreach (SeedDefinition seed in AllSeeds())
            {
                if (seed.buffMultiplier > 1f) Add(buffing, seed);
                else if (seed.upgradeBonus > 0f) Add(upgrades, seed);
            }
            Lay(buffing, _buffingRow);
            Lay(upgrades, _upgradeRow);
            if (_buffingLabel != null) _buffingLabel.SetActive(buffing.Count > 0);
            if (_upgradeLabel != null) _upgradeLabel.SetActive(upgrades.Count > 0);
            Logger.Info($"Build() '{gameObject.name}' — {buffing.Count} buffing and {upgrades.Count} upgrade seeds");
        }

        private static IEnumerable<SeedDefinition> AllSeeds()
        {
            if (WorldManager.Instance != null)
                foreach (SeedDefinition seed in WorldManager.Instance.Buyables) if (seed != null) yield return seed;
            for (int i = 0; i < DeveloperMarket.Count; i++)
            {
                SeedDefinition seed = DeveloperMarket.SeedOf(i);
                if (seed != null) yield return seed;
            }
        }

        private static void Add(List<SeedDefinition> list, SeedDefinition seed)
        {
            if (seed.seedPrefab == null || list.Exists(s => s.seedId == seed.seedId)) return;
            list.Add(seed);
        }

        private void Lay(List<SeedDefinition> seeds, Transform row)
        {
            if (row == null) return;
            for (int i = 0; i < seeds.Count; i++)
            {
                GameObject copy = VisualCopy.Of(seeds[i].seedPrefab.gameObject, row, seeds[i].displayName);
                copy.transform.localPosition = Vector3.right * ((i - (seeds.Count - 1) * 0.5f) * _spacing);
                copy.transform.localScale = Vector3.one * _scale;
                TutorialStage stage = copy.AddComponent<TutorialStage>();
                stage.UseSmoke(_smoke);
                stage.Hide();
                _seeds.Add(stage);
            }
        }
    }
}
