/*
 * Based on project from https://github.com/jocover/sdrsharp-limesdr
 * 
 * modifications by YT7PWR 2018 https://github.com/GoranRadivojevic/sdrsharp-limesdr
 * modifications by netdog 2019 https://github.com/netdoggy/sdrsharp-limesdr
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SDRSharp.Radio;
using System.Windows.Forms;
using System.Diagnostics;

namespace SDRSharp.LimeSDR
{
    public class LimeSDRIO : IFrontendController, IIQStreamController, IDisposable, IFloatingConfigDialogProvider, ITunableSource // @todo: buggy ISampleRateChangeSource
    {
        #region variable

        private long _frequency = 105500000L;
        private LimeSDRDevice _limeSDRDevice = null;
        private readonly LimeSDRControllerDialog _gui;
        private SDRSharp.Radio.SamplesAvailableDelegate _callbackSamplesAvailable;
        //public event EventHandler SampleRateChanged;
        //public bool _isStreaming;
        private uint _channel = 0;      // rx0
        private uint _ant = 2;          // ant_l
        private double _lpbw = 1.5 * 1e6;
        private double _gain = 40.0;
        public double _sampleRate = 2 * 1e6;
        private float _specOffset = 100.0f;
        public string RadioName = "";
        private double _freqDiff = 0.0;
        private ushort _lnaGain = 9;
        private ushort _tiaGain = 1;
        private ushort _pgaGain = 11;

        #endregion

        #region properties

        public LimeSDRDevice Device
        {
            get { return _limeSDRDevice; }
        }

        public IntPtr LimeSDR_Device
        {
            get
            {
                if (Device != null)
                    return Device._lms_device;
                else
                    return IntPtr.Zero;
            }
        }

        public uint Channel
        {
            get
            {
                return _channel;
            }
            set
            {
                _channel = value;

                if(_limeSDRDevice != null)
                {
                    _limeSDRDevice.Channel = _channel;
                }
            }
        }

        public double LPBW
        {
            get
            {
                return _lpbw;
            }

            set
            {
                _lpbw = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.LPBW = _lpbw;
                }
            }
        }

        public double Gain
        {
            get
            {
                if (_limeSDRDevice != null)
                {
                    _gain = _limeSDRDevice.Gain;
                }
                return _gain;
            }

            set
            {
                _gain = (uint)value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.Gain = _gain;
                }
            }
        }

        public uint Antenna
        {
            get
            {
                return _ant;
            }
            set
            {
                _ant = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.Antenna = _ant;
                }
            }
        }

        public float SpecOffset
        {
            get
            {
                return _specOffset;
            }

            set
            {
                _specOffset = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.SpectrumOffset = _specOffset;
                }
            }
        }

        public double FreqDiff
        {
            set
            {
                _freqDiff = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.FreqDiff = _freqDiff;
                }
            }
        }

        public ushort LNAgain
        {
            set
            {
                _lnaGain = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.LNAgain = _lnaGain;
                }
            }

            get
            {
                if (_limeSDRDevice != null)
                {
                    _lnaGain = _limeSDRDevice.LNAgain;
                }
                return _lnaGain;
            }
        }

        public ushort TIAgain
        {
            set
            {
                _tiaGain = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.TIAgain = _tiaGain;
                }
            }

            get
            {
                if (_limeSDRDevice != null)
                {
                    _tiaGain = _limeSDRDevice.TIAgain;
                }
                return _tiaGain;
            }
        }

        public ushort PGAgain
        {
            set
            {
                _pgaGain = value;

                if (_limeSDRDevice != null)
                {
                    _limeSDRDevice.PGAgain = _pgaGain;
                }
            }
            get
            {
                if (_limeSDRDevice != null)
                {
                    _pgaGain = _limeSDRDevice.PGAgain;
                }
                return _pgaGain;
            }
        }

        #endregion

        #region constructor/destructor

        public LimeSDRIO()
        {           
            _gui = new LimeSDRControllerDialog(this);
            _sampleRate = _gui._sampleRate;          
        }

        ~LimeSDRIO()
        {
            Dispose();
        }

        public void Dispose()
        {
            Close();

            if (_gui != null)
            {
                _gui.Close();
                _gui.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        private unsafe void LimeSDRDevice_SamplesAvailable(object sender, SamplesAvailableEventArgs e)
        {
            _callbackSamplesAvailable(this, e.Buffer, e.Length);
        }

        public void Close()
        {
        
            if (_limeSDRDevice == null)
                return;
           
            _limeSDRDevice.SamplesAvailable -= LimeSDRDevice_SamplesAvailable;
            //_limeSDRDevice.SampleRateChanged -= LimeSDRDevice_SampleRateChanged;
            _limeSDRDevice.Dispose();
            _limeSDRDevice = null;
        }

        //private void LimeSDRDevice_SampleRateChanged(object sender, EventArgs e)
        //{
        //    EventHandler eventHandler = this.SampleRateChanged;

        //    if (eventHandler == null)
        //        return;

        //    eventHandler((object)this, e);
        //}

        public void Open()
        {
            try
            {
                _limeSDRDevice = new LimeSDRDevice(this);
                _limeSDRDevice.SamplesAvailable += LimeSDRDevice_SamplesAvailable;
                //_limeSDRDevice.SampleRateChanged += new EventHandler(this.LimeSDRDevice_SampleRateChanged);
                _limeSDRDevice.SampleRate = _sampleRate;
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                throw ex; 
            }
        }

        public void Start(Radio.SamplesAvailableDelegate callback)
        {
            try
            {
                _gui.grpChannel.Enabled = false;
                _gui.samplerateComboBox.Enabled = false;

                if (_limeSDRDevice == null)
                {
                    _limeSDRDevice = new LimeSDRDevice(this);
                    _limeSDRDevice.SamplesAvailable += LimeSDRDevice_SamplesAvailable;
                    //_limeSDRDevice.SampleRateChanged += LimeSDRDevice_SampleRateChanged;
                   //limeSDRDevice.SampleRate = _sampleRate;
                }


                if (!_limeSDRDevice.Open(RadioName))
                {
                    _limeSDRDevice.Close();
                    _limeSDRDevice.Open(RadioName);
                }
                _callbackSamplesAvailable = callback;

                _limeSDRDevice.LPBW = _gui.LPBW;
                //imeSDRDevice.SampleRate = _sampleRate; to be set in .Start
                _limeSDRDevice.Start(_channel, _lpbw, _gain, _ant, _sampleRate, _specOffset);
                _limeSDRDevice.LPBW = _gui.LPBW;
                _limeSDRDevice.LNAgain = _gui.LNAGain;
                _limeSDRDevice.PGAgain = _gui.PGAGain;
                _limeSDRDevice.TIAgain = _gui.TIAGain;
                
                //_isStreaming = true;
                _gui.RefreshFormAllGains();
                _gui.GetLimeSDRDeviceInfo();
            }
            catch(Exception ex)
            {
                _gui.grpChannel.Enabled = true;
                _gui.samplerateComboBox.Enabled = true;
                Debug.Write(ex.ToString());
                // MessageBox.Show(ex.ToString());
                throw ex;
            }
        }

        public void ReStart()
        {
            try
            {
                _gui.grpChannel.Enabled = false;
                _gui.samplerateComboBox.Enabled = false;

                if (this._limeSDRDevice == null)
                {
                    _limeSDRDevice = new LimeSDRDevice(this);
                    _limeSDRDevice.SamplesAvailable += LimeSDRDevice_SamplesAvailable;
                    //_limeSDRDevice.SampleRateChanged += LimeSDRDevice_SampleRateChanged;
                    //_limeSDRDevice.SampleRate = _sampleRate;
                }

                if (!_limeSDRDevice.Open(RadioName))
                {
                    _limeSDRDevice.Close();
                    _limeSDRDevice.Open(RadioName);
                }

                _limeSDRDevice.LPBW = _gui.LPBW;
                //_limeSDRDevice.SampleRate = _sampleRate;
                _limeSDRDevice.Start(_channel, _lpbw, _gain, _ant, _sampleRate, _specOffset);
                _limeSDRDevice.LPBW = _gui.LPBW;
                _limeSDRDevice.LNAgain = _gui.LNAGain;
                _limeSDRDevice.PGAgain = _gui.PGAGain;
                _limeSDRDevice.TIAgain = _gui.TIAGain;

                //_isStreaming = true;
                _gui.RefreshFormAllGains();
                _gui.GetLimeSDRDeviceInfo();
            }
            catch (Exception ex)
            {
                _gui.grpChannel.Enabled = true;
                _gui.samplerateComboBox.Enabled = true;
                Debug.Write(ex.ToString());
            }
        }

        public void Stop()
        {
            try
            {
                //_isStreaming = false;
                _gui.grpChannel.Enabled = true;
                _gui.samplerateComboBox.Enabled = true;
                _gui.toolStripStatusLabel_RxRate.Text = "";

                if (_limeSDRDevice != null)
                {
                    try
                    {
                        _limeSDRDevice.Stop();
                    }
                    catch
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        public bool IsSoundCardBased
        {
            get
            {
                return false;
            }
        }

        public string SoundCardHint
        {
            get
            {
                return string.Empty;
            }
        }

        public void ShowSettingGUI(IWin32Window parent)
        {
            if (this._gui.IsDisposed)
                return;

            _gui.Show();
            _gui.Activate();
            _gui.RefreshLimeSdrTemp();

        }

        public void HideSettingGUI()
        {
            if (_gui.IsDisposed)
                return;

            _gui.Hide();
        }

        public long Frequency
        {
            get
            {
                if (this._limeSDRDevice != null)
                {
                    _frequency = this._limeSDRDevice.Frequency;
                }
                return (long)_frequency;
            }
            set
            {
                if (this._limeSDRDevice != null)
                {
                    this._limeSDRDevice.Frequency = (long)value;
                    this._frequency = (long)value;
                }

                _frequency = (long)value;
            }
        }

        public double Samplerate
        {
            get
            {
                if (_limeSDRDevice != null)
                {
                    return _limeSDRDevice.SampleRate;
                }
                return 10000000.0;
            }
        }

        public bool CanTune
        {
            get
            { return true; }
        }

        public long MaximumTunableFrequency
        {

            get { return 3800000000; }
        }

        public long MinimumTunableFrequency
        {
            get { return 0; }
        }
    }
}
