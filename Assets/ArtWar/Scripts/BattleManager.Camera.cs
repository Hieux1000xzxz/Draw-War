using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ArtWar
{
    /// <summary>
    /// A gentle look around the battlefield: drag on the field (mouse or one finger) to pan, scroll or pinch to zoom,
    /// arrow keys / WASD also pan. The view only moves a little from the default framing and glides smoothly;
    /// double-click / double-tap the field or press R to go back.
    /// </summary>
    public partial class BattleManager
    {
        const float PanLimitX = 4f, PanLimitZ = 1.6f;
        const float ZoomMin = 0.78f, ZoomMax = 1.18f;

        Vector2 viewPan, viewPanTarget;
        float viewZoom = 1f, viewZoomTarget = 1f;
        bool viewDragging;
        Vector2 viewDragLast;
        float lastFieldTap = -1f;
        float lastPinch = -1f;

        void UpdateView()
        {
            float dt = Time.unscaledDeltaTime;
            if (page == Page.None) ReadViewInput(dt);
            else viewDragging = false;

            viewPanTarget.x = Mathf.Clamp(viewPanTarget.x, -PanLimitX, PanLimitX);
            viewPanTarget.y = Mathf.Clamp(viewPanTarget.y, -PanLimitZ, PanLimitZ);
            viewZoomTarget = Mathf.Clamp(viewZoomTarget, ZoomMin, ZoomMax);
            // glide towards where the player pointed, never jump
            float k = 1f - Mathf.Exp(-dt * 6f);
            viewPan = Vector2.Lerp(viewPan, viewPanTarget, k);
            viewZoom = Mathf.Lerp(viewZoom, viewZoomTarget, k);
        }

        void ReadViewInput(float dt)
        {
            // keyboard
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 move = Vector2.zero;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) move.y -= 1f;
                viewPanTarget += move * (4f * dt);
                if (keyboard.rKey.wasPressedThisFrame) ResetView();
            }

            // mouse wheel
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !PointerOverUI()) viewZoomTarget -= Mathf.Sign(scroll) * 0.05f;
            }

            // two-finger pinch
            var touch = Touchscreen.current;
            if (touch != null && touch.touches.Count >= 2 && touch.touches[0].isInProgress && touch.touches[1].isInProgress)
            {
                float d = Vector2.Distance(touch.touches[0].position.ReadValue(), touch.touches[1].position.ReadValue());
                if (lastPinch > 0f) viewZoomTarget -= (d - lastPinch) / Screen.height * 0.8f;
                lastPinch = d;
                viewDragging = false;
                return;
            }
            lastPinch = -1f;

            // one pointer drag on the battlefield (never on the drawing page or buttons)
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame && !PointerOverUI())
            {
                viewDragging = true;
                viewDragLast = pos;
                if (Time.unscaledTime - lastFieldTap < 0.3f) ResetView(); // double click / tap
                lastFieldTap = Time.unscaledTime;
            }
            if (!pointer.press.isPressed) viewDragging = false;
            if (!viewDragging) return;

            Vector2 delta = pos - viewDragLast;
            viewDragLast = pos;
            // grab the paper: the world follows the finger
            float unitsPerPixel = 22f / Screen.height;
            viewPanTarget -= new Vector2(delta.x, delta.y * 1.6f) * unitsPerPixel;
        }

        void ResetView()
        {
            viewPanTarget = Vector2.zero;
            viewZoomTarget = 1f;
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
