using System;
using DG.Tweening;
using UnityEngine;

namespace ADOFAI.EditorTweaks.ChartRendering.Features.ChartRendering
{
    /// <summary>
    /// 在离线视频渲染期间，精准同步并驱动关卡背景音乐（BGM）播放。
    /// 解决在 AudioRenderer.Start() 离线渲染模式下，Unity 原生 PlayScheduled 无法正常发声的问题。
    /// </summary>
    internal static class ChartRenderSongSync
    {
        private static float originalVolume = -1f;
        private static bool originalMute;

        public static void Reset()
        {
            originalVolume = -1f;
            originalMute = false;
        }

        public static void Update()
        {
            if (!ChartRenderSession.IsRendering)
            {
                return;
            }

            scrConductor conductor = ADOBase.conductor;
            if (conductor == null || conductor.song == null || conductor.song.clip == null)
            {
                return;
            }

            AudioSource song = conductor.song;
            AudioClip clip = song.clip;
            float clipLength = clip.length;

            if (originalVolume < 0f)
            {
                originalVolume = song.volume;
                originalMute = song.mute;
            }

            // 1. 获取当前谱面虚拟位置（秒）
            if (!ChartRenderVisualClock.TryGetSongPosition(out double songPosition))
            {
                songPosition = conductor.songposition_minusi;
            }

            double pitch = song.pitch <= 0.0001f ? 1.0 : (double)song.pitch;
            double addoffset = conductor.addoffset;

            // 获取游戏延迟（玩家音频校准 inputOffset，单位秒）
            double inputOffset = 0.0;
            try
            {
                inputOffset = (double)scrConductor.currentPreset.inputOffset * 0.001;
            }
            catch
            {
            }

            // 倒计时扣除项（仅当单独倒计时开启时）
            double countdownOffset = 0.0;
            try
            {
                countdownOffset = conductor.separateCountdownTime
                    ? conductor.crotchetAtStart * (double)conductor.adjustedCountdownTicks
                    : 0.0;
            }
            catch
            {
            }

            // 2. 根据 ADOFAI 核心谱面与 Conductor 同步方程：
            // 音频物理播放时间 = 谱面位置 + 音乐偏移 (addoffset) + 游戏延迟 (inputOffset * pitch) - 独立倒计时
            // 在第 0 块砖 (songPosition = 0) 命中时刻，音乐恰好处于 addoffset + inputOffset * pitch；
            // 完美还原游戏内“音乐偏移”与“游戏延迟”的双重叠加机制。
            double targetAudioTime = songPosition + addoffset + inputOffset * pitch - countdownOffset;

            // 3. 分阶段精确驱动：
            if (targetAudioTime < 0.0)
            {
                // 阶段 A：尚处于倒计时前奏阶段，音乐暂未开始
                if (song.isPlaying)
                {
                    song.Pause();
                }
            }
            else if (targetAudioTime < (double)clipLength)
            {
                // 阶段 B：处于音乐播放区间
                float targetSeconds = (float)targetAudioTime;

                // 确保解除静音和恢复音量
                if (song.mute)
                {
                    song.mute = false;
                }

                // 杀掉可能将 volume 压为 0 的 DOTween 淡入补间
                song.DOKill(complete: false);

                float targetVolume = 1f;
                LevelData? levelData = ADOBase.customLevel?.levelData ?? ADOBase.editor?.customLevel?.levelData;
                if (levelData != null && levelData.volume > 0)
                {
                    targetVolume = (float)levelData.volume * 0.01f;
                }
                else if (originalVolume > 0.01f)
                {
                    targetVolume = originalVolume;
                }

                if (song.volume < 0.01f || Mathf.Abs(song.volume - targetVolume) > 0.05f)
                {
                    song.volume = targetVolume;
                }

                // 启动或校准播放
                if (!song.isPlaying)
                {
                    song.time = Mathf.Clamp(targetSeconds, 0f, clipLength - 0.001f);
                    song.Play();
                    ChartRenderDiagnostics.Log("BGM playback started by ChartRenderSongSync at targetAudioTime="
                        + targetSeconds.ToString("F3") + "s, volume=" + song.volume.ToString("F2") + ".");
                }
                else
                {
                    // 已在播放中：仅当漂移明显超过容差（> 80ms 且 < 5s）时才执行轻度重定位，避免每帧定位产生杂音
                    float drift = Mathf.Abs(song.time - targetSeconds);
                    if (drift > 0.08f && drift < 5.0f)
                    {
                        song.time = Mathf.Clamp(targetSeconds, 0f, clipLength - 0.001f);
                    }
                }

                if (!conductor.hasSongStarted)
                {
                    conductor.hasSongStarted = true;
                }

                // 同步处理副音轨 song2/song3（如果存在）
                SyncSecondaryTrack(conductor.song2, targetSeconds, targetVolume);
                SyncSecondaryTrack(conductor.song3, targetSeconds, targetVolume);
            }
            else
            {
                // 阶段 C：音乐已超出 clip 时长，停止播放
                if (song.isPlaying)
                {
                    song.Pause();
                }
            }
        }

        private static void SyncSecondaryTrack(AudioSource? track, float targetSeconds, float targetVolume)
        {
            if (track == null || track.clip == null)
            {
                return;
            }

            float len = track.clip.length;
            if (targetSeconds >= 0f && targetSeconds < len)
            {
                if (track.mute)
                {
                    track.mute = false;
                }

                track.DOKill(complete: false);
                if (track.volume < 0.01f)
                {
                    track.volume = targetVolume;
                }

                if (!track.isPlaying)
                {
                    track.time = Mathf.Clamp(targetSeconds, 0f, len - 0.001f);
                    track.Play();
                }
                else
                {
                    float drift = Mathf.Abs(track.time - targetSeconds);
                    if (drift > 0.08f && drift < 5.0f)
                    {
                        track.time = Mathf.Clamp(targetSeconds, 0f, len - 0.001f);
                    }
                }
            }
            else if (track.isPlaying)
            {
                track.Pause();
            }
        }
    }
}
