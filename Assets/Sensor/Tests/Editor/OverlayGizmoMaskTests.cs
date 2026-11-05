using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class OverlayGizmoMaskTests
    {
        private GameObject _cameraObject;
        private RenderTexture _texture;
        private Camera _camera;
        private readonly object _owner = new object();

        [SetUp]
        public void SetUp()
        {
            _texture = new RenderTexture(1000, 500, 16);
            _cameraObject = new GameObject("mask camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.targetTexture = _texture;
            _camera.fieldOfView = 60f;
            _camera.transform.position = Vector3.zero;
            _camera.transform.rotation = Quaternion.identity;
        }

        [TearDown]
        public void TearDown()
        {
            OverlayGizmoMask.Withdraw(_owner);
            Object.DestroyImmediate(_cameraObject);
            _texture.Release();
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void WithNothingCovered_TheWholeLineIsVisible()
        {
            var parts = new List<Vector2>();
            OverlayGizmoMask.GetVisibleParts(_camera, new Vector3(-5, 0, 10), new Vector3(5, 0, 10), parts);

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(new Vector2(0f, 1f), parts[0]);
        }

        [Test]
        public void ALineThroughTheCoveredArea_IsCutInTwo_AndTheCutsLandOnTheEdges()
        {
            OverlayGizmoMask.Publish(_owner, new[] { new Rect(400, 0, 200, 500) });
            Vector3 from = new Vector3(-8, 0, 10);
            Vector3 to = new Vector3(8, 0, 10);

            var parts = new List<Vector2>();
            OverlayGizmoMask.GetVisibleParts(_camera, from, to, parts);

            Assert.AreEqual(2, parts.Count, "The middle is hidden, so two pieces are left.");
            float leftEdge = _camera.WorldToScreenPoint(Vector3.Lerp(from, to, parts[0].y)).x;
            float rightEdge = _camera.WorldToScreenPoint(Vector3.Lerp(from, to, parts[1].x)).x;
            Assert.AreEqual(400f - 2f, leftEdge, 1f, "The first piece must stop at the covered area, which is padded by 2 pixels.");
            Assert.AreEqual(600f + 2f, rightEdge, 1f, "The second piece must start where the covered area ends.");
        }

        [Test]
        public void ALineOutsideTheCoveredArea_IsLeftAlone()
        {
            OverlayGizmoMask.Publish(_owner, new[] { new Rect(800, 0, 200, 500) });

            var parts = new List<Vector2>();
            OverlayGizmoMask.GetVisibleParts(_camera, new Vector3(-3, 0, 10), new Vector3(3, 0, 10), parts);

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(new Vector2(0f, 1f), parts[0]);
        }

        [Test]
        public void ALineEntirelyBehindTheOverlay_IsRemoved()
        {
            OverlayGizmoMask.Publish(_owner, new[] { new Rect(0, 0, 1000, 500) });

            var parts = new List<Vector2>();
            OverlayGizmoMask.GetVisibleParts(_camera, new Vector3(-1, 0, 10), new Vector3(1, 0, 10), parts);

            Assert.IsEmpty(parts);
        }

        [Test]
        public void ALineThatStartsInsideTheCoveredArea_KeepsOnlyTheOutsidePart()
        {
            OverlayGizmoMask.Publish(_owner, new[] { new Rect(500, 0, 500, 500) });
            Vector3 from = new Vector3(2, 0, 10);
            Vector3 to = new Vector3(-8, 0, 10);

            var parts = new List<Vector2>();
            OverlayGizmoMask.GetVisibleParts(_camera, from, to, parts);

            Assert.AreEqual(1, parts.Count);
            Assert.Greater(parts[0].x, 0f, "The start is hidden.");
            Assert.AreEqual(1f, parts[0].y, 0.0001f, "The end is visible.");
        }
    }
}
