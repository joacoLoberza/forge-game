using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.Core;

namespace ForjaGame.UI
{
    public static partial class ForgeCanvasThemeStyler
    {
        public static void StyleEndScreensAndMainMenu()
        {
            GameObject victoryObj = null;
            GameObject defeatObj = null;

            var glc = Object.FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);
            if (glc != null)
            {
                victoryObj = glc.VictoryPanel;
                defeatObj = glc.DefeatPanel;
            }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c == null) continue;
                foreach (var tr in c.GetComponentsInChildren<Transform>(true))
                {
                    if (victoryObj == null && (tr.name == "PanelVictory" || tr.name == "VictoryPanel" || tr.name == "VictoryOverlay"))
                        victoryObj = tr.gameObject;
                    if (defeatObj == null && (tr.name == "PanelDefeat" || tr.name == "DefeatPanel" || tr.name == "DefeatOverlay"))
                        defeatObj = tr.gameObject;
                }
            }

            if (victoryObj != null) StyleDecreeModal(victoryObj, "ALTAR DE LAS PROFUNDIDADES", true);
            if (defeatObj != null) StyleDecreeModal(defeatObj, "CAÍDO EN LA CAVERNA", false);
        }

        private static void StyleDecreeModal(GameObject modal, string title, bool victory)
        {
            if (modal == null) return;

            var bgImg = modal.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = null;
                bgImg.color = victory ? new Color(0.03f, 0.08f, 0.04f, 0.88f) : new Color(0.08f, 0.02f, 0.02f, 0.90f);
            }

            foreach (var legacyText in modal.GetComponentsInChildren<TMP_Text>(true))
            {
                if (legacyText.name == "WinText" || legacyText.name == "DefeatText")
                    legacyText.gameObject.SetActive(false);
            }

            var body = CreateOrGetParchmentCard(modal.transform, "DecreeBody", Vector2.zero, new Vector2(740f, 510f));
            CreateOrGetTitleBanner(body, "DecreeTitle", title, new Vector2(0f, 175f), new Vector2(620f, 58f), fontSize: 23f, topAnchored: false);

            Transform existingMsg = body.Find("DecreeMessage");
            GameObject msgGo = existingMsg != null ? existingMsg.gameObject : new GameObject("DecreeMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            msgGo.transform.SetParent(body, false);
            var mRt = msgGo.GetComponent<RectTransform>();
            mRt.anchorMin = mRt.anchorMax = mRt.pivot = new Vector2(0.5f, 0.5f);
            mRt.anchoredPosition = new Vector2(0f, 25f);
            mRt.sizeDelta = new Vector2(640f, 205f);

            var mTmp = msgGo.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(mTmp);
            mTmp.fontSize = 17f;
            mTmp.enableAutoSizing = true;
            mTmp.fontSizeMin = 13f;
            mTmp.fontSizeMax = 17.5f;
            mTmp.color = DarkInk;
            mTmp.alignment = TextAlignmentOptions.Center;

            string outcome = GameLoopController.LastOutcomeSummary;
            if (victory)
            {
                string extra = !string.IsNullOrEmpty(outcome) ? $"\n\n<color=#5C3310><b>{outcome}</b></color>" : "";
                mTmp.text = "<b>¡Has alcanzado y consagrado el Altar Ancestral de la Caverna!</b>\nRegresas a salvo al taller de la superficie con todo tu botín intacto." + extra;
            }
            else
            {
                string lost = !string.IsNullOrEmpty(outcome) ? $"\n\n<color=#7A1818><b>{outcome}</b></color>" : "";
                mTmp.text = "<b>Tus fuerzas han flaqueado en la oscuridad subterránea.</b>\nDespertarás junto al calor de la fragua, pero los minerales cosechados en esta incursión quedaron atrás." + lost;
            }

            foreach (var b in modal.GetComponentsInChildren<Button>(true))
            {
                b.transform.SetParent(body, false);
                StyleForgeButton(b, primary: true, body, new Vector2(0f, -170f), new Vector2(420f, 54f));

                var btnText = b.GetComponentInChildren<TMP_Text>(true);
                if (btnText != null)
                {
                    MedievalTypographyProvider.ApplyToTMP(btnText);
                    btnText.text = victory ? "REGRESAR AL TALLER CON EL BOTÍN" : "DESPERTAR EN EL TALLER";
                    btnText.fontSize = 16.5f;
                    btnText.enableAutoSizing = true;
                    btnText.fontSizeMin = 13f;
                    btnText.fontSizeMax = 17f;
                    btnText.fontStyle = FontStyles.Bold;
                    btnText.color = GoldTitleInk;
                }
            }

            MedievalTypographyProvider.ApplyToAllTMPInHierarchy(modal.transform);
        }
    }
}
