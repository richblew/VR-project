using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CablePuzzle
{
    /// <summary>Tracks which pairs are connected and raises events when all are (or stop being) connected.</summary>
    public class CablePairManager : MonoBehaviour
    {
        public XRSocketInteractor[] sockets;          // one PairSocket per pair
        public bool logEvents = true;
        public UnityEvent onAllConnected;
        public UnityEvent onBroken;

        /// <summary>(pairId, connected)</summary>
        public event Action<int, bool> PairChanged;

        readonly HashSet<int> connected = new HashSet<int>();

        void OnEnable()
        {
            foreach (var s in sockets)
            {
                if (s == null) continue;
                s.selectEntered.AddListener(OnPlugged);
                s.selectExited.AddListener(OnUnplugged);
            }
        }

        void OnDisable()
        {
            foreach (var s in sockets)
            {
                if (s == null) continue;
                s.selectEntered.RemoveListener(OnPlugged);
                s.selectExited.RemoveListener(OnUnplugged);
            }
        }

        void OnPlugged(SelectEnterEventArgs e)
        {
            var socket = e.interactorObject as PairSocket;
            if (socket == null) return;
            if (!connected.Add(socket.pairId)) return;
            if (logEvents) Debug.Log($"[CablePuzzle] Pair {socket.pairId} connected ({connected.Count}/{sockets.Length})");
            PairChanged?.Invoke(socket.pairId, true);
            if (connected.Count == sockets.Length) onAllConnected.Invoke();
        }

        void OnUnplugged(SelectExitEventArgs e)
        {
            var socket = e.interactorObject as PairSocket;
            if (socket == null) return;
            bool wasComplete = connected.Count == sockets.Length;
            if (!connected.Remove(socket.pairId)) return;
            if (logEvents) Debug.Log($"[CablePuzzle] Pair {socket.pairId} disconnected ({connected.Count}/{sockets.Length})");
            PairChanged?.Invoke(socket.pairId, false);
            if (wasComplete) onBroken.Invoke();
        }
    }
}
