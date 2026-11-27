using System;
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

        private static readonly AccessTools.FieldRef<UICardRelic, Image> RelicIcon =
            AccessTools.FieldRefAccess<UICardRelic, Image>("icon");

        private UIOverlayMerchant merchant;
        private Transform buys;
        private Button reroll;
        private Button cancel;
        private const float NativeDimAlpha = 0.6f;
        private const float ReadableDimAlpha = 0.9f;
        private RectTransform header;
        private TMP_Text title;
        private Transform resources;
        private RectTransform strip;
        private CanvasGroup stripGroup;
        private float offset;
        private bool chooseFocus;
        private bool keepReroll;
        private float focusUntil;
        private GameObject fallbackFocus;
        private bool placedFallback;
        private readonly Vector3[] corners = new Vector3[4];

        internal static void ShowPurchased(UIOverlayMerchant merchant, GameObject card, string identifier,
            ResourceAmount? paid, Func<Relic, ResourceAmount[]> quote)
        {
            if (string.IsNullOrEmpty(identifier)) return;
            var relic = AddressablesManager.Instance.GetRelic(identifier);
            if (relic == null || relic.GetIdentifier() != identifier) return;
            var owned = RelicManager.Instance.GetRelic(identifier);
            var sprite = owned != null && owned.GetIdentifier() == identifier ? owned.GetIcon() : relic.GetIcon();
            if (sprite != null)
            {
                var icon = new GameObject("Purchased relic", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(card.transform, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.color = new Color(0.82f, 0.82f, 0.82f);
                var rect = icon.rectTransform;
                rect.anchorMin = new Vector2(0.34f, 0.63f);
                rect.anchorMax = new Vector2(0.66f, 0.85f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            // The save stores the sold identifier. Until a purchase is made in this session,
            // the active shop quote is the only price available for that slot.
            var costs = paid.HasValue ? null : quote?.Invoke(relic);
            var display = paid ?? (costs?.Length == 1 ? costs[0] : (ResourceAmount?)null);
            if (!display.HasValue || display.Value.amount < 0) return;
            var row = merchant.transform.Find("Panel/BuyButtons");
            if (row == null || row.childCount == 0) return;
            var price = row.GetChild(row.childCount - 1);
            var group = price.GetComponent<CanvasGroup>();
            var button = price.GetComponent<Button>();
            var amount = price.Find("Amount")?.GetComponent<TMP_Text>();
            var currency = price.Find("Icon")?.GetComponent<Image>();
            if (group == null || button == null || amount == null || currency == null) return;
            amount.text = " " + display.Value.amount;
            amount.color = new Color32(241, 226, 202, 255);
            currency.sprite = RaceManager.Instance.GetResourceIcon(display.Value.type);
            var colors = button.colors;
            colors.disabledColor = new Color32(91, 62, 54, 255);
            button.colors = colors;
            group.blocksRaycasts = false;
            group.alpha = 1f;
        }

        internal static void ShowUnavailable(UICardRelic card, ResourceAmount[] costs, Transform price)
        {
            if (!ReadablePrice(price.GetComponent<Button>(), price.GetComponent<CanvasGroup>()) || card == null) return;

            // Keep the title and effect panel at full contrast.
            var art = RelicIcon(card);
            if (art != null) art.color *= new Color(0.66f, 0.66f, 0.66f, 1f);

            if (costs?.Length != 1 || costs[0].amount <= 0) return;
            int deficit = costs[0].amount - ResourceManager.Instance.GetResourceAvailableAmount(costs[0].type);
            var amount = price.Find("Amount")?.GetComponent<TMP_Text>();
            if (deficit <= 0 || amount == null) return;

            var label = new GameObject("Price shortfall", typeof(RectTransform), typeof(LayoutElement));
            label.GetComponent<LayoutElement>().ignoreLayout = true;
            var hint = label.AddComponent<TextMeshProUGUI>();
            var rect = hint.rectTransform;
            rect.SetParent(price, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(0f, -25f);
            rect.offsetMax = new Vector2(0f, -1f);
            hint.font = amount.font;
            hint.fontSize = Mathf.Min(17f, amount.fontSize);
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color32(255, 205, 151, 255);
            hint.raycastTarget = false;
            hint.enableWordWrapping = false;
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 10f;
            hint.fontSizeMax = hint.fontSize;
            hint.text = "-" + deficit + " " + ResourceManager.Instance.GetResourcesTitle(costs[0].type, deficit);
        }

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
            ChooseFocusAfterRefresh(false);
        }

        private static bool ReadablePrice(Button button, CanvasGroup group)
        {
            // The native disabled tint and canvas fade both dim the button.
            if (button == null || group == null || !Mathf.Approximately(group.alpha, NativeDimAlpha) || button.IsInteractable()) return false;
            var colors = button.colors;
            var tint = colors.disabledColor;
            tint.a = 1f;
            colors.disabledColor = tint;
            button.colors = colors;
            group.alpha = ReadableDimAlpha;
            return true;
        }

        internal void ChooseFocusAfterRefresh(bool afterReroll)
        {
            chooseFocus = true;
            keepReroll = afterReroll;
            placedFallback = false;
            fallbackFocus = null;
            focusUntil = Time.unscaledTime + 1.3f;
        }

        private void LateUpdate()
        {
            if (CurrentOverlay(OverlayManager.Instance) != merchant || Details(OverlayManager.Instance).IsActive()) return;
            ClearLedger();
            var pad = GamepadManager.Instance;
            var events = EventSystem.current;
            if (events == null || !pad.IsGamepadInUse() || PauseMenuManger.Instance?.IsOpen() == true) return;
            var selected = events.currentSelectedGameObject;
            if (chooseFocus)
            {
                // The native offers briefly disable their price buttons when animated in.
                // Upgrade only our automatic footer selection, leaving subsequent navigation alone.
                if ((placedFallback && selected != fallbackFocus && Usable(selected)) || Time.unscaledTime > focusUntil)
                    chooseFocus = false;
                else if (keepReroll && Usable(reroll?.gameObject))
                {
                    chooseFocus = false;
                    pad.SetGameObjectToSelect(reroll.gameObject, true);
                    return;
                }
                else
                {
                    var offer = FirstBuy();
                    if (offer != null)
                    {
                        chooseFocus = false;
                        pad.SetGameObjectToSelect(offer, true);
                        return;
                    }
                    if (!placedFallback)
                    {
                        fallbackFocus = Usable(selected) ? selected : Footer();
                        if (fallbackFocus != null) pad.SetGameObjectToSelect(fallbackFocus, true);
                        placedFallback = true;
                    }
                    return;
                }
            }

            if (Usable(selected)) return;
            var target = pad.GetGameObjectToSelect();
            if (!Usable(target)) target = FirstBuy() ?? Footer();
            if (target != null) pad.SetGameObjectToSelect(target, true);
        }

        private GameObject FirstBuy()
        {
            if (buys != null)
                foreach (Transform child in buys)
                    if (Usable(child.gameObject)) return child.gameObject;
            return null;
        }

        private GameObject Footer() => Usable(reroll?.gameObject) ? reroll.gameObject :
            Usable(cancel?.gameObject) ? cancel.gameObject : null;

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
