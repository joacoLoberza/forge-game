using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using ForjaGame.Forge;

namespace ForjaGame.Audio
{
    /// <summary>
    /// Submódulo de carga de pistas musicales y efectos de sonido externos reales desde Assets/Resources/Audio/:
    /// - Carga música medieval real (Kevin MacLeod) y todos los efectos de sonido descargados (Flare RPG & Battle for Wesnoth).
    /// - Gestiona fuego ambiental 3D y música dinámica (Forja vs Cueva/Jefe).
    /// </summary>
    public partial class ForgeAudioSystem
    {
        [Header("Optional Custom Music Clips")]
        [SerializeField] private AudioClip forgeMusicClip, combatMusicClip;

        private AudioClip _procForgeMusicLoop, _procCombatMusicLoop, _procPressLoop;
        private AudioClip _procFanfareGreat, _procFanfareGood, _procFanfarePoor;
        private AudioClip _extBladeMountFit, _extWeaponClaimed;
        private AudioSource _forgeMusicSource, _combatMusicSource, _anvilBrazierLoop, _pressLoopSource;
        private FurnaceController _cachedFurnace;
        private AnvilController _cachedAnvil;
        private float _nextStationLookupTime, _pressLoopActiveUntil, _nextPressImpactTime;

        private void SetupMusicAndStationFireSources()
        {
            TryLoadResourcesAudioBank();

            string sName = SceneManager.GetActiveScene().name;
            bool inMenu = !string.IsNullOrEmpty(sName) && sName.ToLower().Contains("menu");
            _forgeMusicSource = gameObject.AddComponent<AudioSource>();
            _forgeMusicSource.clip = forgeMusicClip != null ? forgeMusicClip : _procForgeMusicLoop;
            _forgeMusicSource.loop = true; _forgeMusicSource.spatialBlend = 0f;
            _forgeMusicSource.ignoreListenerPause = true;
            _forgeMusicSource.volume = inMenu ? 0.38f : 0.22f;
            _forgeMusicSource.Play();

            _pressLoopSource = gameObject.AddComponent<AudioSource>();
            _pressLoopSource.clip = _procPressLoop;
            _pressLoopSource.loop = true; _pressLoopSource.spatialBlend = 0f; _pressLoopSource.volume = 0f;

            _combatMusicSource = gameObject.AddComponent<AudioSource>();
            _combatMusicSource.clip = combatMusicClip != null ? combatMusicClip : _procCombatMusicLoop;
            _combatMusicSource.loop = true; _combatMusicSource.spatialBlend = 0f; _combatMusicSource.volume = 0f;
            _combatMusicSource.Play();

            var anvilFireObj = new GameObject("AnvilFireAmbience");
            anvilFireObj.transform.SetParent(transform);
            anvilFireObj.transform.position = new Vector3(-9.60f, 1.15f, 13.80f);
            _anvilBrazierLoop = anvilFireObj.AddComponent<AudioSource>();
            _anvilBrazierLoop.clip = _procFireLoop; _anvilBrazierLoop.loop = true; _anvilBrazierLoop.spatialBlend = 0.25f;
            _anvilBrazierLoop.minDistance = 8.5f; _anvilBrazierLoop.maxDistance = 32f;
            _anvilBrazierLoop.pitch = 0.96f; _anvilBrazierLoop.volume = 0.36f;
            if (_anvilBrazierLoop.clip != null) _anvilBrazierLoop.Play();

            if (_forgeHearthLoop != null && _procFireLoop != null)
            {
                _forgeHearthLoop.clip = _procFireLoop;
                _forgeHearthLoop.spatialBlend = 0.12f;
                if (!_forgeHearthLoop.isPlaying) _forgeHearthLoop.Play();
            }
            if (_caveAmbienceLoop != null && _procCaveLoop != null)
            {
                _caveAmbienceLoop.clip = _procCaveLoop;
                _caveAmbienceLoop.Play();
            }

            StartCoroutine(LoadExternalAudioBankRoutine());
            StartCoroutine(LoadDiskPageTurnFallbacks());
        }

        private static AudioClip LoadRes(string name, AudioClip fallback)
        {
            AudioClip loaded = UnityEngine.Resources.Load<AudioClip>("Audio/" + name);
            if (loaded == null) Debug.LogWarning($"[ForgeAudio] Missing Resources/Audio/{name}");
            return loaded != null ? loaded : fallback;
        }

        private void TryLoadResourcesAudioBank()
        {
            forgeMusicClip = LoadRes("ForgeMusic", forgeMusicClip);
            combatMusicClip = LoadRes("CombatMusic", combatMusicClip);
            _procItemSelect = LoadRes("OreSelect", _procItemSelect);
            _procItemPickup = LoadRes("ItemPickup", _procItemPickup);
            _extBladeMountFit = LoadRes("BladeMountFit", _procHammerGreat);
            _extWeaponClaimed = LoadRes("WeaponClaimed", _procFanfareGreat);
            _procFanfareGreat = LoadRes("PieceCompleteGreat", _procFanfareGreat);
            _procFanfareGood = LoadRes("PieceCompleteGood", _procFanfareGood);
            _procFanfarePoor = LoadRes("PieceCompletePoor", _procFanfarePoor);
            _procFireLoop = LoadRes("FireLoop", _procFireLoop);
            _procCaveLoop = LoadRes("CaveLoop", _procCaveLoop);
            _procFurnaceStoke = LoadRes("FurnaceBellows", _procFurnaceStoke);
            _procCoalStoke = LoadRes("CoalStoke", _procHitStone);
            _procPressCrush = LoadRes("PressCrush", _procPressCrush);
            _procPressLoop = LoadRes("PressLoop", _procPressLoop);
            _procHammerGreat = LoadRes("HammerGreat", _procHammerGreat);
            _procHammerStrike = LoadRes("HammerNormal", _procHammerStrike);
            _procHammerPoor = LoadRes("HammerPoor", _procHammerPoor);
            _procChiselCut = LoadRes("ChiselCut", _procChiselCut);
            _procWoodCut = LoadRes("WoodCut", _procWoodCut);
            _procPolishScrape = LoadRes("PolishScrape", _procPolishScrape);
            _procQuenchSizzle = LoadRes("QuenchSizzle", _procQuenchSizzle);
            _procSwingLight = LoadRes("SwingLight", _procSwingLight);
            _procSwingHeavy = LoadRes("SwingHeavy", _procSwingHeavy); _procSwingPickaxe = LoadRes("PickaxeSwing", _procSwingHeavy);
            _procUiClick = LoadRes("UIClick", _procUiClick); _procUiTab = LoadRes("UITab", _procUiTab);
            _procHitFlesh = LoadRes("HitFlesh", _procHitFlesh);
            _procHitStone = LoadRes("HitStone", _procHitStone);
            _procWallWood = LoadRes("HitWood", _procWallWood);
            _procPickaxe = LoadRes("PickaxeHit", _procPickaxe);
            _procPlayerHurt = LoadRes("PlayerHurt", _procPlayerHurt);
            _procAltarChord = LoadRes("AltarVictory", _procAltarChord);
            _procSpiderAggro = LoadRes("SpiderAggro", _procSpiderAggro);
            _procSpiderAttack = LoadRes("SpiderAttack", _procSpiderAttack);
            _procSpiderHurt = LoadRes("SpiderHurt", _procSpiderHurt);
            _procGolemAggro = LoadRes("GolemAggro", _procGolemAggro);
            _procGolemAttack = LoadRes("GolemAttack", _procGolemAttack);
            _procGolemHurt = LoadRes("GolemHurt", _procGolemHurt);
            _procGolemStone = LoadRes("GolemStone", _procHitStone);
            _procTitanAggro = LoadRes("TitanAggro1", _procTitanAggro);
            _procTitanAggro2 = LoadRes("TitanAggro2", _procTitanAggro2);
            _procTitanAttack = LoadRes("TitanAttack1", _procTitanAttack);
            _procTitanAttack2 = LoadRes("TitanAttack2", _procTitanAttack2);
            _procTitanHurt = LoadRes("TitanHurt", _procTitanHurt);
        }

        private IEnumerator LoadExternalAudioBankRoutine()
        {
            if (forgeMusicClip == null) yield return TryLoadDiskAudio("ForgeMusic.mp3", AudioType.MPEG, c => { forgeMusicClip = c; if (_forgeMusicSource != null) { _forgeMusicSource.clip = c; _forgeMusicSource.Play(); } });
            if (combatMusicClip == null) yield return TryLoadDiskAudio("CombatMusic.mp3", AudioType.MPEG, c => { combatMusicClip = c; if (_combatMusicSource != null) { _combatMusicSource.clip = c; _combatMusicSource.Play(); } });
            if (_procFireLoop == null) yield return TryLoadDiskAudio("FireLoop.wav", AudioType.WAV, c => { _procFireLoop = c; if (_forgeHearthLoop != null) { _forgeHearthLoop.clip = c; _forgeHearthLoop.Play(); } if (_anvilBrazierLoop != null) { _anvilBrazierLoop.clip = c; _anvilBrazierLoop.Play(); } });
            if (_procCaveLoop == null) yield return TryLoadDiskAudio("CaveLoop.ogg", AudioType.OGGVORBIS, c => { _procCaveLoop = c; if (_caveAmbienceLoop != null) { _caveAmbienceLoop.clip = c; _caveAmbienceLoop.Play(); } });
            if (_procItemSelect == null) yield return TryLoadDiskAudio("OreSelect.wav", AudioType.WAV, c => _procItemSelect = c);
            if (_procItemPickup == null) yield return TryLoadDiskAudio("ItemPickup.ogg", AudioType.OGGVORBIS, c => _procItemPickup = c);
            if (_extBladeMountFit == null) yield return TryLoadDiskAudio("BladeMountFit.ogg", AudioType.OGGVORBIS, c => _extBladeMountFit = c);
            if (_extWeaponClaimed == null) yield return TryLoadDiskAudio("WeaponClaimed.ogg", AudioType.OGGVORBIS, c => _extWeaponClaimed = c);
            if (_procFanfareGreat == null) yield return TryLoadDiskAudio("PieceCompleteGreat.wav", AudioType.WAV, c => _procFanfareGreat = c);
            if (_procFanfareGood == null) yield return TryLoadDiskAudio("PieceCompleteGood.ogg", AudioType.OGGVORBIS, c => _procFanfareGood = c);
            if (_procFanfarePoor == null) yield return TryLoadDiskAudio("PieceCompletePoor.ogg", AudioType.OGGVORBIS, c => _procFanfarePoor = c);
            if (_procFurnaceStoke == null) yield return TryLoadDiskAudio("FurnaceBellows.wav", AudioType.WAV, c => _procFurnaceStoke = c);
            if (_procCoalStoke == null) yield return TryLoadDiskAudio("CoalStoke.ogg", AudioType.OGGVORBIS, c => _procCoalStoke = c);
            if (_procPressCrush == null) yield return TryLoadDiskAudio("PressCrush.wav", AudioType.WAV, c => _procPressCrush = c);
            if (_procPressLoop == null) yield return TryLoadDiskAudio("PressLoop.wav", AudioType.WAV, c => { _procPressLoop = c; if (_pressLoopSource != null) _pressLoopSource.clip = c; });
            if (_procHammerGreat == null) yield return TryLoadDiskAudio("HammerGreat.wav", AudioType.WAV, c => _procHammerGreat = c);
            if (_procHammerStrike == null) yield return TryLoadDiskAudio("HammerNormal.wav", AudioType.WAV, c => _procHammerStrike = c);
            if (_procHammerPoor == null) yield return TryLoadDiskAudio("HammerPoor.wav", AudioType.WAV, c => _procHammerPoor = c);
            if (_procChiselCut == null) yield return TryLoadDiskAudio("ChiselCut.wav", AudioType.WAV, c => _procChiselCut = c);
            if (_procWoodCut == null) yield return TryLoadDiskAudio("WoodCut.ogg", AudioType.OGGVORBIS, c => _procWoodCut = c);
            if (_procPolishScrape == null) yield return TryLoadDiskAudio("PolishScrape.wav", AudioType.WAV, c => _procPolishScrape = c);
            if (_procQuenchSizzle == null) yield return TryLoadDiskAudio("QuenchSizzle.wav", AudioType.WAV, c => _procQuenchSizzle = c);
            if (_procSwingLight == null) yield return TryLoadDiskAudio("SwingLight.ogg", AudioType.OGGVORBIS, c => _procSwingLight = c);
            if (_procSwingHeavy == null) yield return TryLoadDiskAudio("SwingHeavy.ogg", AudioType.OGGVORBIS, c => _procSwingHeavy = c);
            if (_procHitFlesh == null) yield return TryLoadDiskAudio("HitFlesh.ogg", AudioType.OGGVORBIS, c => _procHitFlesh = c);
            if (_procHitStone == null) yield return TryLoadDiskAudio("HitStone.ogg", AudioType.OGGVORBIS, c => _procHitStone = c);
            if (_procWallWood == null) yield return TryLoadDiskAudio("HitWood.ogg", AudioType.OGGVORBIS, c => _procWallWood = c);
            if (_procPickaxe == null) yield return TryLoadDiskAudio("PickaxeHit.wav", AudioType.WAV, c => _procPickaxe = c);
            if (_procPlayerHurt == null) yield return TryLoadDiskAudio("PlayerHurt.ogg", AudioType.OGGVORBIS, c => _procPlayerHurt = c);
            if (_procAltarChord == null) yield return TryLoadDiskAudio("AltarVictory.ogg", AudioType.OGGVORBIS, c => _procAltarChord = c);
            if (_procSpiderAggro == null) yield return TryLoadDiskAudio("SpiderAggro.wav", AudioType.WAV, c => _procSpiderAggro = c);
            if (_procSpiderAttack == null) yield return TryLoadDiskAudio("SpiderAttack.ogg", AudioType.OGGVORBIS, c => _procSpiderAttack = c);
            if (_procSpiderHurt == null) yield return TryLoadDiskAudio("SpiderHurt.wav", AudioType.WAV, c => _procSpiderHurt = c);
            if (_procGolemAggro == null) yield return TryLoadDiskAudio("GolemAggro.wav", AudioType.WAV, c => _procGolemAggro = c);
            if (_procGolemAttack == null) yield return TryLoadDiskAudio("GolemAttack.wav", AudioType.WAV, c => _procGolemAttack = c);
            if (_procGolemHurt == null) yield return TryLoadDiskAudio("GolemHurt.wav", AudioType.WAV, c => _procGolemHurt = c);
            if (_procGolemStone == null) yield return TryLoadDiskAudio("GolemStone.ogg", AudioType.OGGVORBIS, c => _procGolemStone = c);
            if (_procTitanAggro == null) yield return TryLoadDiskAudio("TitanAggro1.ogg", AudioType.OGGVORBIS, c => _procTitanAggro = c);
            if (_procTitanAggro2 == null) yield return TryLoadDiskAudio("TitanAggro2.ogg", AudioType.OGGVORBIS, c => _procTitanAggro2 = c);
            if (_procTitanAttack == null) yield return TryLoadDiskAudio("TitanAttack1.ogg", AudioType.OGGVORBIS, c => _procTitanAttack = c);
            if (_procTitanAttack2 == null) yield return TryLoadDiskAudio("TitanAttack2.ogg", AudioType.OGGVORBIS, c => _procTitanAttack2 = c);
            if (_procTitanHurt == null) yield return TryLoadDiskAudio("TitanHurt.ogg", AudioType.OGGVORBIS, c => _procTitanHurt = c);
        }

        private IEnumerator TryLoadDiskAudio(string fileName, AudioType type, System.Action<AudioClip> onLoaded)
        {
            string fullPath = Path.Combine(Application.dataPath, "Resources/Audio", fileName);
            if (!File.Exists(fullPath)) yield break;
            using var req = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(fullPath).AbsoluteUri, type);
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null && clip.length > 0.01f) { clip.name = "Ext_" + fileName; onLoaded?.Invoke(clip); }
            }
        }

        private void UpdateDynamicMusicAndStationFires()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool isMainMenu = !string.IsNullOrEmpty(sceneName) && sceneName.ToLower().Contains("menu");
            Camera cam = Camera.main;
            Vector3 listenerPos = cam != null ? cam.transform.position : Vector3.zero;
            bool inCave = !isMainMenu && listenerPos.z > 26.5f;
            bool inBossArena = !isMainMenu && listenerPos.z > 148f;

            if (_forgeMusicSource != null)
            {
                if (!_forgeMusicSource.isPlaying && _forgeMusicSource.clip != null) _forgeMusicSource.Play();
                float targetForgeMusic = isMainMenu ? 0.38f : (inCave ? 0f : 0.21f);
                _forgeMusicSource.volume = Mathf.MoveTowards(_forgeMusicSource.volume, targetForgeMusic, Time.unscaledDeltaTime * 0.45f);
            }
            if (_pressLoopSource != null)
            {
                bool activePress = !isMainMenu && Time.unscaledTime < _pressLoopActiveUntil;
                if (_pressLoopSource.clip == null && _procPressLoop != null) _pressLoopSource.clip = _procPressLoop;
                float targetPress = activePress ? 0.56f : 0f;
                _pressLoopSource.volume = Mathf.MoveTowards(_pressLoopSource.volume, targetPress, Time.unscaledDeltaTime * (activePress ? 5.0f : 3.2f));
                if (activePress && _pressLoopSource.clip != null && !_pressLoopSource.isPlaying) _pressLoopSource.Play();
                else if (!activePress && _pressLoopSource.volume <= 0.005f && _pressLoopSource.isPlaying) _pressLoopSource.Stop();
            }
            if (_combatMusicSource != null)
            {
                float targetCombatVol = (!isMainMenu && inCave) ? (inBossArena ? 0.28f : 0.22f) : 0f;
                _combatMusicSource.volume = Mathf.MoveTowards(_combatMusicSource.volume, targetCombatVol, Time.unscaledDeltaTime * 0.25f);
                _combatMusicSource.pitch = Mathf.MoveTowards(_combatMusicSource.pitch, inBossArena ? 1.05f : 1.0f, Time.unscaledDeltaTime * 0.15f);
            }

            if (Time.unscaledTime >= _nextStationLookupTime)
            {
                _nextStationLookupTime = Time.unscaledTime + 2.0f;
                if (_cachedFurnace == null) _cachedFurnace = FindFirstObjectByType<FurnaceController>();
                if (_cachedAnvil == null) _cachedAnvil = FindFirstObjectByType<AnvilController>();
            }
            if (_forgeHearthLoop != null)
            {
                if (_cachedFurnace != null) _forgeHearthLoop.transform.position = _cachedFurnace.transform.position + Vector3.up * 0.9f;
                if (_forgeHearthLoop.clip != _procFireLoop && _procFireLoop != null) _forgeHearthLoop.clip = _procFireLoop;
                float tempRatio = _cachedFurnace != null ? Mathf.Clamp01((_cachedFurnace.CurrentTemperature - 25f) / 1200f) : 0.25f;
                bool smelting = _cachedFurnace != null && _cachedFurnace.IsInOptimumRange;
                float targetFireVol = isMainMenu ? 0f : (inCave ? 0.08f : Mathf.Lerp(0.54f, 0.80f, smelting ? Mathf.Max(0.65f, tempRatio) : tempRatio));
                _forgeHearthLoop.volume = Mathf.MoveTowards(_forgeHearthLoop.volume, targetFireVol, Time.unscaledDeltaTime * 0.8f);
                _forgeHearthLoop.pitch = Mathf.Lerp(0.95f, 1.06f, tempRatio);
                if (isMainMenu && _forgeHearthLoop.isPlaying) _forgeHearthLoop.Stop();
                else if (!isMainMenu && _forgeHearthLoop.clip != null && !_forgeHearthLoop.isPlaying) _forgeHearthLoop.Play();
            }
            if (_anvilBrazierLoop != null)
            {
                if (_cachedAnvil != null) _anvilBrazierLoop.transform.position = _cachedAnvil.transform.position + Vector3.up * 0.9f;
                if (_anvilBrazierLoop.clip != _procFireLoop && _procFireLoop != null) _anvilBrazierLoop.clip = _procFireLoop;
                bool reheating = _cachedAnvil != null && _cachedAnvil.ThermalController != null && _cachedAnvil.ThermalController.IsReheating;
                float targetAnvilVol = isMainMenu ? 0f : (inCave ? 0.05f : (reheating ? 0.68f : 0.48f));
                _anvilBrazierLoop.volume = Mathf.MoveTowards(_anvilBrazierLoop.volume, targetAnvilVol, Time.unscaledDeltaTime * 1.0f);
                _anvilBrazierLoop.pitch = reheating ? 1.04f : 0.96f;
                if (isMainMenu && _anvilBrazierLoop.isPlaying) _anvilBrazierLoop.Stop();
                else if (!isMainMenu && _anvilBrazierLoop.clip != null && !_anvilBrazierLoop.isPlaying) _anvilBrazierLoop.Play();
            }
        }
    }
}
