using System.Collections.Generic;
using RuneRush;
using UnityEngine;

namespace RuneRush.UnityRuntime
{
    public sealed class RaceAudioController : MonoBehaviour
    {
        private readonly Dictionary<RaceEventCode, AudioClip> _eventClips = new();
        private AudioSource _music;
        private AudioSource _sfx;
        private AudioSource _engineLow;
        private AudioSource _engineHigh;
        private AudioClip _pickup;
        private AudioClip _impact;
        private AudioClip _finish;

        public float MusicVolume { get; private set; } = 0.58f;
        public float SfxVolume { get; private set; } = 0.82f;
        public bool Muted { get; private set; }

        public void Initialize()
        {
            _eventClips.Clear();
            _music = GetOrCreateSource("Music", true, false);
            _sfx = GetOrCreateSource("SFX", false, false);
            _engineLow = GetOrCreateSource("Engine Low", true, true);
            _engineHigh = GetOrCreateSource("Engine High", true, true);

            _music.clip = Resources.Load<AudioClip>("Audio/fantasy-cup");
            _pickup = Resources.Load<AudioClip>("Audio/rune-pickup");
            _impact = Resources.Load<AudioClip>("Audio/metal-impact");
            _finish = Resources.Load<AudioClip>("Audio/crown-finish");
            _engineLow.clip = BuildEngineClip("Engine low loop", 62f, 0.6f);
            _engineHigh.clip = BuildEngineClip("Engine high loop", 124f, 0.32f);

            MapEvents();
            ApplyVolumes();
        }

        public void StartRaceAudio()
        {
            if (_music != null && _music.clip != null && !_music.isPlaying) _music.Play();
            if (_engineLow != null && !_engineLow.isPlaying) _engineLow.Play();
            if (_engineHigh != null && !_engineHigh.isPlaying) _engineHigh.Play();
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                _music?.Pause();
                _engineLow?.Pause();
                _engineHigh?.Pause();
            }
            else
            {
                if (_music != null && _music.clip != null) _music.UnPause();
                _engineLow?.UnPause();
                _engineHigh?.UnPause();
            }
        }

        public void SetVolumes(float music, float sfx)
        {
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            ApplyVolumes();
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            ApplyVolumes();
        }

        public void UpdateEngine(float speed, float throttle, bool nitro)
        {
            if (_engineLow == null || _engineHigh == null) return;
            float normalized = Mathf.Clamp01(speed / 95f);
            _engineLow.pitch = Mathf.Lerp(0.62f, 1.68f, normalized) * (nitro ? 1.08f : 1f);
            _engineHigh.pitch = Mathf.Lerp(0.72f, 2.2f, normalized) * (nitro ? 1.12f : 1f);
            float active = Muted ? 0f : SfxVolume;
            _engineLow.volume = active * Mathf.Lerp(0.18f, 0.32f, Mathf.Max(normalized, throttle));
            _engineHigh.volume = active * Mathf.Lerp(0.015f, 0.21f, normalized) * (nitro ? 1.4f : 1f);
        }

        public void PlayEvent(RaceEvent raceEvent)
        {
            if (raceEvent == null || _sfx == null || Muted) return;
            if (_eventClips.TryGetValue(raceEvent.Code, out AudioClip clip) && clip != null)
            {
                _sfx.PlayOneShot(clip, SfxVolume);
                return;
            }

            if (raceEvent.Kind == RaceEventKind.Danger && _impact != null) _sfx.PlayOneShot(_impact, SfxVolume);
            else if (raceEvent.Kind == RaceEventKind.Finish && _finish != null) _sfx.PlayOneShot(_finish, SfxVolume);
        }

        private void MapEvents()
        {
            RaceEventCode[] pickupCodes =
            {
                RaceEventCode.ItemPickedUp, RaceEventCode.ShieldActivated, RaceEventCode.RepairUsed,
                RaceEventCode.NitroPotionUsed, RaceEventCode.BoostPadTriggered, RaceEventCode.RepairPadTriggered
            };
            foreach (RaceEventCode code in pickupCodes) _eventClips[code] = _pickup;

            RaceEventCode[] impactCodes =
            {
                RaceEventCode.ObstacleHit, RaceEventCode.MineHitPlayer, RaceEventCode.MineHitRival,
                RaceEventCode.PulseHit, RaceEventCode.RacerCollision, RaceEventCode.ShieldBlocked
            };
            foreach (RaceEventCode code in impactCodes) _eventClips[code] = _impact;
            _eventClips[RaceEventCode.LapCompleted] = _finish;
            _eventClips[RaceEventCode.RaceFinished] = _finish;
        }

        private void ApplyVolumes()
        {
            float master = Muted ? 0f : 1f;
            if (_music != null) _music.volume = MusicVolume * master;
            if (_sfx != null) _sfx.volume = SfxVolume * master;
        }

        private AudioSource GetOrCreateSource(string sourceName, bool loop, bool spatial)
        {
            Transform child = transform.Find(sourceName);
            if (child == null)
            {
                child = new GameObject(sourceName).transform;
                child.SetParent(transform, false);
            }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 0.12f : 0f;
            return source;
        }

        private static AudioClip BuildEngineClip(string clipName, float frequency, float harmonic)
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate;
            var data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float fundamental = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                float overtone = Mathf.Sin(t * frequency * 2f * Mathf.PI * 2f) * harmonic;
                data[i] = (fundamental + overtone) * 0.18f;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
