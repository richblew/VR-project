using NUnit.Framework;
using UnityEngine;
using VRRocket.Editor;

namespace VRRocket.Tests
{
    public class AttachPointGeneratorTests
    {
        [Test]
        public void GeneratedTransformsMatchJson()
        {
            var data = AttachPointGenerator.Load();
            var root = new GameObject("BodyTubeTest").transform;
            try
            {
                var points = AttachPointGenerator.Generate(root);
                Assert.AreEqual(data.points.Length, points.Count);
                Assert.AreEqual(12, points.Count, "SPEC.md section 4.4 lists twelve attach points");
                foreach (var p in data.points)
                {
                    var t = root.Find(AttachPointGenerator.ContainerName + "/" + p.name);
                    Assert.IsNotNull(t, p.name + " missing");
                    var expected = new Vector3(p.pos_m[0], p.pos_m[1], p.pos_m[2]);
                    Assert.Less((t.localPosition - expected).magnitude, 1e-6f, p.name + " position");
                    Assert.Less(Quaternion.Angle(t.localRotation, Quaternion.Euler(0f, p.yaw_deg, 0f)), 1e-3f, p.name + " rotation");
                    var ap = t.GetComponent<AttachPoint>();
                    Assert.IsNotNull(ap);
                    Assert.AreEqual(AttachPoint.AcceptsFromName(p.name), ap.accepts);
                    // A point at yaw a sits at (R sin a, h, R cos a), so its local +Z points outward through the point.
                    var radial = new Vector3(expected.x, 0f, expected.z);
                    if (radial.magnitude > 1e-4f)
                        Assert.Less(Vector3.Angle(radial, t.localRotation * Vector3.forward), 0.5f, p.name + " +Z should point outward");
                }
                // Running twice must not duplicate children.
                AttachPointGenerator.Generate(root);
                Assert.AreEqual(12, root.Find(AttachPointGenerator.ContainerName).childCount);
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
