using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// A safe zone beats the floor rule: a part resting in a zone below the respawn floor line (a bin shaft under a low console
    /// shelf) stays where it is instead of jumping back to the bench.
    /// </summary>
    public class RespawnSafeZoneTests : DevSceneTestBase
    {
        [UnityTest]
        public IEnumerator Part_In_A_Safe_Zone_Below_The_Floor_Line_Stays()
        {
            var respawner = workstation.respawner;
            var padT = workstation.transform.Find("ScaledRoot/RespawnPad");
            var pad = padT != null ? padT.position : workstation.transform.position;
            // a pocket 0.8 m below the pad: under the floor line (pad - 0.5), well away from the bench
            var pocket = pad + new Vector3(1.2f, -0.8f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PocketFloor";
            floor.transform.position = pocket - Vector3.up * 0.05f;
            floor.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            var zoneGo = new GameObject("PocketZone");
            zoneGo.transform.position = pocket + Vector3.up * 0.2f;
            var zone = zoneGo.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(0.5f, 0.4f, 0.5f);
            respawner.AddSafeZone(zone);
            Physics.SyncTransforms();

            var fin = Part(PartType.TailFin, 1);
            var drop = pocket + Vector3.up * 0.05f;
            fin.transform.position = drop;
            if (fin.body != null) { fin.body.position = drop; fin.body.linearVelocity = Vector3.zero; fin.body.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();

            yield return new WaitForSeconds(tuning.respawnDelay + 1.5f);
            Assert.Less(Vector3.Distance(fin.transform.position, pocket), 0.3f, "the fin is still in the pocket, not respawned on the pad");
            Assert.IsFalse(respawner.IsOutOfBounds(fin.transform.position, out var below), "a safe zone counts as in bounds even below the floor line");
            Assert.IsFalse(below);
            Object.Destroy(floor);
            Object.Destroy(zoneGo);
        }
    }
}
