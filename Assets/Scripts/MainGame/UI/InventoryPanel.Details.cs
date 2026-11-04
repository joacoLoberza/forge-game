using System.Text;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    public partial class InventoryPanel
    {
        private void RenderRightPage(object item)
        {
            UpdateEquipButtonUI(item as WeaponStats);

            if (item == null)
            {
                if (emptySelectionNotice != null) emptySelectionNotice.SetActive(true);
                if (rightItemTitleText != null) rightItemTitleText.text = "";
                if (rightItemCategoryText != null) rightItemCategoryText.text = "";
                if (rightItemDescriptionText != null) rightItemDescriptionText.text = "";
                return;
            }

            if (emptySelectionNotice != null) emptySelectionNotice.SetActive(false);

            if (item is System.Collections.Generic.KeyValuePair<string, int> min)
            {
                RenderMineralDetails(min.Key, min.Value);
            }
            else if (item is BloomData bloom)
            {
                RenderBloomDetails(bloom);
            }
            else if (item is IngotData ingot)
            {
                RenderIngotDetails(ingot);
            }
            else if (item is HandleGroupData group)
            {
                RenderHandleDetails(group.Handle, group.Count);
            }
            else if (item is HandleData handle)
            {
                RenderHandleDetails(handle, 1);
            }
            else if (item is WeaponStats weapon)
            {
                RenderWeaponDetails(weapon);
            }
        }

        private void RenderMineralDetails(string id, int count)
        {
            string name = RawMaterialCatalog.GetDisplayName(id);
            SetRightHeader(name, "Basic Resource / Fuel");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>Amount in possession:</b> {count} units\n");

            if (id == "Iron")
            {
                sb.AppendLine("<b>Base Metal: Iron</b>");
                sb.AppendLine("• Base damage: <b>20</b> (Balanced)");
                sb.AppendLine("• Attack speed: <b>1.0x</b> (Standard)");
                sb.AppendLine("• Sharpness / Durability: <b>100</b> (High durability)");
                sb.AppendLine("• Optimum smelting range: <b>780°C - 1000°C</b>");
                sb.AppendLine("\n<i>The backbone of any alloy. Provides consistency and durability.</i>");
            }
            else if (id == "Copper")
            {
                sb.AppendLine("<b>Base Metal: Copper</b>");
                sb.AppendLine("• Base damage: <b>14</b> (Light)");
                sb.AppendLine("• Attack speed: <b>1.25x</b> (+25% faster)");
                sb.AppendLine("• Sharpness / Durability: <b>75</b>");
                sb.AppendLine("• Optimum smelting range: <b>560°C - 780°C</b>");
                sb.AppendLine("\n<i>Light and ductile metal. Ideal for fast-striking weapons.</i>");
            }
            else if (id == "Gold")
            {
                sb.AppendLine("<b>Precious Metal: Gold</b>");
                sb.AppendLine("• Base damage: <b>26</b> (Highest impact)");
                sb.AppendLine("• Attack speed: <b>1.05x</b>");
                sb.AppendLine("• Sharpness / Durability: <b>50</b> (Soft metal, dulls faster)");
                sb.AppendLine("• Optimum smelting range: <b>670°C - 890°C</b>");
                sb.AppendLine("\n<i>Dense and destructive metal. Raises damage at the cost of losing sharpness quickly.</i>");
            }
            else if (id == "Coal")
            {
                sb.AppendLine("<b>Mineral Fuel</b>");
                sb.AppendLine("• Heat power: <b>+150°C</b> per unit");
                sb.AppendLine("• Usage: Fuels the furnace to reach optimum temperatures.");
            }

            SetRightDescription(sb.ToString());
        }

        private void RenderBloomDetails(BloomData bloom)
        {
            SetRightHeader(bloom.DisplayName, $"Smelted Bloom • {bloom.SizeLabel}");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>Purity:</b> <color={bloom.PurityColorHex}>{bloom.PurityPercentage}% [{bloom.PurityRank}]</color>");
            sb.AppendLine($"<b>Size:</b> {bloom.SizeLabel}\n");
            sb.AppendLine($"<b>{bloom.composition.GetCompositionDescription()}</b>\n");
            sb.AppendLine("<b>Recommended Next Step:</b>");
            sb.AppendLine("Take this Bloom to the <b>Mechanical Press</b> to consolidate it into a uniform Ingot.");
            SetRightDescription(sb.ToString());
        }

        private void RenderIngotDetails(IngotData ingot)
        {
            SetRightHeader(ingot.DisplayName, $"Forged Ingot • {ingot.SizeLabel}");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<b>Purity:</b> <color={ingot.PurityColorHex}>{ingot.PurityPercentage}% [{ingot.PurityRank}]</color>");
            sb.AppendLine($"<b>Size:</b> {ingot.SizeLabel}\n");
            sb.AppendLine($"<b>{ingot.composition.GetCompositionDescription()}</b>\n");
            sb.AppendLine("<b>Recommended Next Step:</b>");
            sb.AppendLine("Take this Ingot to the <b>Anvil</b> to shape and assemble your weapon.");
            SetRightDescription(sb.ToString());
        }

        private void RenderHandleDetails(HandleData handle, int count = 1)
        {
            if (handle == null) return;
            string title = count > 1 ? $"{handle.DisplayName} x{count}" : handle.DisplayName;
            SetRightHeader(title, "Weapon Handle");
            StringBuilder sb = new StringBuilder();
            if (count > 1) sb.AppendLine($"<b>Cantidad en posesión:</b> {count} unidades\n");
            sb.AppendLine($"<b>Length:</b> {handle.length}");
            sb.AppendLine($"• Reach Multiplier: <b>x{handle.rangeModifier:F2}</b>");
            string speedSign = handle.speedModifier >= 0 ? "+" : "";
            sb.AppendLine($"• Speed Modifier: <b>{speedSign}{handle.speedModifier * 100:F0}%</b>\n");
            sb.AppendLine("<i>Select the handle at the Anvil to balance reach against agility.</i>");
            SetRightDescription(sb.ToString());
        }

        private void RenderWeaponDetails(WeaponStats weapon)
        {
            SetRightHeader(weapon.DisplayName, $"Finished Weapon • {weapon.weaponType}");
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"<b>Effective Damage:</b> <size=120%><color=#9E5B00>{weapon.EffectiveDamage:F1}</color></size> (Base: {weapon.baseDamage:F1})");
            sb.AppendLine($"<b>Remaining Sharpness:</b> {weapon.currentSharpness:F0}/{weapon.maxSharpness:F0} ({weapon.SharpnessRatio * 100:F0}%)");
            sb.AppendLine($"<b>Attack Speed:</b> {weapon.attackSpeed:F2} hits/sec");
            sb.AppendLine($"<b>Reach:</b> {weapon.attackRange:F2} m\n");

            sb.AppendLine($"<b>Type:</b> {weapon.weaponType} | <b>Handle:</b> {weapon.handleLength}");
            sb.AppendLine($"<b>Blade Purity:</b> {weapon.PurityPercentage}% [{weapon.qualityPrefix}]");
            sb.AppendLine($"<b>{weapon.composition.GetCompositionDescription()}</b>\n");

            sb.AppendLine("<size=85%><color=#5C4632>Nota: En combate el filo disminuye gradualmente hasta un 40%, pero las armas nunca se rompen.</color></size>");
            SetRightDescription(sb.ToString());
        }

        private void SetRightHeader(string title, string category)
        {
            if (rightItemTitleText != null) rightItemTitleText.text = title;
            if (rightItemCategoryText != null) rightItemCategoryText.text = category;
        }

        private void SetRightDescription(string description)
        {
            if (rightItemDescriptionText == null) return;
            rightItemDescriptionText.text = description;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rightItemDescriptionText.rectTransform);
            if (rightScrollRect != null && rightScrollRect.content != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rightScrollRect.content);
                rightScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void UpdateTabVisuals()
        {
            SetTab(tabMineralsButton, _currentCategory == ForgeItemCategory.RawMaterial);
            SetTab(tabBloomsButton, _currentCategory == ForgeItemCategory.Bloom);
            SetTab(tabIngotsButton, _currentCategory == ForgeItemCategory.Ingot);
            SetTab(tabHandlesButton, _currentCategory == ForgeItemCategory.Handle);
            SetTab(tabWeaponsButton, _currentCategory == ForgeItemCategory.Weapon);
        }

        private void SetTab(UnityEngine.UI.Button btn, bool active)
        {
            if (btn == null) return;
            var ind = btn.GetComponent<BookTabIndicator>();
            if (ind != null) { ind.SetIndicatorActive(active); return; }
            var txt = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt == null) return;
            txt.color = active ? new Color(1f, 0.80f, 0.20f) : new Color(0.70f, 0.70f, 0.70f);
            txt.fontStyle = active ? TMPro.FontStyles.Bold | TMPro.FontStyles.Underline : TMPro.FontStyles.Normal;
        }

        private bool IsSameItem(object a, object b)
        {
            if (a == null || b == null) return false;
            if (a is System.Collections.Generic.KeyValuePair<string, int> kvA && b is System.Collections.Generic.KeyValuePair<string, int> kvB)
                return kvA.Key == kvB.Key;
            if (a is IForgeItem fA && b is IForgeItem fB)
                return fA.Id == fB.Id;
            return Equals(a, b);
        }

        private (Sprite icon, Color color) GetItemIconAndColor(object itemData)
        {
            var db = ForjaGame.Data.ForgeSpriteDatabase.Instance;
            if (itemData is System.Collections.Generic.KeyValuePair<string, int> min)
            {
                Sprite s = db?.GetMineralSprite(min.Key);
                Color c = System.Enum.TryParse<MetalType>(min.Key, true, out MetalType m)
                    ? MetalProperties.GetMetalColor(m)
                    : Color.white;
                return (s, c);
            }
            if (itemData is BloomData bloom)
            {
                Sprite s = db?.defaultBloomSprite;
                Color c = bloom.composition != null
                    ? MetalProperties.GetMetalColor(bloom.composition.CalculateWeightedProperties().metalType)
                    : Color.gray;
                return (s, c);
            }
            if (itemData is IngotData ingot)
            {
                Sprite s = db?.defaultIngotSprite;
                Color c = ingot.composition != null
                    ? MetalProperties.GetMetalColor(ingot.composition.CalculateWeightedProperties().metalType)
                    : Color.gray;
                return (s, c);
            }
            if (itemData is HandleData handle)
            {
                Sprite s = db?.GetHandleSprite(handle.length);
                return (s, new Color(0.6f, 0.45f, 0.3f, 1f));
            }
            if (itemData is WeaponStats weapon)
            {
                Sprite s = db?.GetWeaponSprite(weapon.weaponType);
                Color c = weapon.composition != null
                    ? MetalProperties.GetMetalColor(weapon.composition.CalculateWeightedProperties().metalType)
                    : Color.white;
                return (s, c);
            }
            return (null, Color.white);
        }
    }
}

