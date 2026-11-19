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
                if (emptySelectionNotice != null)
                {
                    ForgeScrollViewStyler.StyleEmptyNotice(emptySelectionNotice, "No item selected.\nChoose an item from the inventory.");
                    emptySelectionNotice.SetActive(true);
                }
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

        private const string Div = "<color=#9E7E5B>────────────────────────────────</color>";

        private void RenderMineralDetails(string id, int count)
        {
            SetRightHeader(RawMaterialCatalog.GetDisplayName(id), $"Raw Material  •  {count} in pouch");
            var sb = new StringBuilder();
            sb.AppendLine($"<color=#5C4026><b>STOCK:</b></color> <b>{count}</b> units\n{Div}");
            if (id == "Iron")
                AppendSpecBlock(sb, "Base Structural Metal", new[] { ("Damage Base", "20 (Balanced)"), ("Swing Speed", "1.00x Standard"), ("Edge Longevity", "100 (High Durability)"), ("Smelt Window", "780°C – 1000°C") }, "Backbone of sturdy blades and Bastion alloys.");
            else if (id == "Copper")
                AppendSpecBlock(sb, "Light Conductive Metal", new[] { ("Damage Base", "14 (Light)"), ("Swing Speed", "1.25x (+25% Faster)"), ("Edge Longevity", "75 (Medium)"), ("Smelt Window", "560°C – 780°C") }, "Agile and ductile. Accelerates heavy weapons and pairs with Iron or Gold.");
            else if (id == "Gold")
                AppendSpecBlock(sb, "Heavy Precious Metal", new[] { ("Damage Base", "26 (Highest Impact)"), ("Swing Speed", "1.05x (Agile)"), ("Edge Longevity", "50 (Soft Edge)"), ("Smelt Window", "670°C – 890°C") }, "Maximizes strike damage; alloy with Iron or Copper to unlock passives.");
            else if (id == "Coal")
                AppendSpecBlock(sb, "Furnace Combustible", new[] { ("Thermal Output", "+150°C per chunk"), ("Station", "Smelting Furnace") }, "Feed coal cleanly to hold molten ore inside its optimum alloy window.");
            SetRightDescription(sb.ToString());
        }

        private void RenderBloomDetails(BloomData bloom)
        {
            SetRightHeader(bloom.DisplayName, $"Smelted Bloom  •  {bloom.SizeLabel}");
            var sb = new StringBuilder();
            sb.AppendLine("<color=#5C4026><b>QUALITY & COMPOSITION</b></color>");
            sb.AppendLine($"  ▸ <b>Purity:</b> <color={bloom.PurityColorHex}><b>{bloom.PurityPercentage}%</b> [{bloom.PurityRank}]</color>");
            sb.AppendLine($"  ▸ <b>Size:</b> {bloom.SizeLabel}   •   <b>Press Tier:</b> Lv.{bloom.DifficultyLevel}");
            sb.AppendLine($"  ▸ <b>Metals:</b> {bloom.composition.GetCompositionDescription()}\n{Div}");
            AppendAlloyPassivePreview(sb, bloom.composition);
            sb.AppendLine("\n<color=#5C4026><b>NEXT STATION:</b></color> Compress this Bloom at the <b>Mechanical Press</b> into a uniform Ingot.");
            SetRightDescription(sb.ToString());
        }

        private void RenderIngotDetails(IngotData ingot)
        {
            SetRightHeader(ingot.DisplayName, $"Forged Ingot  •  {ingot.SizeLabel}");
            var sb = new StringBuilder();
            sb.AppendLine("<color=#5C4026><b>METALLURGY & PURITY</b></color>");
            sb.AppendLine($"  ▸ <b>Purity:</b> <color={ingot.PurityColorHex}><b>{ingot.PurityPercentage}%</b> [{ingot.PurityRank}]</color>");
            sb.AppendLine($"  ▸ <b>Mass Class:</b> {ingot.SizeLabel}");
            sb.AppendLine($"  ▸ <b>Blend:</b> {ingot.composition?.GetCompositionDescription() ?? "Standard Forged Alloy"}\n{Div}");
            AppendAlloyPassivePreview(sb, ingot.composition);
            sb.AppendLine("\n<color=#5C4026><b>NEXT STATION:</b></color> Heat and hammer on the <b>Anvil</b> with a handle to forge a weapon.");
            SetRightDescription(sb.ToString());
        }

        private void RenderHandleDetails(HandleData handle, int count = 1)
        {
            if (handle == null) return;
            SetRightHeader(count > 1 ? $"{handle.DisplayName} x{count}" : handle.DisplayName, $"Weapon Hilt  •  {handle.length}");
            var sb = new StringBuilder();
            string sgn = handle.speedModifier >= 0 ? "+" : "";
            AppendSpecBlock(sb, $"Hilt Ergonomics ({handle.length})", new[] {
                ("Quantity", count > 1 ? $"{count} hilts" : "1 hilt"),
                ("Reach Factor", $"x{handle.rangeModifier:F2}"),
                ("Speed Offset", $"{sgn}{handle.speedModifier * 100:F0}%"),
                ("Grip Stance", handle.length == HandleLength.Long ? "Two-Handed (2H)" : "One-Handed (1H)")
            }, "Pair at the Anvil: Short hilts favor fast swings; Long hilts command wide 2H reach.");
            SetRightDescription(sb.ToString());
        }

        private void RenderWeaponDetails(WeaponStats weapon)
        {
            string stance = weapon.GetCombatStance() == WeaponCombatStance.OneHanded ? "1-Handed" : "2-Handed";
            SetRightHeader(weapon.DisplayName, $"{weapon.weaponType}  •  {stance}  •  {weapon.qualityPrefix}");
            var sb = new StringBuilder();
            float dps = weapon.EffectiveDamage * weapon.attackSpeed;
            sb.AppendLine("<color=#5C4026><b>COMBAT PERFORMANCE</b></color>");
            sb.AppendLine($"  ▸ <b>Damage:</b> <size=115%><color=#9E5B00><b>{weapon.EffectiveDamage:F1}</b></color></size> <size=90%>(Base {weapon.baseDamage:F1} • Est. {dps:F0} DPS)</size>");
            sb.AppendLine($"  ▸ <b>Speed:</b> <b>{weapon.attackSpeed:F2}</b> hits/s   •   <b>Reach:</b> <b>{weapon.attackRange:F2} m</b>");
            sb.AppendLine($"  ▸ <b>Edge:</b>  {BuildSharpBar(weapon.SharpnessRatio, 10)} <b>{weapon.SharpnessRatio * 100:F0}%</b> <size=85%>({weapon.currentSharpness:F0}/{weapon.maxSharpness:F0})</size>");
            sb.AppendLine($"\n{Div}\n<color=#5C4026><b>CRAFTING & ALLOY PASSIVE</b></color>");
            sb.AppendLine($"  ▸ <b>Build:</b> {weapon.ingotSize} Head + {weapon.handleLength} Hilt • Purity <b>{weapon.PurityPercentage}%</b>");
            sb.AppendLine($"  ▸ <b>Metals:</b> {weapon.composition.GetCompositionDescription()}");
            var passive = ForjaGame.Combat.AlloyPassiveResolver.Resolve(weapon);
            sb.AppendLine($"  ★ <color={passive.ColorHex}><b>{passive.PassiveName}:</b></color> {passive.Summary}");
            sb.AppendLine("\n<size=85%><color=#6B523B>Tip: Edge sharpness wears only down to 40% min; repair or reforge anytime.</color></size>");
            SetRightDescription(sb.ToString());
        }

        private static void AppendSpecBlock(StringBuilder sb, string heading, (string k, string v)[] rows, string tip)
        {
            sb.AppendLine($"<color=#5C4026><b>{heading.ToUpperInvariant()}</b></color>");
            foreach (var (k, v) in rows) sb.AppendLine($"  ▸ <b>{k}:</b> {v}");
            sb.AppendLine($"\n{Div}\n<size=92%><color=#4B3624><i>{tip}</i></color></size>");
        }

        private static void AppendAlloyPassivePreview(StringBuilder sb, AlloyComposition comp)
        {
            string matName = comp != null ? AlloyNameRegistry.ResolveAlloyMaterialName(comp.ingredients) : "Iron";
            var passive = ForjaGame.Combat.AlloyPassiveResolver.ResolveByAlloyName(matName);
            sb.AppendLine($"<color=#5C4026><b>ALLOY PROPERTY</b></color>");
            sb.AppendLine($"  ★ <color={passive.ColorHex}><b>{passive.PassiveName}:</b></color> {passive.Summary}");
        }

        private static string BuildSharpBar(float ratio, int slots)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(ratio * slots), 0, slots);
            return "<color=#9E5B00>" + new string('■', filled) + "</color><color=#B8A690>" + new string('□', slots - filled) + "</color>";
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

