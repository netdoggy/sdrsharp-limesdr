/*
 * Based on project from https://github.com/jocover/sdrsharp-limesdr
 * 
 * modifications by YT7PWR 2018 https://github.com/GoranRadivojevic/sdrsharp-limesdr
 * modifications by netdog 2019 https://github.com/netdoggy/sdrsharp-limesdr
 */

using System;
using SDRSharp.Radio;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace SDRSharp.LimeSDR
{
    public class LimeSDRDevice : IDisposable
    {
        #region variable 

        LimeSDRIO _parrent;
        private const double DefaultSamplerate = 768000;
        private const long DefaultFrequency = 101700000;
        private const uint SampleTimeoutMs = 1000;

        public IntPtr _ptrLmsDevice = IntPtr.Zero;
        public IntPtr _ptrLmsStream = IntPtr.Zero;
        private bool _isStreaming;
        private Thread _sampleThread = null;
        private uint _channel = 0;  // rx0
        private uint _ant = 2;      // ant_l

        private static uint _readLength = 5000;
        private UnsafeBuffer _iqBuffer;
        private unsafe Complex* _iqPtr;
        private UnsafeBuffer _samplesBuffer;
        private unsafe float* _samplesPtr;
        private readonly SamplesAvailableEventArgs _eventArgs = new SamplesAvailableEventArgs();

        private double _sampleRate = DefaultSamplerate;
        private double _centerFrequency = DefaultFrequency;
        private double _old_centerFrequency = DefaultFrequency;
        private uint _gain = 40;
        private ushort _lnaGain = 9;
        private ushort _tiaGain = 1;
        private ushort _pgaGain = 11;

        public const bool LMS_CH_TX = true;
        public const bool LMS_CH_RX = false;

        [method: CompilerGenerated]
        [CompilerGenerated]
        public event SamplesAvailableDelegate SamplesAvailable;

        private double _lpbw = 1.5 * 1e6;
        public float SpectrumOffset = 100.0f;
        private double _freqDiff = 0.0;

        LMS7Parameter LMS7_PGA_gain = new LMS7Parameter();
        LMS7Parameter LMS7_TIA_gain = new LMS7Parameter();
        LMS7Parameter LMS7_LNA_gain = new LMS7Parameter();
        //LMS7Parameter LMS7_G_RXLOOPB_RFE = new LMS7Parameter();

        #endregion

        #region Properties

        public bool IsStreaming
        {
            get
            {
                return _isStreaming;
            }
        }

        public double FreqDiff
        {
            set
            {
                _freqDiff = value;

                if (IsStreaming)
                    Frequency = (long)_centerFrequency;   // force reload
            }
        }

        public ushort LNAgain
        {
            set
            {
                /* G_LNA_RFE_
                15 – Gmax (default)
               14 – Gmax-1
               13 – Gmax-2
               12 – Gmax-3
               11 – Gmax-4
               10 – Gmax-5
               9 – Gmax-6
               8 – Gmax-9
               7 – Gmax-12
               6 – Gmax-15
               5 – Gmax-18
               4 – Gmax-21
               3 – Gmax-24
               2 – Gmax-27
               1 – Gmax-30
               */
                _lnaGain = value;

                if (_isStreaming)
                {
                    if (NativeMethods.LMS_WriteParam(_ptrLmsDevice, LMS7_LNA_gain, _lnaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
            }

            get
            {

                if (_isStreaming)
                {
                    if (NativeMethods.LMS_ReadParam(_ptrLmsDevice, LMS7_LNA_gain, ref _lnaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }

                }
                return _lnaGain;
            }
        }

        public ushort TIAgain
        {
            set
            {
                _tiaGain = value;

                if (_isStreaming)
                {
                    if (NativeMethods.LMS_WriteParam(_ptrLmsDevice, LMS7_TIA_gain, _tiaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
            }

            get
            {
                if (_isStreaming)
                {
                    if (NativeMethods.LMS_ReadParam(_ptrLmsDevice, LMS7_TIA_gain, ref _tiaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
                return _tiaGain;
            }
        }

        public ushort PGAgain
        {
            set
            {
                _pgaGain = value;

                if (_isStreaming)
                {
                    if (NativeMethods.LMS_WriteParam(_ptrLmsDevice, LMS7_PGA_gain, _pgaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
            }

            get
            {
                if (_isStreaming)
                {
                    if (NativeMethods.LMS_ReadParam(_ptrLmsDevice, LMS7_PGA_gain, ref _pgaGain) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
                return _pgaGain;
            }
        }

        public double Temperature()
        {
            double temp = 0;

            if (_isStreaming && NativeMethods.LMS_GetChipTemperature(_ptrLmsDevice, 0, ref temp) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }
            return temp;
        }

        #endregion

        #region callback

        private unsafe void ThreadReceiveSamples_Sync()
        {
            try
            {
                lms_stream_meta_t meta = new lms_stream_meta_t();
                meta.timestamp = 0;
                meta.flushPartialPacket = false;
                meta.waitForTimestamp = false;


                while (_isStreaming)
                {

                    if (_iqBuffer == null || _iqBuffer.Length != _readLength)
                    {
                        _iqBuffer = UnsafeBuffer.Create((int)_readLength, sizeof(Complex));
                        _iqPtr = (Complex*)_iqBuffer;
                        Thread.Sleep(1);
                    }

                    if (_samplesBuffer == null || _samplesBuffer.Length != (2 * _readLength))
                    {
                        _samplesBuffer = UnsafeBuffer.Create((int)(2 * _readLength), sizeof(float));
                        _samplesPtr = (float*)_samplesBuffer;
                        Thread.Sleep(1);
                    }

                    var samplesReceived = NativeMethods.LMS_RecvStream(_ptrLmsStream, _samplesPtr, _readLength, ref meta, SampleTimeoutMs);
                    if (samplesReceived < 0)
                    {
                        MessageBox.Show("LimeSDRDevice::ReceiveSamples_sync read error");
                        //throw ApplicationException("LimeSDRDevice::ReceiveSamples_sync read error");
                        break;
                    }
                    var ptrIq = _iqPtr;

                    for (int i = 0; i < _readLength; i++)
                    {
                        ptrIq->Real = _samplesPtr[i * 2] / SpectrumOffset;
                        ptrIq->Imag = _samplesPtr[i * 2 + 1] / SpectrumOffset;
                        ptrIq++;
                    }

                    ComplexSamplesAvailable(_iqPtr, _iqBuffer.Length);
                }
            } catch (Exception ex)
            {
                this._parrent.LogError(ex, "LimeSDR_Thred_Error.txt");
                _isStreaming = false;
            }
            NativeMethods.LMS_StopStream(_ptrLmsStream);
            NativeMethods.LMS_DestroyStream(_ptrLmsDevice, _ptrLmsStream);
            Close();
        }

        #endregion

        public LimeSDRDevice(LimeSDRIO parrent)
        {
            _parrent = parrent;

            // G_PGA_RBB_(1, 2)[4:0]: This is the gain of the PGA. The gain is adaptively set to
            // maintain signal swing of 0.6Vpkd at the output of the PGA. The value of the gain is:
            // Gain(dB) = -12+G_PGA_RBB. Default
            LMS7_PGA_gain.address = 0x0119;
            LMS7_PGA_gain.msb = 4;
            LMS7_PGA_gain.lsb = 0;
            LMS7_PGA_gain.defaultValue = 11;
            LMS7_PGA_gain.name = "G_PGA_RBB";
            LMS7_PGA_gain.tooltip = "This is the gain of the PGA";

            LMS7_TIA_gain.address = 0x0113;
            LMS7_TIA_gain.msb = 1;
            LMS7_TIA_gain.lsb = 0;
            LMS7_TIA_gain.defaultValue = 3;
            LMS7_TIA_gain.name = "G_TIA_RFE";
            LMS7_TIA_gain.tooltip = "Controls the Gain of the TIA";

            //LMS7_G_RXLOOPB_RFE.address = 0x0113;
            //LMS7_G_RXLOOPB_RFE.msb = 5;
            //LMS7_G_RXLOOPB_RFE.lsb = 2;
            //LMS7_G_RXLOOPB_RFE.defaultValue = 0;
            //LMS7_G_RXLOOPB_RFE.name = "G_RXLOOPB_RFE";
            //LMS7_G_RXLOOPB_RFE.tooltip = "Controls RXFE loopback gain (Should be '0' when actual LNAs are working)";

            LMS7_LNA_gain.address = 0x0113;
            LMS7_LNA_gain.msb = 9;
            LMS7_LNA_gain.lsb = 6;
            LMS7_LNA_gain.defaultValue = 15;
            LMS7_LNA_gain.name = "G_LNA_RFE";
            LMS7_LNA_gain.tooltip = "Controls the gain of the LNA";
        }

        ~LimeSDRDevice()
        {
            Dispose();
        }


        private unsafe void ComplexSamplesAvailable(Complex* buffer, int length)
        {
            if (SamplesAvailable != null)
            {
                _eventArgs.Buffer = buffer;
                _eventArgs.Length = length;
                SamplesAvailable(this, _eventArgs);
            }
        }

        public void Stop()
        {
            if (!_isStreaming)
                return;

            _isStreaming = false;
            _sampleThread.Join();
            _sampleThread = null;
            
            Marshal.FreeHGlobal(_ptrLmsStream);
            _ptrLmsStream = IntPtr.Zero;

        }

        public void Dispose()
        {
            Stop();
            if (_ptrLmsDevice != IntPtr.Zero)
                NativeMethods.LMS_Close(_ptrLmsDevice);

            GC.SuppressFinalize(this);
            _ptrLmsDevice = IntPtr.Zero;
        }

        public unsafe bool Open(string name)
        {
            if (NativeMethods.LMS_GetDeviceList(null) < 1)
            {
                throw new Exception("Cannot found LimeSDR device.");
            }

            if (NativeMethods.LMS_Open(out _ptrLmsDevice, name, null) != 0)
            {
                var str = NativeMethods.limesdr_strerror();
                throw new ApplicationException(str.Length > 1 ? str : "Cannot open LimeSDR device. Is the device locked somewhere?");
            }

            return true;
        }

        public unsafe void Close()
        {
            if (NativeMethods.LMS_Close(_ptrLmsDevice) != 0)
            {
                throw new ApplicationException("Cannot open LimeSDR device. Is the device locked somewhere?");
            }

            _ptrLmsDevice = IntPtr.Zero;
        }

        private unsafe void _SetDeviceSampleRate(double sampleRate)
        {
            uint oversample = 32;

            if (sampleRate > 32e6)
            {
                oversample = 0;
            }
            else if (sampleRate > 16e6)
            {
                oversample = 4;
            }
            else if (sampleRate > 8e6)
            {
                oversample = 8;
            }
            else if (sampleRate > 4e6)
            {
                oversample = 16;
            }
            else if (sampleRate > 2e6)
            {
                oversample = 32;
            }
            else
            {
                oversample = 32;
            }


            // Set sampling rate for all RX or TX channels.Sample rate is in complex samples(1 sample = I + Q).The function sets sampling rate that is used for data exchange with the host.It also allows to specify higher sampling rate to be used in RF by setting oversampling ratio.Valid oversampling values are 1, 2, 4, 8, 16, 32 or 0(use device default oversampling value).
            if (NativeMethods.LMS_SetSampleRateDir(_ptrLmsDevice, LMS_CH_RX, sampleRate, oversample) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }
        }

        public unsafe void Start(uint ch, double lpbw, double gain, uint ant, double sr, float specOffset)
        {
            if (_isStreaming)
            {
                throw new ApplicationException("Start() Already running");
            }
            _ant = ant;
            _channel = ch;
            _lpbw = lpbw;
            _gain = (uint)gain;
            _sampleRate = sr;
            SpectrumOffset = specOffset;

            if (NativeMethods.LMS_Init(_ptrLmsDevice) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }

            if (NativeMethods.LMS_EnableChannel(_ptrLmsDevice, LMS_CH_RX, _channel, true) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }

            if (NativeMethods.LMS_SetAntenna(_ptrLmsDevice, LMS_CH_RX, _channel, _ant) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }

            if (NativeMethods.LMS_SetGaindB(_ptrLmsDevice, LMS_CH_RX, _channel, _gain) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }
            this.SampleRate = _sampleRate;

            LPBW = _lpbw;

            lms_stream_t streamId = new lms_stream_t();
            streamId.handle = (UIntPtr)0;                // size_t on LimeSuite.h
            streamId.channel = _channel;                // channel number
            streamId.fifoSize = 16 * 1024;              // fifo size in samples
            streamId.throughputVsLatency = 0.5f;        // balance
            streamId.isTx = false;                      // RX channel
            streamId.dataFmt = dataFmt.LMS_FMT_F32;
            _ptrLmsStream = Marshal.AllocHGlobal(Marshal.SizeOf(streamId));

            Marshal.StructureToPtr(streamId, _ptrLmsStream, false);

            if (NativeMethods.LMS_SetupStream(_ptrLmsDevice, _ptrLmsStream) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }

            if (NativeMethods.LMS_StartStream(_ptrLmsStream) != 0)
            {
                throw new ApplicationException(NativeMethods.limesdr_strerror());
            }

            _isStreaming = true;

            this.Frequency = (long)_centerFrequency;

            _sampleThread = new Thread(ThreadReceiveSamples_Sync);
            _sampleThread.Name = "limesdr_samples_rx";
            _sampleThread.Priority = ThreadPriority.Highest;
            _sampleThread.Start();
        }

        public unsafe long Frequency
        {

            get
            {
                if (_ptrLmsDevice != IntPtr.Zero)
                {
                    if (NativeMethods.LMS_GetLOFrequency(_ptrLmsDevice, LMS_CH_RX, _channel, ref _centerFrequency) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
                return (long)_centerFrequency;
            }
            set
            {
                _centerFrequency = value;

                try
                {
                    if (!_isStreaming)
                    {
                        _old_centerFrequency = _centerFrequency;
                        return;
                    }

                    if (_centerFrequency < 30 * 1e6 && _old_centerFrequency >= 30 * 1e6)
                    {
                        _old_centerFrequency = _centerFrequency;
                        Stop();
                        _parrent.ReStart();
                    }
                    else if (_centerFrequency > 30 * 1e6 && _old_centerFrequency <= 30 * 1e6)
                    {
                        _old_centerFrequency = _centerFrequency;
                        Stop();
                        _parrent.ReStart();
                    }
                    else
                    {
                        _old_centerFrequency = _centerFrequency;

                        if (_ptrLmsDevice == IntPtr.Zero)
                        {
                            return;
                        }

                        if (value >= 30 * 1e6)
                        {
                            if (NativeMethods.LMS_SetNCOIndex(_ptrLmsDevice, LMS_CH_RX, _channel, 15, true) != 0)   // 0.0 NCO
                            {
                                Stop();
                                throw new ApplicationException(NativeMethods.limesdr_strerror());
                            }

                            if (NativeMethods.LMS_SetLOFrequency(_ptrLmsDevice, LMS_CH_RX, _channel, _centerFrequency + _freqDiff) != 0)
                            {
                                throw new ApplicationException(NativeMethods.limesdr_strerror());
                            }
                        }
                        else
                        {
                            if (NativeMethods.LMS_SetLOFrequency(_ptrLmsDevice, LMS_CH_RX, _channel, 30.0 * 1e6) != 0)
                            {
                                Stop();
                                throw new ApplicationException(NativeMethods.limesdr_strerror());
                            }

                            double[] losc_freq = new double[16];
                            double[] pho = new double[1];

                            fixed (double* freq = &losc_freq[0])
                            fixed (double* pho_ptr = &pho[0])
                            {
                                losc_freq[0] = (30.0 * 1e6) - _centerFrequency;
                                losc_freq[15] = 0.0;

                                if (NativeMethods.LMS_SetNCOFrequency(_ptrLmsDevice, LMS_CH_RX, _channel, freq, 0.0) != 0)
                                {
                                    Stop();
                                    throw new ApplicationException(NativeMethods.limesdr_strerror());
                                }

                                if (NativeMethods.LMS_SetNCOIndex(_ptrLmsDevice, LMS_CH_RX, _channel, 0, false) != 0)
                                {
                                    Stop();
                                    throw new ApplicationException(NativeMethods.limesdr_strerror());
                                }
                            }
                        }
                            
                    }
                 
                }
                catch (Exception ex)
                {
                    throw ex;
                }

            }

        }

        public event EventHandler SampleRateChanged;

        public void OnSampleRateChanged()
        {
            EventHandler sampleRateChanged = this.SampleRateChanged;
            if (sampleRateChanged != null)
            {
                sampleRateChanged(this, EventArgs.Empty);
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

                if (_isStreaming)
                {
                    if (_ptrLmsDevice != IntPtr.Zero || _ptrLmsDevice != null)
                    {
                        if (NativeMethods.LMS_SetAntenna(_ptrLmsDevice, LMS_CH_RX, _channel, _ant) != 0)
                        {
                            throw new ApplicationException(NativeMethods.limesdr_strerror());
                        }
                    }
                }
            }
        }

        public double SampleRate
        {
            get
            {
                return _sampleRate;
            }

            set
            {
                _sampleRate = value;

                if (_ptrLmsDevice != IntPtr.Zero)
                {
                    _SetDeviceSampleRate(_sampleRate);
                }

                if (_isStreaming)
                {
                    Thread.Sleep(1000);
                    OnSampleRateChanged();
                }
            }
        }

        public double Gain
        {
            get
            {

                if (_isStreaming)
                {
                    if (_ptrLmsDevice != IntPtr.Zero)
                    {
                        if (NativeMethods.LMS_GetGaindB(_ptrLmsDevice, LMS_CH_RX, _channel, ref _gain) != 0)
                        {
                            throw new ApplicationException(NativeMethods.limesdr_strerror());
                        }

                    }
                }

                return _gain;
            }

            set
            {
                _gain = (uint)value;

                if (_isStreaming)
                {
                    if (_ptrLmsDevice != IntPtr.Zero)
                    {
                        if (NativeMethods.LMS_SetGaindB(_ptrLmsDevice, LMS_CH_RX, _channel, _gain) != 0)
                        {
                            throw new ApplicationException(NativeMethods.limesdr_strerror());
                        }

                    }
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

                if (_isStreaming)
                {
                    if (NativeMethods.LMS_SetLPFBW(_ptrLmsDevice, LMS_CH_RX, _channel, _lpbw) != 0)
                    {
                        throw new ApplicationException(NativeMethods.limesdr_strerror());
                    }
                }
            }
        }
    }

    public sealed class SamplesAvailableEventArgs : EventArgs
    {
        public int Length { get; set; }
        public unsafe Complex* Buffer { get; set; }
    }

    public delegate void SamplesAvailableDelegate(object sender, SamplesAvailableEventArgs e);

}
