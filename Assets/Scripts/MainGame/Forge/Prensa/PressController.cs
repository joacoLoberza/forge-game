using System;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Mechanical press controller: transforms a Bloom into an Ingot.
    /// Interactive button-mashing mechanic with smooth exponential resistance scaling.
    /// </summary>
    public class PressController : MonoBehaviour
    {
        [Header("Press Calibration")]
        [Tooltip("Base progress advance per push.")]
        [SerializeField] private float basePushAmount = 0.075f;

        [Tooltip("Base decay speed at which the piston retreats if the player stops mashing.")]
        [SerializeField] private float baseDecayRate = 0.13f;

        private BloomData _currentBloom;
        private float _currentProgress = 0f;
        private bool _isCompleted = false;

        public bool HasBloom => _currentBloom != null;
        public bool IsCompleted => _isCompleted;
        public float PressProgress => _currentProgress;
        public BloomData CurrentBloom => _currentBloom;
        public float CurrentResistance => _currentBloom != null ? _currentBloom.resistance : 40f;

        public event Action<float> PressProgressChanged;
        public event Action<IngotData> PressCompleted;
        public event Action PressReset;

        private void Update()
        {
            if (_currentBloom != null && !_isCompleted && _currentProgress > 0f)
            {
                float resistanceFactor = CurrentResistance / 40f;
                // Progressive resistance centered around range 35-44
                float difficultyDecay = 1f + Mathf.Pow(_currentProgress, 2.0f) * 1.15f;
                float decay = baseDecayRate * resistanceFactor * difficultyDecay * Time.deltaTime;

                _currentProgress = Mathf.Max(0f, _currentProgress - decay);
                PressProgressChanged?.Invoke(_currentProgress);
            }
        }

        public bool InsertBloom(BloomData bloom)
        {
            if (bloom == null || _currentBloom != null) return false;

            _currentBloom = bloom;
            _currentProgress = 0f;
            _isCompleted = false;
            PressProgressChanged?.Invoke(0f);
            return true;
        }

        public void PushPress()
        {
            if (_currentBloom == null || _isCompleted) return;

            // Generous initial push smoothly scaling towards 100%
            float progressFactor = Mathf.Lerp(1.0f, 0.48f, _currentProgress);
            float resistanceFactor = Mathf.Clamp(40f / CurrentResistance, 0.80f, 1.25f);
            float push = basePushAmount * progressFactor * resistanceFactor;

            _currentProgress = Mathf.Clamp01(_currentProgress + push);
            ForjaGame.Audio.ForgeAudioSystem.PlayPressCrush(transform.position);
            PressProgressChanged?.Invoke(_currentProgress);

            if (_currentProgress >= 1.0f)
            {
                _isCompleted = true;
                IngotData ingot = new IngotData(_currentBloom);
                PressCompleted?.Invoke(ingot);
            }
        }

        public IngotData ExtractIngot()
        {
            if (!_isCompleted || _currentBloom == null) return null;

            IngotData ingot = new IngotData(_currentBloom);
            _currentBloom = null;
            _isCompleted = false;
            _currentProgress = 0f;
            PressReset?.Invoke();
            PressProgressChanged?.Invoke(0f);
            return ingot;
        }

        public BloomData CancelAndRetrieveBloom()
        {
            if (_currentBloom == null || _isCompleted) return null;

            BloomData retrieved = _currentBloom;
            _currentBloom = null;
            _currentProgress = 0f;
            _isCompleted = false;
            PressReset?.Invoke();
            PressProgressChanged?.Invoke(0f);
            return retrieved;
        }
    }
}
