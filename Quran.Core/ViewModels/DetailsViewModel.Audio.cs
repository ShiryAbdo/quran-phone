// --------------------------------------------------------------------------------------------------------------------
// <summary>
//    Defines the DetailsViewModel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Quran.Core.Common;
using Quran.Core.Data;
using Quran.Core.Interfaces;
using Quran.Core.Properties;
using Quran.Core.Utils;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Quran.Core.ViewModels
{
    /// <summary>
    /// Define the DetailsViewModel type.
    /// </summary>
    public partial class DetailsViewModel : ViewModelWithDownload
    {
        #region Properties
        private AudioState audioPlayerState;
        public AudioState AudioPlayerState
        {
            get { return audioPlayerState; }
            set
            {
                if (value == audioPlayerState)
                    return;

                audioPlayerState = value;
                base.OnPropertyChanged(() => AudioPlayerState);
            }
        }

        private bool isLoadingAudio;
        public bool IsLoadingAudio
        {
            get { return isLoadingAudio; }
            set
            {
                if (value == isLoadingAudio)
                    return;

                isLoadingAudio = value;
                base.OnPropertyChanged(() => IsLoadingAudio);
            }
        }
        #endregion Properties

        #region Audio

        public async Task<bool> Play()
        {
            if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Playing)
            {
                // Do nothing
                return true;
            }
            else if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Paused)
            {
                QuranApp.NativeProvider.AudioProvider.Play();
                return true;
            }
            else
            {
                var ayah = SelectedAyah;
                if (ayah == null)
                {
                    var bounds = QuranUtils.GetPageBounds(CurrentPageNumber);
                    ayah = new QuranAyah
                        {
                            Surah = bounds[0],
                            Ayah = bounds[1]
                        };
                    //if (ayah.Ayah == 1 && ayah.Surah != Constants.SURA_TAWBA &&
                    //    ayah.Surah != Constants.SURA_FIRST)
                    //{
                    //    ayah.Ayah = 0;
                    //}
                }
                if (QuranUtils.IsValid(ayah))
                {
                    return await PlayFromAyah(ayah.Surah, ayah.Ayah);
                }
                else
                {
                    return false;
                }
            }
        }

        public void Pause()
        {
            QuranApp.NativeProvider.AudioProvider.Pause();
        }

        public void Stop()
        {
            QuranApp.NativeProvider.AudioProvider.Stop();
        }

        public void NextTrack()
        {
            var ayah = SelectedAyah;
            if (ayah != null)
            {
                if (AudioPlayerState == AudioState.Playing)
                {
                    QuranApp.NativeProvider.AudioProvider.Next();
                }
            }
        }

        public void PreviousTrack()
        {
            var ayah = SelectedAyah;
            if (ayah != null)
            {
                if (AudioPlayerState == AudioState.Playing)
                {
                    QuranApp.NativeProvider.AudioProvider.Previous();
                }
            }
        }

        public async Task<bool> PlayFromAyah(int startSura, int startAyah)
        {
            int currentQari = AudioUtils.GetReciterIdByName(SettingsUtils.Get<string>(Constants.PREF_ACTIVE_QARI));
            if (currentQari == -1)
                return false;

            var shouldRepeat = SettingsUtils.Get<bool>(Constants.PREF_AUDIO_REPEAT);
            var repeatAmount = SettingsUtils.Get<RepeatAmount>(Constants.PREF_REPEAT_AMOUNT);
            var repeatTimes = SettingsUtils.Get<int>(Constants.PREF_REPEAT_TIMES);
            var repeat = new RepeatInfo();
            if (shouldRepeat)
            {
                repeat.RepeatAmount = repeatAmount;
                repeat.RepeatCount = repeatTimes;
            }
            var lookaheadAmount = SettingsUtils.Get<AudioDownloadAmount>(Constants.PREF_DOWNLOAD_AMOUNT);
            var ayah = new QuranAyah(startSura, startAyah);
            var request = new AudioRequest(currentQari, ayah, repeat, 0, lookaheadAmount)
            {
                IsStreaming = SettingsUtils.Get<bool>(Constants.PREF_PREFER_STREAMING)
            };          

            if (request.IsStreaming)
            {
                PlayStreaming(request);
                return true;
            }
            else
            {
                return await DownloadAndPlayAudioRequest(request);
            }
        }

        private void PlayStreaming(AudioRequest request)
        {
            QuranApp.NativeProvider.AudioProvider.SetTrack(request);
        }

        private async Task<bool> DownloadAndPlayAudioRequest(AudioRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (this.ActiveDownload.IsDownloading)
            {
                return true;
            }

            var result = await DownloadAudioRequest(request);

            if (!result)
            {
                await QuranApp.NativeProvider.ShowErrorMessageBox("Something went wrong. Unable to download audio.");
                return false;
            }
            else
            {
                QuranApp.NativeProvider.AudioProvider.SetTrack(request);
                return true;
            }
        }

        private async Task<bool> DownloadAudioRequest(AudioRequest request)
        {
            bool result = true;
            // checking if there is aya position file
            if (!await FileUtils.HaveAyaPositionFile())
            {
                result = await DownloadAyahPositionFile();
            }

            // checking if need to download gapless database file
            if (result && await AudioUtils.ShouldDownloadGaplessDatabase(request))
            {
                string url = request.Reciter.GaplessDatabasePath;
                string destination = FileUtils.GetQuranDatabaseDirectory();
                destination = Path.Combine(destination, request.Reciter.LocalPath);
                // start the download
                result = await this.ActiveDownload.DownloadSingleFile(url, destination, Resources.loading_data);
            }

            // checking if need to download mp3
            if (result && !await AudioUtils.HaveAllFiles(request))
            {
                string url = request.Reciter.ServerUrl;
                string destination = request.Reciter.LocalPath;
                await FileUtils.EnsureDirectoryExists(destination);

                if (request.Reciter.IsGapless)
                    result = await AudioUtils.DownloadGaplessRange(url, destination, request.FromAyah, request.ToAyah);
                else
                    result = await AudioUtils.DownloadRange(request);
            }
            return result;
        }

        private async void AudioProvider_StateChanged(IAudioProvider sender, AudioPlayerPlayState e)
        {
            if (e == AudioPlayerPlayState.Stopped ||
                e == AudioPlayerPlayState.Closed ||
                e == AudioPlayerPlayState.Buffering)
            {
                await Task.Delay(500);
                // Check if still stopped
                if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Stopped ||
                    QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Closed ||
                    QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Buffering)
                {
                    AudioPlayerState = AudioState.Stopped;
                }
            }
            else if (e == AudioPlayerPlayState.Paused)
            {
                AudioPlayerState = AudioState.Paused;
            }
            else if (e == AudioPlayerPlayState.Playing)
            {
                AudioPlayerState = AudioState.Playing;                
            }

            if (e == AudioPlayerPlayState.Opening || e == AudioPlayerPlayState.Buffering)
            {
                IsLoadingAudio = true;
            }
            else
            {
                IsLoadingAudio = false;
            }
        }

        private async void AudioProvider_TrackChanged(Interfaces.IAudioProvider sender, AudioTrackModel request)
        {
            if (request != null)
            {
                try
                {
                    var requestAyah = new QuranAyah(request.Ayah.Key, request.Ayah.Value);
                    var pageNumber = QuranUtils.GetPageFromAyah(requestAyah);
                    var oldPageIndex = CurrentPageIndex;
                    var newPageIndex = GetIndexFromPageNumber(pageNumber);

                    CurrentPageIndex = newPageIndex;
                    if (oldPageIndex != newPageIndex)
                    {
                        await Task.Delay(500);
                    }
                    // If bismillah set to first ayah
                    if (requestAyah.Ayah == 0)
                        requestAyah.Ayah = 1;
                    SelectedAyah = requestAyah;
                }
                catch
                {
                    // Bad track
                }
            }
        }

        #endregion
    }
}
