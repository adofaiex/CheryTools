using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    // Unity's AudioRenderer returns no samples at all when the DSP buffer is larger than
    // RenderDspBufferSize (a 1024 buffer gave zero samples for a whole render), so renders switch the
    // buffer temporarily and switch it back afterwards.
    //
    // Changing the buffer needs AudioSettings.Reset, which restarts the audio system and drops the data
    // of every AudioClip loaded at runtime. The game's AudioManager keeps those dead clips cached, so the
    // level song and the level's custom sounds are reloaded through scnGame after each reset; the render
    // waits for the song (IsReloadingSong) before starting playback. Clips owned by other mods (e.g.
    // CReplay key sounds) cannot be reloaded from here. Players whose buffer is already at most
    // RenderDspBufferSize never trigger a reset.
    internal sealed class ChartRenderAudioBuffer
    {
        private const int RenderDspBufferSize = 256;

        private AudioConfiguration savedConfiguration;
        private AudioClip? songBeforeReset;
        private bool active;
        private bool changed;

        public bool IsActive => active;

        public static int RequiredDspBufferSize => RenderDspBufferSize;

        // True until the game has replaced the level song cleared by the last reset.
        public bool IsReloadingSong
        {
            get
            {
                AudioSource? song = ADOBase.conductor?.song;
                return songBeforeReset != null && song != null && ReferenceEquals(song.clip, songBeforeReset);
            }
        }

        public void Begin()
        {
            if (active)
            {
                return;
            }

            savedConfiguration = AudioSettings.GetConfiguration();
            active = true;
            int originalSize = savedConfiguration.dspBufferSize;
            if (originalSize <= RenderDspBufferSize)
            {
                ChartRenderDiagnostics.Log("Render audio DSP buffer unchanged. dspBufferSize=" + originalSize
                    + " sampleRate=" + savedConfiguration.sampleRate + ".");
                return;
            }

            AudioConfiguration renderConfiguration = savedConfiguration;
            renderConfiguration.dspBufferSize = RenderDspBufferSize;
            ResetAndReloadLevelAudio(renderConfiguration);
            changed = true;
            ChartRenderDiagnostics.Log("Render audio DSP buffer set temporarily. original=" + originalSize
                + " render=" + RenderDspBufferSize + "; reloading level song and custom sounds.");
        }

        public void Restore()
        {
            if (!active)
            {
                return;
            }

            active = false;
            if (!changed)
            {
                return;
            }

            changed = false;
            ResetAndReloadLevelAudio(savedConfiguration);
            ChartRenderDiagnostics.Log("Render audio DSP buffer restored. dspBufferSize="
                + savedConfiguration.dspBufferSize + "; reloading level song and custom sounds.");
        }

        private void ResetAndReloadLevelAudio(AudioConfiguration configuration)
        {
            songBeforeReset = ADOBase.conductor?.song?.clip;
            AudioSettings.Reset(configuration);
            scnGame? game = scnGame.instance;
            if (game == null)
            {
                songBeforeReset = null;
                return;
            }

            game.ReloadSong(force: true);
            game.ReloadCustomSounds(force: true);
        }
    }
}
