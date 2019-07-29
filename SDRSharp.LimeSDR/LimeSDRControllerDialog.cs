/*
 * Based on project from https://github.com/jocover/sdrsharp-limesdr
 * 
 * modifications by YT7PWR 2018 https://github.com/GoranRadivojevic/sdrsharp-limesdr
 * modifications by netdog 2019 https://github.com/netdoggy/sdrsharp-limesdr
 */

using SDRSharp.Radio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SDRSharp.LimeSDR
{
    public partial class LimeSDRControllerDialog : Form
    {
        private readonly LimeSDRIO _owner;
        private bool _initialized;
        public double _sampleRate = 1.5 * 1e6;
        public double _freqDiff = 0.0;
        private Dictionary<int, string> _sampleRates = new Dictionary<int, string>();
        private ControllerPanel _panel = null;

        public LimeSDRControllerDialog(LimeSDRIO owner)
        {
            try
            {
                InitializeComponent();
                string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                toolStripStatusLabel_Version.Text = "v" + assemblyVersion.ToString().Replace(".0", "");

                float dpi = this.CreateGraphics().DpiX;
                float ratio = dpi / 96.0f;
                string font_name = this.Font.Name;
                float size = (float)(8.25 / ratio);
                System.Drawing.Font new_font = new System.Drawing.Font(font_name, size);
                this.Font = new_font;
                this.PerformAutoScale();
                this.PerformLayout();
                _owner = owner;
                InitSampleRates();
                GetDeviceList();


                _initialized = true;
                toolStripStatusLabel_RxRate.Text = "RxRate: 0 MB/s";
                toolStripStatusLabel_DroppedPackets.Text = "0";
                comboRadioModel.Text = Utils.GetStringSetting("LimeSDR.model", "");

                tbLimeSDR_Gain.Value = Utils.GetIntSetting("LimeSDR.Gain", 40);
                tbLimeSDR_LNAGain.Value = Utils.GetIntSetting("LimeSDR.GainLNA", tbLimeSDR_LNAGain.Value); // default fron TrackBar defaultValue
                tbLimeSDR_TIAGain.Value = Utils.GetIntSetting("LimeSDR.GainTIA", tbLimeSDR_TIAGain.Value);
                tbLimeSDR_PGAGain.Value = Utils.GetIntSetting("LimeSDR.GainPGA", tbLimeSDR_PGAGain.Value);
                tbLimeSDR_PGAGain.Value = Utils.GetIntSetting("LimeSDR.GainPGA", tbLimeSDR_PGAGain.Value);

              

                RefreshLabelLnaGain();
                RefreshLabelTiaGain();
                RefreshLabelPgaGain();

                tbDcRemoval.Value = Utils.GetIntSetting("LimeSDR.DCRemove.TbValue", tbDcRemoval.Value);

                gainBar_Scroll(this, EventArgs.Empty); // set device param from gui
                tbDcRemover_Scroll(this, EventArgs.Empty); // set device param from gui

                lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value.ToString() + "dB";
                samplerateComboBox.SelectedValue = Int32.Parse(Utils.GetStringSetting("LimeSDR.SampleRate", "2304000"));
                LPBWcomboBox.Text = Utils.GetStringSetting("LimeSDR.LPBW", "60MHz");
                rx0.Checked = Utils.GetBooleanSetting("LimeSDR.RX0", true);
                rx1.Checked = Utils.GetBooleanSetting("LimeSDR.RX1");
                ant_l.Checked = Utils.GetBooleanSetting("LimeSDR.ANT_L", true);
                ant_h.Checked = Utils.GetBooleanSetting("LimeSDR.ANT_H");
                ant_w.Checked = Utils.GetBooleanSetting("LimeSDR.ANT_W");
                udSpecOffset.Value = (decimal)Utils.GetDoubleSetting("LimeSDR.SpecOffset", 1);
                udSpecOffset_ValueChanged(this, EventArgs.Empty); // force resave param  SpecOffset to Onwer (device) obj
                udFrequencyDiff.Value = (decimal)Utils.GetDoubleSetting("LimeSDR.FrequencyDiff", 0.0);
                RefreshLimeSdrTemp();
                GetLimeSuiteLibVersion();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "LimeSDR Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.Write(ex.ToString());     
                throw ex;
            }
        }


        public void setPanel(ControllerPanel panel)
        {
            _panel = panel;
        }
        private bool Initialized
        {
            get
            {
                return _initialized && _owner != null;
            }
        }

        private void InitSampleRates()
        {
            int[] sampleRates =
            {
                192000,
                384000,
                768000,
                1536000,
                2304000,
                3072000,
                6144000,
                8192000,
                12288000,
                19200000,
                24576000,
                30000000,
                32767999,
                35000000,
                36864000,
                40000000,
                40550400,
                49152000,
                55296000,
                64143360,
                65000000,
            };

            foreach (var sr in sampleRates)
            {
                var displayValue = Math.Round(sr / 1e6f, 4);
                _sampleRates.Add(sr, displayValue.ToString() + " MSPS");
            }


            samplerateComboBox.DataSource = new BindingSource(_sampleRates, null);
            samplerateComboBox.DisplayMember = "Value";
            samplerateComboBox.ValueMember = "Key";
            samplerateComboBox.SelectedValue = 2304000;
            // string key = ((KeyValuePair<string, string>)samplerateComboBox.SelectedItem).Key;
            // string value = ((KeyValuePair<string, string>)samplerateComboBox.SelectedItem).Value;
        }

        public double LPBW
        {
            get
            {
                var commaCulture = new System.Globalization.CultureInfo("en")
                {
                    NumberFormat = { NumberDecimalSeparator = "." }
                };
                return (double)(UInt32)(double.Parse(LPBWcomboBox.Text.Replace("MHz", ""), commaCulture) * 1e6);
            }
        }

        public ushort TIAGain
        {
            get
            {
                return (ushort)tbLimeSDR_TIAGain.Value;
            }
        }

        public ushort PGAGain
        {
            get
            {
                return (ushort)tbLimeSDR_PGAGain.Value;
            }
        }


        public ushort LNAGain
        {
            get
            {
                return (ushort)tbLimeSDR_LNAGain.Value;
            }
        }

        public float DcRemovalRatio
        {
            get
            {
                return DcRemovalConvertValueToRatio(tbDcRemoval.Value);
            }
        }

        private void samplerateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int value = ((KeyValuePair<int, string>)samplerateComboBox.SelectedItem).Key;

            if (!Initialized)
            {
                _sampleRate = value; 
                return;
            }

            try
            {
                _sampleRate = value;
                Utils.SaveSetting("LimeSDR.SampleRate", value);
                _owner._sampleRate = _sampleRate;

                if (_owner.Device != null)
                    _owner.Device.SampleRate = _sampleRate;
            }
            catch (Exception ex)
            {
                _sampleRate = 1.5 * 1e6;
                throw ex;
            }
        }

        private void saveSettings()
        {
            Utils.SaveSetting("LimeSDR.GainLNA", (int)tbLimeSDR_LNAGain.Value);
            Utils.SaveSetting("LimeSDR.GainTIA", (int)tbLimeSDR_TIAGain.Value);
            Utils.SaveSetting("LimeSDR.GainPGA", (int)tbLimeSDR_PGAGain.Value);
        }
        private void close_Click(object sender, EventArgs e)
        {
            Hide();
            saveSettings();
        }

        private void LimeSDRControllerDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
            saveSettings();
        }


        private void gainBar_Scroll(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }


            _owner.Gain = tbLimeSDR_Gain.Value; // write to device
            lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value + "dB";
            Utils.SaveSetting("LimeSDR.Gain", (int)tbLimeSDR_Gain.Value);

            if (_owner.Device != null && _owner.Device.IsStreaming) // if you read when the device is not open then default values will be obtained, which is not correct
            {
                // read from device if open
                tbLimeSDR_LNAGain.Value = _owner.LNAgain;
                RefreshLabelLnaGain();

                tbLimeSDR_TIAGain.Value = _owner.TIAgain;
                RefreshLabelTiaGain();

                tbLimeSDR_PGAGain.Value = _owner.PGAgain;
                RefreshLabelPgaGain();
            }
        }

        private void tbLimeSDR_PGAGain_Scroll(object sender, EventArgs e)
        {
            try
            {
                Utils.SaveSetting("LimeSDR.GainPGA", (int)tbLimeSDR_PGAGain.Value);
                RefreshLabelPgaGain();

                if (_owner != null)
                {
                    _owner.PGAgain = (ushort)tbLimeSDR_PGAGain.Value;
                    UpdateAutoGainFromLimeSdr();

                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                throw ex;
            }
        }


        private void tbLimeSDR_LNAGain_Scroll(object sender, EventArgs e)
        {


            try
            {
                Utils.SaveSetting("LimeSDR.GainLNA", (int)tbLimeSDR_LNAGain.Value);
                RefreshLabelLnaGain();
                if (_owner != null)
                {
                    _owner.LNAgain = (ushort)tbLimeSDR_LNAGain.Value;
                    UpdateAutoGainFromLimeSdr();
                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                throw ex;
            }
        }

        private void tbLimeSDR_TIAGain_Scroll(object sender, EventArgs e)
        {
            try
            {
                Utils.SaveSetting("LimeSDR.GainTIA", (int)tbLimeSDR_TIAGain.Value);
                RefreshLabelTiaGain();

                if (_owner != null)
                {
                    _owner.TIAgain = (ushort)tbLimeSDR_TIAGain.Value;
                    UpdateAutoGainFromLimeSdr();
                }

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                throw ex;
            }
        }



        private void rx0_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }


            var prevChannel = _owner.Channel;
            if (rx0.Checked)
                _owner.Channel = 0;

            Utils.SaveSetting("LimeSDR.RX0", rx0.Checked);

            if (_owner.Channel != prevChannel)
                ChangeChannel();
        }
        private void rx1_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }
            var prevChannel = _owner.Channel;
            if (rx1.Checked)
                _owner.Channel = 1;

            Utils.SaveSetting("LimeSDR.RX1", rx1.Checked);

            if (_owner.Channel != prevChannel)
                ChangeChannel();
        }

        private void ChangeChannel()
        {
            if (!Initialized)
            {
                return;
            }

            if (_owner.Device != null && _owner.Device.IsStreaming)
            {
                _owner.Restart();
                //var c = await System.Threading.Tasks.Task<int>.Factory.StartNew(() =>
                //{                   
                //});                
                //MessageBox.Show("restarted channel");
            }           
        }
 

        private void ant_h_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_h.Checked)
                _owner.Antenna = 1;

            Utils.SaveSetting("LimeSDR.ANT_H", ant_h.Checked);
        }

        private void ant_l_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_l.Checked)
                _owner.Antenna = 2;

            Utils.SaveSetting("LimeSDR.ANT_L", ant_l.Checked);
        }

        private void ant_w_CheckedChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            if (ant_w.Checked)
                _owner.Antenna = 3;

            Utils.SaveSetting("LimeSDR.ANT_W", ant_w.Checked);
        }

        private void LPBWcomboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }
        
            try
            {
                var commaCulture = new System.Globalization.CultureInfo("en")
                {
                    NumberFormat = { NumberDecimalSeparator = "." }
                };

                _owner.LPBW = (double)(UInt32)(double.Parse(LPBWcomboBox.Text.Replace("MHz", ""), commaCulture) * 1e6);
                Utils.SaveSetting("LimeSDR.LPBW", LPBWcomboBox.Text);
         
            }
            catch (Exception ex)
            {
                _owner.LPBW = 60 * 1e6;
                throw ex;
            }
        }

        private void udSpecOffset_ValueChanged(object sender, EventArgs e)
        {
            if (!Initialized)
            {
                return;
            }

            try
            {
                if (_owner != null && _owner.Device != null)
                    _owner.Device.SpectrumOffset = (float)udSpecOffset.Value;

                if (_owner != null)
                    _owner.SpecOffset = (float)udSpecOffset.Value;

                Utils.SaveSetting("LimeSDR.SpecOffset", udSpecOffset.Value.ToString());
            }
            catch
            {

            }
        }

        private void btnRadioRefresh_Click(object sender, EventArgs e)
        {
            RefreshStreamStatus();
            try
            {
                //if (_owner != null && !_owner._isStreaming)
                if (_owner != null && !_owner.Device.IsStreaming)
                {
                    comboRadioModel.Items.Clear();
                    GetDeviceList();
                    comboRadioModel.Text = Utils.GetStringSetting("LimeSDR.model", "");
                }

                GetLimeSDRDeviceInfo();

            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        unsafe void GetLimeSuiteLibVersion()
        {
            IntPtr libVersion = (IntPtr)NativeMethods.LMS_GetLibraryVersion();
            txtLimeSuiteVersion.Text = Marshal.PtrToStringAnsi(libVersion);
        }
        unsafe public void GetDeviceList()
        {
            try
            {
                ASCIIEncoding ascii = new ASCIIEncoding();
                byte[] info = new byte[2048];
                int count = 0;

                fixed (byte* deviceList = &info[0])
                {
                    count = NativeMethods.LMS_GetDeviceList(deviceList);
                }

                for (int i = 0; i < count; i++)
                    comboRadioModel.Items.Add(ascii.GetString(info, i * 256, 256).Trim('\0'));
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void comboRadioModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string[] data = new string[5];
                data = comboRadioModel.SelectedItem.ToString().Split(',');
                txtRadioName.Text = data[0];
                txtRadioSerialNo.Text = data[3].Replace("serial=", "");
                txtModule.Text = data[2].Replace("module=", "");
                _owner.RadioName = comboRadioModel.SelectedItem.ToString();
                Utils.SaveSetting("LimeSDR.model", comboRadioModel.Text);

                txtRadioModel.Text = "";
                txtFirm_version.Text = "";
                txtSerialNumber.Text = "";
                txtGatewareVersion.Text = "";
               //txtLimeSuiteVersion.Text = "";
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        private void udFrequencyDiff_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                _freqDiff = (double)udFrequencyDiff.Value * 1e3;

                if (_owner != null)
                    _owner.FreqDiff = (double)udFrequencyDiff.Value * 1e3;

                Utils.SaveSetting("LimeSDR.FrequencyDiff", udFrequencyDiff.Value.ToString());
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                throw ex;
            }
        }



        private void UpdateAutoGainFromLimeSdr()
        {
            tbLimeSDR_Gain.Value = (int)_owner.Gain;
            lblLimeSDR_GainDB.Text = tbLimeSDR_Gain.Value.ToString() + "dB";
        }

        private void RefreshLabelLnaGain()
        {
            string[] LnaValues = { "N/A", "-30 (0dB)", "-27 (3dB)", "-24 (6dB)", "-21 (9dB)", "-18 (12dB)", "-15 (15dB)", "-12 (18dB)", "-9 (21dB)", "-6 (24dB)", "-5 (25dB)", "-4 (26dB)", "-3 (27dB)", "-2 (28)", "-1 (29dB)", "Max (30dB)" };
            lblLimeSDR_LNAGain.Text = LnaValues[tbLimeSDR_LNAGain.Value];
        }

        private void RefreshLabelTiaGain()
        {
            string[] TiaValues = { "N/A", "-12 (0dB)", "-3 (9dB)", "Max (12dB)" };
            lblLimeSDR_TIAGain.Text = TiaValues[tbLimeSDR_TIAGain.Value];
        }

        private void RefreshLabelPgaGain()
        {
            lblLimeSDR_PGAGain.Text = (-12 + tbLimeSDR_PGAGain.Value).ToString() + "dB";
        }

        public void RefreshFormAllGains()
        {
            if (_owner == null)
                return;

            // read data from device 
            tbLimeSDR_LNAGain.Value = _owner.LNAgain;
            RefreshLabelLnaGain();

            tbLimeSDR_TIAGain.Value = _owner.TIAgain;
            RefreshLabelTiaGain();

            tbLimeSDR_PGAGain.Value = _owner.PGAgain;
            RefreshLabelPgaGain();
        }



        public lms_stream_status_t lms_stream_status = new lms_stream_status_t();
        //public lms_stream_status_t lms_stream_status_accomulated = new lms_stream_status_t();
        public void RefreshStreamStatus()
        {
            if (_owner == null || _owner.LimeSDR_Device == IntPtr.Zero || _owner.Device._ptrLmsStream == IntPtr.Zero)
            {
                return;
            }

            if (!_owner.Device.IsStreaming)
                return;

            NativeMethods.LMS_GetStreamStatus(_owner.Device._ptrLmsStream, out lms_stream_status);

            //uint.TryParse(this.txtDropPackets.Text, out res);
            //this.txtDropPackets.Text = (res + lms_stream_status.droppedPackets).ToString();
            toolStripStatusLabel_RxRate.Text =
                // lms_stream_status.fifoFilledCount.ToString() 
                //+ "/" + lms_stream_status.fifoSize.ToString() 
                //+ " | " + lms_stream_status.underrun.ToString() 
                //+ "/" + lms_stream_status.overrun.ToString() 
                //+ " | " + lms_stream_status.droppedPackets.ToString() 
                "RxRate: " + Math.Round(lms_stream_status.linkRate / 1e6, 2).ToString() + " MB/s";
            toolStripStatusLabel_DroppedPackets.Text = (uint.Parse(toolStripStatusLabel_DroppedPackets.Text) + lms_stream_status.droppedPackets).ToString();

            if (_owner.Device.IsStreaming && !lms_stream_status.active)
            {
                // does not work  .active always true
                //  _owner.Stop();
                // MessageBox.Show("LimeSDR Sreaming has been deactivated.");
            }

        }
        public void RefreshLimeSdrTemp()
        {
            if (_owner == null || _owner.Device == null)
                return;

            var temp = _owner.Device.Temperature();
            txtTemperature.Text = temp == 0 ? "-°" : Math.Round(temp, 2).ToString() + "°";
            toolStripStatusLabel_Temp.Text = txtTemperature.Text;

            if (_panel != null )
            {
                _panel.setTemp(toolStripStatusLabel_Temp.Text);
            }
        }
        public unsafe void GetLimeSDRDeviceInfo()
        {
            try
            {


                if (_owner.LimeSDR_Device != IntPtr.Zero)
                {
                    lms_dev_info_t info = new lms_dev_info_t();

                    info.deviceName = new char[32];
                    info.expansionName = new char[32];
                    info.firmwareVersion = new char[16];
                    info.hardwareVersion = new char[16];
                    info.protocolVersion = new char[16];
                    info.boardSerialNumber = 0;
                    info.gatewareVersion = new char[16];
                    info.gatewareTargetBoard = new char[32];
                    IntPtr deviceInfo;

                    deviceInfo = (IntPtr)NativeMethods.LMS_GetDeviceInfo(_owner.LimeSDR_Device);
                    byte[] buff = new byte[168];
                    Marshal.Copy(deviceInfo, buff, 0, 168);
                    ASCIIEncoding ascii = new ASCIIEncoding();
                    string s = ascii.GetString(buff);
                    string deviceName = ascii.GetString(buff, 0, 32).Trim('\0');
                    string expansionName = ascii.GetString(buff, 32, 32).Trim('\0');
                    string firmwareVersion = ascii.GetString(buff, 64, 16).Trim('\0');
                    string hardwareVersion = ascii.GetString(buff, 80, 16).Trim('\0');
                    string protocolVersion = ascii.GetString(buff, 96, 16).Trim('\0');
                    UInt64 serial = 0;

                    for (int i = 8; i > 0; i--)
                    {
                        serial += buff[111 + i];

                        if (i > 1)
                            serial = serial << 8;
                    }

                    string boardSerialNumber = serial.ToString("X");
                    string gatewareVersion = ascii.GetString(buff, 120, 16).Trim('\0');
                    string gatewareTargetBoard = ascii.GetString(buff, 136, 32).Trim('\0');

                    IntPtr libVersion = (IntPtr)NativeMethods.LMS_GetLibraryVersion();
                    txtLimeSuiteVersion.Text = Marshal.PtrToStringAnsi(libVersion);

                    txtRadioModel.Text = deviceName;
                    txtFirm_version.Text = firmwareVersion;
                    txtSerialNumber.Text = boardSerialNumber;
                    txtGatewareVersion.Text = gatewareVersion;
                    RefreshLimeSdrTemp();
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                txtRadioModel.Text = "";
                txtFirm_version.Text = "";
                txtSerialNumber.Text = "";
                txtGatewareVersion.Text = "";
                txtLimeSuiteVersion.Text = "";
            }

        }

      
        private void timerTemp_Tick(object sender, EventArgs e)
        {
            RefreshLimeSdrTemp();
            RefreshStreamStatus();
        }

        private void toolStripStatusLabel_Version_DoubleClick(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/netdoggy/sdrsharp-limesdr");
        }

        private void LimeSDRControllerDialog_Load(object sender, EventArgs e)
        {

        }

        private void toolStripStatusLabel_Author_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/netdoggy/sdrsharp-limesdr");
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
           
        }

        private float DcRemovalConvertValueToRatio (int value)
        {
            return value > 1.0f ? 1.0f / (float)Math.Pow(1.85, value) : 1.0f; 
        }
        private void tbDcRemover_Scroll(object sender, EventArgs e)
        {

            
            var ratio = DcRemovalConvertValueToRatio(tbDcRemoval.Value); // tbDcRemoval.Value > 1 ? 1.0 / (float)Math.Pow(1.85, tbDcRemoval.Value) : 1.0;
            var l = (int)Math.Floor(Math.Log10(1/ratio)) + 3;
            label18.Text = (ratio > 0.999999) ? "1(off)" : Math.Round(ratio, l).ToString();

            Utils.SaveSetting("LimeSDR.DCRemove.TbValue", tbDcRemoval.Value);

            if (_owner == null || _owner.Device == null)
                return;

             _owner.Device.dcRemovalRatio = (float)ratio;
 

        }
    }
}