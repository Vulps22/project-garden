using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The one place the local player's position is read. Neither Somnium nor Unity raises an event when the player moves,
    /// so this samples it once a frame and turns it into events: each watcher names a grid (a key from a position) and
    /// hears only when the player's key in it changes. Nothing else measures its own distance to the player.
    /// See docs/principles.md → Games are event driven.
    /// </summary>
    public class PlayerTracker : MonoBehaviour
    {
        private class Watcher
        {
            public Func<Vector3, Vector2Int> Key;
            public Action<Vector3> Changed;
            public Vector2Int Last;
            public bool Known;
        }

        private static readonly List<Watcher> _watchers = new List<Watcher>();
        private static bool _hasPosition;

        /// <summary>Where the local player's head is, once known.</summary>
        public static Vector3 Position { get; private set; }

        /// <summary>Whether the player's position is known yet.</summary>
        public static bool HasPosition => _hasPosition;

        /// <summary>Calls <paramref name="changed"/> whenever the player's key changes, and at once if the position is known.</summary>
        public static void Watch(Func<Vector3, Vector2Int> key, Action<Vector3> changed)
        {
            var w = new Watcher { Key = key, Changed = changed };
            _watchers.Add(w);
            if (_hasPosition) { w.Last = key(Position); w.Known = true; changed(Position); }
        }

        /// <summary>A square grid of this many metres, centred on the world origin.</summary>
        public static Func<Vector3, Vector2Int> Grid(float metres) =>
            p => new Vector2Int(Mathf.FloorToInt(p.x / metres), Mathf.FloorToInt(p.z / metres));

        public static void Unwatch(Action<Vector3> changed) => _watchers.RemoveAll(w => w.Changed == changed);

        // Not cleared in Awake: a watcher's OnEnable can run before this one wakes. Statics outlive the scene, so here.
        private void OnDestroy()
        {
            _watchers.Clear();
            _hasPosition = false;
        }

        private void Update()
        {
            Transform head = PlayerBridge.LocalHead;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            Position = head.position;
            _hasPosition = true;

            for (int i = 0; i < _watchers.Count; i++)
            {
                Watcher w = _watchers[i];
                Vector2Int key = w.Key(Position);
                if (w.Known && key == w.Last) continue;
                w.Last = key;
                w.Known = true;
                w.Changed(Position);
            }
        }
    }
}
