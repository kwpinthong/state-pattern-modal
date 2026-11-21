using UnityEngine;

namespace ALittleBB.StateMachines
{
    /// <summary>
    /// Plays and fades the background music of one Audio Source for <see cref="StateAudioBinding"/>. It is added,
    /// hidden in the Inspector, next to that Audio Source (on its GameObject, not on the binding's), so the music and
    /// a fade under way do not depend on the binding: disabling or destroying the binding leaves the music playing.
    /// <para>A crossfade needs two clips at once: the second Audio Source is added next to the first, also hidden,
    /// the first time it is needed, and used again for every later fade. Its settings (output mixer group, spatial
    /// blend, priority, pitch, 3D settings...) are copied from the Audio Source that is set at the start of each
    /// fade. Either of the two can be the one that plays after a fade.</para>
    /// <para>The volume of the music (V) is the volume of the Audio Source that is set, read when a fade or a change
    /// starts while no fade is under way. Every fade ends with the music at V and the stopped Audio Sources back at
    /// V. Fades run on unscaled time, so they go on while Time.timeScale is 0.</para>
    /// </summary>
    [AddComponentMenu("")]
    sealed class StateAudioBgmPlayer : MonoBehaviour
    {
        AudioSource _set;
        AudioSource _second;

        /// <summary>The Audio Source that plays, or last played, the music.</summary>
        AudioSource _current;

        bool _fading;
        float _volume;
        float _elapsed;
        float _duration;

        /// <summary>The Audio Source that ends the fade at V, or null when the fade ends in silence.</summary>
        AudioSource _target;

        float _setFrom;
        float _secondFrom;

        /// <summary>The player of <paramref name="source"/>, added next to it the first time.</summary>
        internal static StateAudioBgmPlayer For(AudioSource source)
        {
            StateAudioBgmPlayer[] players = source.GetComponents<StateAudioBgmPlayer>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i]._set == source)
                    return players[i];
            }

            var player = source.gameObject.AddComponent<StateAudioBgmPlayer>();
            player.hideFlags = HideFlags.HideInInspector;
            player._set = source;
            player._current = source;
            return player;
        }

        internal bool Plays(AudioSource source) => _set == source;

        /// <summary>The volume the music ends at.</summary>
        float Volume => _fading ? _volume : _set.volume;

        /// <summary>
        /// Plays <paramref name="clip"/> looped at V. When it is already playing it goes on from where it is.
        /// With <paramref name="fadeSeconds"/> &gt; 0 it fades in while what else plays fades out.
        /// </summary>
        internal void Play(AudioClip clip, float fadeSeconds)
        {
            float volume = Volume;
            AudioSource playing = PlayingWith(clip);

            if (fadeSeconds <= 0f)
            {
                AudioSource source = playing != null ? playing : _current;
                source.loop = true;
                if (playing == null)
                {
                    source.clip = clip;
                    source.Play();
                }

                End(source, volume);
                return;
            }

            if (_fading && playing != null && _target == playing)
                return; // the fade under way already ends with this clip

            if (playing == null)
            {
                playing = SourceForANewClip();
                playing.clip = clip;
                playing.volume = 0f;
                playing.loop = true;
                playing.Play();
            }

            playing.loop = true;
            if (!_fading && !IsPlaying(Other(playing)) && Mathf.Approximately(playing.volume, volume))
            {
                _current = playing;
                return; // it already plays alone at V
            }

            StartFade(playing, volume, fadeSeconds);
        }

        /// <summary>Stops the music, fading it out over <paramref name="fadeSeconds"/> when that is &gt; 0.</summary>
        internal void Stop(float fadeSeconds)
        {
            float volume = Volume;
            if (fadeSeconds <= 0f || (!IsPlaying(_set) && !IsPlaying(_second)))
            {
                End(null, volume);
                return;
            }

            StartFade(null, volume, fadeSeconds);
        }

        /// <summary>Ends a fade under way at once, where it was going.</summary>
        internal void Finish()
        {
            if (_fading)
                End(_target, _volume);
        }

        void Update()
        {
            if (!_fading)
                return;

            if (_set == null)
            {
                _fading = false; // the Audio Source that is set was destroyed: nothing to fade any more
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration)
            {
                End(_target, _volume);
                return;
            }

            float k = _elapsed / _duration;
            _set.volume = Mathf.Lerp(_setFrom, _target == _set ? _volume : 0f, k);
            if (_second != null)
                _second.volume = Mathf.Lerp(_secondFrom, _target == _second ? _volume : 0f, k);
        }

        void OnDisable() => Finish();

        void StartFade(AudioSource target, float volume, float seconds)
        {
            if (_second != null)
                CopySettings(_set, _second);

            _fading = true;
            _target = target;
            _volume = volume;
            _elapsed = 0f;
            _duration = seconds;
            _setFrom = _set.volume;
            _secondFrom = _second != null ? _second.volume : 0f;
            if (target != null)
                _current = target;
        }

        /// <summary>
        /// No fade any more: <paramref name="target"/> (if any) plays at <paramref name="volume"/>, the other
        /// Audio Source is stopped, and both are left at that volume.
        /// </summary>
        void End(AudioSource target, float volume)
        {
            _fading = false;
            _target = null;
            if (target != null)
                _current = target;

            EndOne(_set, target, volume);
            EndOne(_second, target, volume);
        }

        static void EndOne(AudioSource source, AudioSource target, float volume)
        {
            if (source == null)
                return;

            if (source != target)
                source.Stop();

            source.volume = volume;
        }

        /// <summary>The Audio Source to fade a new clip in on: one that is silent, or else the quieter of the two.</summary>
        AudioSource SourceForANewClip()
        {
            if (!IsPlaying(_current))
                return _current;

            AudioSource other = Other(_current);
            if (other == null)
            {
                other = _second = gameObject.AddComponent<AudioSource>();
                _second.hideFlags = HideFlags.HideInInspector;
                _second.playOnAwake = false;
                CopySettings(_set, _second);
                return other;
            }

            if (!other.isPlaying || other.volume <= _current.volume)
            {
                other.Stop();
                return other;
            }

            _current.Stop();
            return _current;
        }

        AudioSource Other(AudioSource source) => source == _set ? _second : _set;

        static bool IsPlaying(AudioSource source) => source != null && source.isPlaying;

        AudioSource PlayingWith(AudioClip clip)
        {
            if (IsPlaying(_current) && _current.clip == clip)
                return _current;

            AudioSource other = Other(_current);
            return IsPlaying(other) && other.clip == clip ? other : null;
        }

        static void CopySettings(AudioSource from, AudioSource to)
        {
            to.outputAudioMixerGroup = from.outputAudioMixerGroup;
            to.mute = from.mute;
            to.bypassEffects = from.bypassEffects;
            to.bypassListenerEffects = from.bypassListenerEffects;
            to.bypassReverbZones = from.bypassReverbZones;
            to.priority = from.priority;
            to.pitch = from.pitch;
            to.panStereo = from.panStereo;
            to.spatialBlend = from.spatialBlend;
            to.reverbZoneMix = from.reverbZoneMix;
            to.dopplerLevel = from.dopplerLevel;
            to.spread = from.spread;
            to.rolloffMode = from.rolloffMode;
            to.minDistance = from.minDistance;
            to.maxDistance = from.maxDistance;
            to.ignoreListenerPause = from.ignoreListenerPause;
            to.ignoreListenerVolume = from.ignoreListenerVolume;
            to.velocityUpdateMode = from.velocityUpdateMode;
            to.spatialize = from.spatialize;
            to.spatializePostEffects = from.spatializePostEffects;
            to.SetCustomCurve(AudioSourceCurveType.CustomRolloff, from.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            to.SetCustomCurve(AudioSourceCurveType.SpatialBlend, from.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
            to.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, from.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
            to.SetCustomCurve(AudioSourceCurveType.Spread, from.GetCustomCurve(AudioSourceCurveType.Spread));
        }
    }
}
