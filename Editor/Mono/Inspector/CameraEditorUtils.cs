// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using System.Linq;

namespace UnityEditor
{
    public static class CameraEditorUtils
    {
        static readonly Color k_ColorThemeCameraGizmo = new Color(233f / 255f, 233f / 255f, 233f / 255f, 128f / 255f);

        public static float GameViewAspectRatio => CameraEditor.GetGameViewAspectRatio();

        static int s_MovingHandleId = 0;
        static Vector3 s_InitialFarMid;
        static readonly int[] s_FrustrumHandleIds =
        {
            "CameraEditor_FrustrumHandleTop".GetHashCode(),
            "CameraEditor_FrustrumHandleBottom".GetHashCode(),
            "CameraEditor_FrustrumHandleLeft".GetHashCode(),
            "CameraEditor_FrustrumHandleRight".GetHashCode()
        };

        public static void HandleFrustum(Camera c)
        {
            Color orgHandlesColor = Handles.color;
            Color slidersColor = k_ColorThemeCameraGizmo;
            slidersColor.a *= 2f;
            Handles.color = slidersColor;

            // get the corners of the far clip plane in world space
            var far = new Vector3[4];
            float frustumAspect;
            if (!TryGetFrustum(c, null, far, out frustumAspect))
                return;
            var leftBottomFar = far[0];
            var leftTopFar = far[1];
            var rightTopFar = far[2];
            var rightBottomFar = far[3];

            // manage our own gui changed state, so we can use it for individual slider changes
            bool guiChanged = GUI.changed;


            Vector3 farMid = Vector3.Lerp(leftBottomFar, rightTopFar, 0.5f);
            if (s_MovingHandleId != 0)
            {
                if (!s_FrustrumHandleIds.Contains(GUIUtility.hotControl))
                    s_MovingHandleId = GUIUtility.hotControl;
                else
                    farMid = s_InitialFarMid;
            }
            else if (s_FrustrumHandleIds.Contains(GUIUtility.hotControl))
            {
                s_MovingHandleId = GUIUtility.hotControl;
                s_InitialFarMid = farMid;
            }

            // FOV handles
            // Top and bottom handles
            float halfHeight = -1.0f;
            Vector3 changedPosition = MidPointPositionSlider(s_FrustrumHandleIds[0], leftTopFar, rightTopFar, c.transform.up);
            if (!GUI.changed)
                changedPosition = MidPointPositionSlider(s_FrustrumHandleIds[1], leftBottomFar, rightBottomFar, -c.transform.up);
            if (GUI.changed)
                halfHeight = (changedPosition - farMid).magnitude;

            // Left and right handles
            GUI.changed = false;
            changedPosition = MidPointPositionSlider(s_FrustrumHandleIds[2], rightBottomFar, rightTopFar, c.transform.right);
            if (!GUI.changed)
                changedPosition = MidPointPositionSlider(s_FrustrumHandleIds[3], leftBottomFar, leftTopFar, -c.transform.right);
            if (GUI.changed)
                halfHeight = (changedPosition - farMid).magnitude / frustumAspect;

            // Update camera settings if changed
            if (halfHeight >= 0.0f)
            {
                Undo.RecordObject(c, "Adjust Camera");
                if (c.orthographic)
                {
                    c.orthographicSize = halfHeight;
                }
                else
                {
                    Vector3 posUp = farMid + c.transform.up * halfHeight;
                    Vector3 posDown = farMid - c.transform.up * halfHeight;
                    Vector3 nearMid = farMid - c.transform.forward * c.farClipPlane;
                    c.fieldOfView = Vector3.Angle((posDown - nearMid), (posUp - nearMid));
                }
                guiChanged = true;
            }

            GUI.changed = guiChanged;
            Handles.color = orgHandlesColor;
        }

        public static void DrawFrustumGizmo(Camera camera)
        {
            var near = new Vector3[4];
            var far = new Vector3[4];
            float frustumAspect;
            if (CameraEditorUtils.TryGetFrustum(camera, near, far, out frustumAspect))
            {
                Color orgColor = Handles.color;
                Handles.color = k_ColorThemeCameraGizmo;
                for (int i = 0; i < 4; ++i)
                {
                    Handles.DrawLine(near[i], near[(i + 1) % 4]);
                    Handles.DrawLine(far[i], far[(i + 1) % 4]);
                    Handles.DrawLine(near[i], far[i]);
                }
                Handles.color = orgColor;
            }
        }

        // Returns near- and far-corners in this order: leftBottom, leftTop, rightTop, rightBottom
        // Assumes input arrays are of length 4 (if allocated)
        public static bool TryGetFrustum(Camera camera, Vector3[] near, Vector3[] far, out float frustumAspect)
        {
            frustumAspect = GetFrustumAspectRatio(camera);
            if (frustumAspect < 0)
                return false;

            if (far != null)
            {
                far[0] = new Vector3(0, 0, camera.farClipPlane); // leftBottomFar
                far[1] = new Vector3(0, 1, camera.farClipPlane); // leftTopFar
                far[2] = new Vector3(1, 1, camera.farClipPlane); // rightTopFar
                far[3] = new Vector3(1, 0, camera.farClipPlane); // rightBottomFar
                for (int i = 0; i < 4; ++i)
                    far[i] = camera.ViewportToWorldPointWithoutGateFit(far[i]);
            }

            if (near != null)
            {
                near[0] = new Vector3(0, 0, camera.nearClipPlane); // leftBottomNear
                near[1] = new Vector3(0, 1, camera.nearClipPlane); // leftTopNear
                near[2] = new Vector3(1, 1, camera.nearClipPlane); // rightTopNear
                near[3] = new Vector3(1, 0, camera.nearClipPlane); // rightBottomNear
                for (int i = 0; i < 4; ++i)
                    near[i] = camera.ViewportToWorldPointWithoutGateFit(near[i]);
            }
            return true;
        }

        public static bool IsViewportRectValidToRender(Rect normalizedViewPortRect)
        {
            if (normalizedViewPortRect.width <= 0f || normalizedViewPortRect.height <= 0f)
                return false;
            if (normalizedViewPortRect.x >= 1f || normalizedViewPortRect.xMax <= 0f)
                return false;
            if (normalizedViewPortRect.y >= 1f || normalizedViewPortRect.yMax <= 0f)
                return false;
            return true;
        }

        public static float GetFrustumAspectRatio(Camera camera)
        {
            var normalizedViewPortRect = camera.rect;
            if (normalizedViewPortRect.width <= 0f || normalizedViewPortRect.height <= 0f)
                return -1f;

            return camera.usePhysicalProperties ?
                camera.sensorSize.x / camera.sensorSize.y : GameViewAspectRatio * normalizedViewPortRect.width / normalizedViewPortRect.height;
        }

        public static Vector3 PerspectiveClipToWorld(Matrix4x4 clipToWorld, Vector3 viewPositionWS, Vector3 positionCS)
        {
            var tempCS = new Vector3(positionCS.x, positionCS.y, 0.95f);
            var result = clipToWorld.MultiplyPoint(tempCS);
            var r = result - viewPositionWS;
            return r.normalized * positionCS.z + viewPositionWS;
        }

        public static void GetFrustumPlaneAt(Matrix4x4 clipToWorld, Vector3 viewPosition, float distance, Vector3[] points)
        {
            points[0] = new Vector3(-1, -1, distance); // leftBottomFar
            points[1] = new Vector3(-1, 1, distance); // leftTopFar
            points[2] = new Vector3(1, 1, distance); // rightTopFar
            points[3] = new Vector3(1, -1, distance); // rightBottomFar
            for (var i = 0; i < 4; ++i)
                points[i] = PerspectiveClipToWorld(clipToWorld, viewPosition, points[i]);
        }

        static Vector3 MidPointPositionSlider(int controlID, Vector3 position1, Vector3 position2, Vector3 direction)
        {
            Vector3 midPoint = Vector3.Lerp(position1, position2, 0.5f);
            return Handles.Slider(controlID, midPoint, direction, HandleUtility.GetHandleSize(midPoint) * 0.03f, Handles.DotHandleCap, 0f);
        }
    }
}
