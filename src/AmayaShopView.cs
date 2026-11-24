using BepInEx.Bootstrap;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AmayaWithoutCooldown
{
    internal sealed class AmayaShopView : MonoBehaviour
    {
        internal static readonly AccessTools.FieldRef<OverlayManager, UIOverlayBase> CurrentOverlay =
            AccessTools.FieldRefAccess<OverlayManager, UIOverlayBase>("m_CurrentlyOpen");
        private static readonly AccessTools.FieldRef<OverlayManager, UIRelicDetails> Details =
            AccessTools.FieldRefAccess<OverlayManager, UIRelicDetails>("relicDetails");

        private UIOverlayMerchant merchant;
        private Transform buys;
        private Button reroll;
        private Button cancel;
        private RectTransform header;
        private TMP_Text title;
        private Transform resources;
        private RectTransform strip;
        private CanvasGroup stripGroup;
        private float offset;
        private readonly Vector3[] corners = new Vector3[4];

        internal void Open()
        {
            merchant = GetComponent<UIOverlayMerchant>();
            buys = transform.Find("Panel/BuyButtons");
            reroll = transform.Find("Panel/Buttons/Reroll/Button")?.GetComponent<Button>();
            cancel = transform.Find("Panel/Buttons/Cancel/Button")?.GetComponent<Button>();
            header = transform.Find("HeaderContainer") as RectTransform;
            title = header?.Find("Header")?.GetComponent<TMP_Text>();
            if (Chainloader.PluginInfos.ContainsKey("sfk.daybook"))
                resources = FindObjectOfType<UIHeader>()?.transform.Find("PanelResources");
            enabled = true;
        }

        private void LateUpdate()
        {
            if (CurrentOverlay(OverlayManager.Instance) != merchant || Details(OverlayManager.Instance).IsActive()) return;
            ClearLedger();
            var pad = GamepadManager.Instance;
            var events = EventSystem.current;
            if (events == null || !pad.IsGamepadInUse() || PauseMenuManger.Instance?.IsOpen() == true) return;
            if (Usable(events.currentSelectedGameObject)) return;

            // World dialogue and a reroll can clear EventSystem selection after the
            // merchant has already recorded the button it wants the controller to use.
            var target = pad.GetGameObjectToSelect();
            if (!Usable(target))
            {
                target = null;
                if (buys != null)
                    foreach (Transform child in buys)
                        if (Usable(child.gameObject)) { target = child.gameObject; break; }
                if (target == null && Usable(reroll?.gameObject)) target = reroll.gameObject;
                if (target == null && Usable(cancel?.gameObject)) target = cancel.gameObject;
            }
            if (target != null) pad.SetGameObjectToSelect(target, true);
        }

        private bool Usable(GameObject obj) => obj != null && obj.activeInHierarchy &&
            obj.transform.IsChildOf(transform) && obj.GetComponent<Button>() is Button b && b.isActiveAndEnabled && b.IsInteractable();

        internal bool CanBackOut() => isActiveAndEnabled && !Details(OverlayManager.Instance).IsActive() &&
            Usable(cancel?.gameObject);

        private void ClearLedger()
        {
            if (resources == null || title == null || !title.isActiveAndEnabled) return;
            if (strip == null)
            {
                strip = resources.Find("Daybook/Daily changes") as RectTransform;
                stripGroup = strip?.GetComponentInParent<CanvasGroup>();
            }
            float required = 0f;
            if (strip != null && strip.gameObject.activeInHierarchy && (stripGroup == null || stripGroup.alpha > 0f))
            {
                var parent = header.parent;
                strip.GetWorldCorners(corners);
                var lo = parent.InverseTransformPoint(corners[0]);
                var hi = parent.InverseTransformPoint(corners[2]);
                var bounds = title.textBounds;
                var top = parent.InverseTransformPoint(title.transform.TransformPoint(bounds.max));
                var bottom = parent.InverseTransformPoint(title.transform.TransformPoint(bounds.min));
                if (bottom.x < hi.x && top.x > lo.x && bottom.y + offset < hi.y)
                    required = Mathf.Max(0f, top.y + offset - lo.y + 4f);
            }
            SetOffset(required);
        }

        private void SetOffset(float value)
        {
            if (header != null && Mathf.Abs(value - offset) > 0.01f)
                header.anchoredPosition += Vector2.up * (offset - value);
            offset = value;
        }

        private void OnDisable() => SetOffset(0f);
    }
}
