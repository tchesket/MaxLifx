﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using MaxLifx.Controllers;
using MaxLifx.Controls;
using MaxLifx.Payload;
using MaxLifx.Processors.ProcessorSettings;
using MaxLifx.Threads;
using MaxLifx.UIs;
using NAudio.CoreAudioApi;

namespace MaxLifx
{
    public enum WaveTypes
    {
        Sine,
        Square,
        Sawtooth,
        Audio,
        Noise,
        EaseZero,
        EaseZeroPointFive,
        EaseOne,
        EaseLinear,
        EaseInSine,
        EaseOutSine,
        EaseInOutSine,
        EaseInQuint,
        EaseOutQuint,
        EaseInOutQuint,
        EaseInQuart,
        EaseOutQuart,
        EaseInOutQuart,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad,
        EaseInExpo,
        EaseOutExpo,
        EaseInOutExpo,
        EaseInElastic,
        EaseOutElastic,
        EaseInOutElastic,
        EaseInCirc,
        EaseOutCirc,
        EaseInOutCirc,
        EaseInBack,
        EaseOutBack,
        EaseInOutBack,
        EaseInBounce,
        EaseOutBounce,
        EaseInOutBounce,
        EaseInCubic,
        EaseOutCubic,
        EaseInOutCubic
    }


    public class SoundResponseProcessor : ProcessorBase
    {
        private Random r;

        [XmlIgnore]
        public SoundResponseSettings SettingsCast
        {
            get { return ((SoundResponseSettings) Settings); }
        }

        public override ISettings Settings { get; set; }

        public override string SettingsAsXml
        {
            get { return ((SoundResponseSettings) (Settings)).ToXmlString(); }
            set
            {
                var s = new SoundResponseSettings();

                using (var st = new StringReader(value))
                {
                    s = (SoundResponseSettings) (new XmlSerializer(typeof (SoundResponseSettings)).Deserialize(st));
                }

                Settings = s;
            }
        }

        public void SoundResponse(MaxLifxBulbController bulbController, Random random)
        {
            r = random;

            if (Settings == null)
            {
                Settings = new SoundResponseSettings();
                SettingsCast.Hues.Add(0);
                SettingsCast.HueRanges.Add(0);
                SettingsCast.Saturations.Add(0);
                SettingsCast.SaturationRanges.Add(0);
                SettingsCast.Brightnesses.Add(.9f);
                SettingsCast.BrightnessRanges.Add(.1f);
                SettingsCast.Levels.Add(50);
                SettingsCast.LevelRanges.Add(25);
                SettingsCast.Bins.Add(10);
            }

            var de = new MMDeviceEnumerator();
            var device = de.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            DoMainLoop(bulbController, device);
        }

        private void DoMainLoop(MaxLifxBulbController bulbController, MMDevice device)
        {
            SettingsCast.WaveStartTime = DateTime.Now;

            var persistentFloatH = (float) r.NextDouble();
            var persistentFloatS = (float) r.NextDouble();
            var persistentFloatB = (float) r.NextDouble();
            var persistedSince = DateTime.Now;

            var spectrumEngine = new SpectrumAnalyserEngine();
            spectrumEngine.StartCapture();

            while (!TerminateThread)
            {
                var startTime = DateTime.Now;

                if (ShowUI)
                {
                    var t = new Thread(() =>
                    {
                        var form = new SoundResponseUI(SettingsCast, bulbController.Bulbs.Select(x => x.Label).ToList(), r);
                            /* (SettingsCast, bulbController.Bulbs);*/
                        form.ShowDialog();
                    });
                    t.Start();
                    ShowUI = false;
                }

                var _offOrOn = Settings.OffOrOn();
                if (_offOrOn)
                {
                    ushort brightness = 0;
                    ushort saturation = 0;
                    var _hue = 0;
                    var timeRunning = DateTime.Now - SettingsCast.WaveStartTime;

                    var floatValueH = 0f;
                    var floatValueS = 0f;
                    var floatValueB = 0f;

                    var bulbCtr = 0;
                    foreach (var label in SettingsCast.SelectedLabels)
                    {
                        var bulbNumber = SettingsCast.PerBulb ? bulbCtr : 0;

                        // don't raise an exception if there's no input...
                        if (bulbNumber >= SettingsCast.Levels.Count) continue;

                        try
                        {
                            var bulbTime = (timeRunning.TotalMilliseconds + SettingsCast.WaveDuration * bulbCtr / SettingsCast.Levels.Count) % SettingsCast.WaveDuration ;
                            switch (SettingsCast.WaveType)
                            {
                                case WaveTypes.Audio:
                                    var levelMin = 1 -
                                                   (((float) (SettingsCast.Levels[bulbNumber] + SettingsCast.LevelRanges[bulbNumber]/ 2 > 255 ? 255 : SettingsCast.Levels[bulbNumber] + SettingsCast.LevelRanges[bulbNumber] / 2) /
                                                     255));
                                    var levelMax = 1 -
                                                   (((float) (SettingsCast.Levels[bulbNumber] - SettingsCast.LevelRanges[bulbNumber] / 2 < 0 ? 0 : SettingsCast.Levels[bulbNumber] - SettingsCast.LevelRanges[bulbNumber] / 2) /255));
                                    var levelRange = levelMax - levelMin;

                                    float rawLevel;

                                    // don't raise an exception if there's no input...
                                    if (SettingsCast.Bins[bulbNumber] > spectrumEngine.LatestPoints.Count - 1) rawLevel = 0;
                                    else rawLevel = 1 - (spectrumEngine.LatestPoints[SettingsCast.Bins[bulbNumber]].Y / 255);

                                    float adjustedLevel;

                                    if (rawLevel < levelMin)
                                        adjustedLevel = 0;
                                    else if (rawLevel > levelMax)
                                        adjustedLevel = 1;
                                    else
                                    {
                                        adjustedLevel = (rawLevel - levelMin)/levelRange;
                                        if (adjustedLevel < 0 || adjustedLevel > 1) MessageBox.Show("D'oh!");
                                    }

                                    floatValueH = floatValueS = floatValueB = adjustedLevel;
                                        // device.AudioMeterInformation.MasterPeakValue;
                                    break;
                                case WaveTypes.Sine:
                                    floatValueH =
                                        floatValueS =
                                            floatValueB =
                                                (float)
                                                    (Math.Sin(timeRunning.TotalSeconds*6.283*500/SettingsCast.WaveDuration) +
                                                     1)/2;
                                    break;
                                case WaveTypes.Square:
                                    floatValueH =
                                        floatValueS =
                                            floatValueB =
                                                ((int) (timeRunning.TotalMilliseconds/SettingsCast.WaveDuration))%2;
                                    break;
                                case WaveTypes.Sawtooth: 
                                    floatValueH =
                                        floatValueS =
                                            floatValueB =
                                                ((float) timeRunning.TotalMilliseconds -
                                                 (((int) timeRunning.TotalMilliseconds/SettingsCast.WaveDuration)*
                                                  SettingsCast.WaveDuration))/SettingsCast.WaveDuration;
                                    break;

                                case WaveTypes.Noise:
                                    var span = DateTime.Now - persistedSince;
                                    if (span.TotalMilliseconds > SettingsCast.WaveDuration)
                                    {
                                        floatValueH = (float) r.NextDouble();
                                        floatValueS = (float) r.NextDouble();
                                        floatValueB = (float) r.NextDouble();
                                        persistentFloatH = floatValueH;
                                        persistentFloatS = floatValueS;
                                        persistentFloatB = floatValueB;
                                        persistedSince = DateTime.Now;
                                    }
                                    else
                                    {
                                        floatValueH = persistentFloatH;
                                        floatValueS = persistentFloatS;
                                        floatValueB = persistentFloatB;
                                    }
                                    break;

                                default:
                                    GetAdditionalWaveformValues(out floatValueH, out floatValueS, out floatValueB, timeRunning.TotalMilliseconds, bulbCtr); break;
                            }

                            if (SettingsCast.Hues.Count > bulbNumber)
                            {
                                brightness =
                                    (ushort)
                                        (((SettingsCast.BrightnessInvert ? 1 - floatValueB : floatValueB) *
                                          SettingsCast.BrightnessRanges[bulbNumber] * 2 +
                                          (SettingsCast.Brightnesses[bulbNumber] -
                                           SettingsCast.BrightnessRanges[bulbNumber])) *
                                         65535);
                                saturation =
                                    (ushort)
                                        (((SettingsCast.SaturationInvert ? 1 - floatValueS : floatValueS)*
                                          SettingsCast.SaturationRanges[bulbNumber]*2 +
                                          (SettingsCast.Saturations[bulbNumber] -
                                           SettingsCast.SaturationRanges[bulbNumber]))*
                                         65535);
                                _hue =
                                    ((int)
                                        ((SettingsCast.HueInvert ? 1 - floatValueH : floatValueH)*
                                         SettingsCast.HueRanges[bulbNumber]*2 +
                                         (SettingsCast.Hues[bulbNumber] - SettingsCast.HueRanges[0])) + 720)%360;
                                var _payload = new SetColourPayload
                                {
                                    Hue = _hue,
                                    Saturation = saturation,
                                    Brightness = brightness,
                                    Kelvin = SettingsCast.Kelvin,
                                    TransitionDuration = (uint) SettingsCast.TransitionDuration,
                                    PayloadType = BulbType.Lifx
                                };

                                _payload.PayloadType = bulbController.Bulbs.First(x => x.Label == label).BulbType;
                                    
                                bulbController.SetColour(label, _payload);
                               if (SettingsCast.Delay > 200)
                               {
                                   bulbController.SetColour(label, _payload);
                               }
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            Thread.Sleep(1);
                        }

                        bulbCtr++;
                    }
                }

                var runTime = DateTime.Now - startTime;
                int delayTime = (int)(SettingsCast.Delay - runTime.TotalMilliseconds);
                if (delayTime > 0)
                {
                    Thread.Sleep(delayTime);
                }
            }

            spectrumEngine.StopCapture();
        }

        private void GetAdditionalWaveformValues(out float floatValueH, out float floatValueS, out float floatValueB, double totalMilliseconds, int bulbCtr)
        {

            var totalDuration = SettingsCast.AllWaveformsDuration;
            var everyOther = true;// (totalMilliseconds / totalDuration) % 2 < 1;
            
            var position = everyOther ? (totalMilliseconds + totalDuration * bulbCtr / (0+SettingsCast.SelectedLabels.Count)) % totalDuration
                                      : (totalMilliseconds + totalDuration * ((SettingsCast.SelectedLabels.Count+0)-bulbCtr) / (0+SettingsCast.SelectedLabels.Count)) % totalDuration;

            WaveTypes? waveType = null;
            double? waveDuration = null;
            bool? waveInvert = null;

            if (position < SettingsCast.WaveDuration) { waveType = SettingsCast.WaveType; waveDuration = SettingsCast.WaveDuration; waveInvert = false; }
            else
            {
                position -= SettingsCast.WaveDuration;

                var ctr = 0;
                while(position > -1)
                {
                    if (position < SettingsCast.AdditionalWaveforms[ctr].Duration) { waveType = SettingsCast.AdditionalWaveforms[ctr].WaveType; waveDuration = SettingsCast.AdditionalWaveforms[ctr].Duration;  waveInvert = SettingsCast.AdditionalWaveforms[ctr].InverseWaveform;  break; }
                    position -= SettingsCast.AdditionalWaveforms[ctr].Duration;
                    ctr++;
                }
            }

            if (!waveType.HasValue) { floatValueH = floatValueS = floatValueB = 0; return; }

            switch (waveType)
            {
                case WaveTypes.EaseZero: floatValueH = floatValueS = floatValueB = 0f; break;
                case WaveTypes.EaseZeroPointFive: floatValueH = floatValueS = floatValueB = .5f; break;
                case WaveTypes.EaseOne: floatValueH = floatValueS = floatValueB = 1f; break;
                case WaveTypes.EaseLinear: floatValueH = floatValueS = floatValueB = (float)Ease.Linear(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInSine: floatValueH = floatValueS = floatValueB = (float)Ease.InSine(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutSine: floatValueH = floatValueS = floatValueB = (float)Ease.OutSine(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutSine: floatValueH = floatValueS = floatValueB = (float)Ease.InOutSine(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInQuint: floatValueH = floatValueS = floatValueB = (float)Ease.InQuint(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutQuint: floatValueH = floatValueS = floatValueB = (float)Ease.OutQuint(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutQuint: floatValueH = floatValueS = floatValueB = (float)Ease.InOutQuint(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInQuart: floatValueH = floatValueS = floatValueB = (float)Ease.InQuart(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutQuart: floatValueH = floatValueS = floatValueB = (float)Ease.OutQuart(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutQuart: floatValueH = floatValueS = floatValueB = (float)Ease.InOutQuart(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInQuad: floatValueH = floatValueS = floatValueB = (float)Ease.InQuad(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutQuad: floatValueH = floatValueS = floatValueB = (float)Ease.OutQuad(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutQuad: floatValueH = floatValueS = floatValueB = (float)Ease.InOutQuad(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInExpo: floatValueH = floatValueS = floatValueB = (float)Ease.InExpo(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutExpo: floatValueH = floatValueS = floatValueB = (float)Ease.OutExpo(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutExpo: floatValueH = floatValueS = floatValueB = (float)Ease.InOutExpo(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInElastic: floatValueH = floatValueS = floatValueB = (float)Ease.InElastic(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutElastic: floatValueH = floatValueS = floatValueB = (float)Ease.OutElastic(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutElastic: floatValueH = floatValueS = floatValueB = (float)Ease.InOutElastic(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInCirc: floatValueH = floatValueS = floatValueB = (float)Ease.InCirc(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutCirc: floatValueH = floatValueS = floatValueB = (float)Ease.OutCirc(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutCirc: floatValueH = floatValueS = floatValueB = (float)Ease.InOutCirc(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInBack: floatValueH = floatValueS = floatValueB = (float)Ease.InBack(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutBack: floatValueH = floatValueS = floatValueB = (float)Ease.OutBack(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutBack: floatValueH = floatValueS = floatValueB = (float)Ease.InOutBack(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInBounce: floatValueH = floatValueS = floatValueB = (float)Ease.InBounce(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutBounce: floatValueH = floatValueS = floatValueB = (float)Ease.OutBounce(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutBounce: floatValueH = floatValueS = floatValueB = (float)Ease.InOutBounce(position, 0, 1, waveDuration.Value); break;

                case WaveTypes.EaseInCubic: floatValueH = floatValueS = floatValueB = (float)Ease.InCubic(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseOutCubic: floatValueH = floatValueS = floatValueB = (float)Ease.OutCubic(position, 0, 1, waveDuration.Value); break;
                case WaveTypes.EaseInOutCubic: floatValueH = floatValueS = floatValueB = (float)Ease.InOutCubic(position, 0, 1, waveDuration.Value); break;
                default: floatValueH = floatValueS = floatValueB = 0; break;

            }
            if (waveInvert.Value)
                floatValueH = floatValueS = floatValueB = (1 - floatValueB);
        }
    }
}