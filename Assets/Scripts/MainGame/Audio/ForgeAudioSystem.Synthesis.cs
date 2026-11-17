using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ForjaGame.Audio
{
    internal sealed class ForgeButtonPointerHook : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            var btn = GetComponent<Button>();
            if (btn != null && !btn.interactable) return;
            ForgeAudioSystem.PlayUIClick();
        }
    }

    /// <summary>
    /// Enrutador de reproducción 2D/3D, clic original de UI y banco externo CC0 en Assets/Resources/Audio/.
    /// </summary>
    public partial class ForgeAudioSystem
    {
        private AudioClip _procUiClick, _procUiTab, _procItemSelect, _procItemPickup, _procSwingPickaxe;
        private AudioClip _procHammerGreat, _procHammerStrike, _procHammerPoor;
        private AudioClip _procChiselCut, _procWoodCut, _procPolishScrape, _procQuenchSizzle, _procPressCrush, _procFurnaceStoke, _procCoalStoke;
        private AudioClip _procSwingLight, _procSwingHeavy, _procHitFlesh, _procHitStone, _procWallWood, _procPickaxe;
        private AudioClip _procSpiderAggro, _procSpiderAttack, _procSpiderHurt;
        private AudioClip _procGolemAggro, _procGolemAttack, _procGolemHurt, _procGolemStone;
        private AudioClip _procTitanAggro, _procTitanAggro2, _procTitanAttack, _procTitanAttack2, _procTitanHurt;
        private AudioClip _procPlayerHurt, _procAltarChord;
        private AudioClip _procFireLoop, _procCaveLoop, _procPageTurn;
        private readonly AudioClip[] _extPageTurn = new AudioClip[3];
        private readonly AudioSource[] _stationChannels = new AudioSource[4];
        private int _poolCursor, _stationCursor, _lastUiFrame = -1, _pageTurnCursor;

        private void EnsureStationChannels()
        {
            if (_stationChannels[0] != null) return;
            for (int i = 0; i < _stationChannels.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.ignoreListenerPause = true;
                _stationChannels[i] = src;
            }
        }

        private void Play2D(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _uiSource == null) return;
            _uiSource.pitch = pitch;
            _uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void PlayStation2D(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) { Debug.LogWarning("[ForgeAudio] PlayStation2D skipped: null AudioClip."); return; }
            if (_uiSource != null)
            {
                _uiSource.pitch = pitch;
                _uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
                return;
            }
            EnsureStationChannels();
            AudioSource src = _stationChannels[_stationCursor];
            _stationCursor = (_stationCursor + 1) % _stationChannels.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void Play3D(AudioClip clip, Vector3 pos, float volume, float pitch)
        {
            if (clip == null || _pool.Count == 0) return;
            float dPl = ForjaGame.Core.PlayerMovement.LocalInstance != null
                ? Vector3.Distance(ForjaGame.Core.PlayerMovement.LocalInstance.transform.position, pos) : 999f;
            float dCam = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, pos) : 999f;
            float dist = Mathf.Min(dPl, dCam);
            if (dist >= 900f) dist = 0f;
            if (dist > 55f) return;
            float atten = Mathf.Clamp01(1f - Mathf.Max(0f, dist - 9f) / 44f);
            if (atten <= 0.02f) return;

            AudioSource src = _pool[_poolCursor];
            _poolCursor = (_poolCursor + 1) % _pool.Count;
            src.transform.position = pos;
            src.spatialBlend = 0f;
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume * atten));
        }

        private void BuildProceduralBank()
        {
            var rng = new System.Random(1337);
            _procUiClick = MakeClip("Proc_UIClick", 0.048f, (t, d) =>
                Mathf.Exp(-t * 115f) * (Mathf.Sin(t * 2f * Mathf.PI * 245f) * 0.65f + Mathf.Sin(t * 2f * Mathf.PI * 490f) * 0.25f));
            float lp = 0f;
            _procUiTab = MakeClip("Proc_UITab", 0.11f, (t, d) =>
            {
                lp = Mathf.Lerp(lp, (float)rng.NextDouble() * 2f - 1f, 0.22f);
                return Mathf.Sin(Mathf.PI * (t / d)) * (lp * 0.25f + Mathf.Sin(t * 2f * Mathf.PI * 340f) * Mathf.Exp(-t * 42f) * 0.55f);
            });
            float pLp = 0f;
            _procPageTurn = MakeClip("Proc_PageTurn", 0.24f, (t, d) =>
            {
                pLp = Mathf.Lerp(pLp, (float)rng.NextDouble() * 2f - 1f, 0.14f);
                return Mathf.Sin(Mathf.PI * (t / d)) * pLp * 0.42f;
            });
            LoadPageTurnBank();
            TryLoadResourcesAudioBank();
        }

        private void LoadPageTurnBank()
        {
            _extPageTurn[0] = UnityEngine.Resources.Load<AudioClip>("Audio/PageTurn") ?? _extPageTurn[0];
            _extPageTurn[1] = UnityEngine.Resources.Load<AudioClip>("Audio/PageTurn2") ?? _extPageTurn[1];
            _extPageTurn[2] = UnityEngine.Resources.Load<AudioClip>("Audio/PageTurn3") ?? _extPageTurn[2];
        }

        private System.Collections.IEnumerator LoadDiskPageTurnFallbacks()
        {
            string[] fNames = { "PageTurn.ogg", "PageTurn2.ogg", "PageTurn3.ogg" };
            for (int i = 0; i < fNames.Length; i++)
            {
                if (_extPageTurn[i] != null) continue;
                int idx = i;
                yield return TryLoadDiskAudio(fNames[idx], AudioType.OGGVORBIS, c => _extPageTurn[idx] = c);
            }
        }

        private void PlayPageTurnInternal()
        {
            if (Time.frameCount == _lastUiFrame) return;
            _lastUiFrame = Time.frameCount;
            AudioClip clip = null;
            for (int k = 0; k < _extPageTurn.Length; k++)
            {
                int idx = (_pageTurnCursor + k) % _extPageTurn.Length;
                if (_extPageTurn[idx] != null) { clip = _extPageTurn[idx]; _pageTurnCursor = (idx + 1) % _extPageTurn.Length; break; }
            }
            Play2D(clip ?? _procPageTurn, 0.92f, Random.Range(0.95f, 1.05f));
        }

        private static AudioClip MakeClip(string clipName, float duration, System.Func<float, float, float> sampleFn)
        {
            const int sr = 44100;
            int n = Mathf.Max(64, Mathf.RoundToInt(duration * sr));
            float[] data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sampleFn(i / (float)sr, duration), -0.95f, 0.95f);
            var clip = AudioClip.Create(clipName, n, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
